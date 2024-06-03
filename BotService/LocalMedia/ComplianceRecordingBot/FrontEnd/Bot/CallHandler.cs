using AzureStorageTools;
using CommonTools;
using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.MediaBuffer;
using ComplianceRecordingBot.FrontEnd.Models;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using ComplianceRecordingBot.FrontEnd.Util;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using Microsoft.Skype.Bots.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace ComplianceRecordingBot.FrontEnd.Bot
{
    /// <summary>
    /// Call Handler Logic.
    /// </summary>
    public class CallHandler : HeartbeatHandler, IDisposable
    {
        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _Settings;

        /// <summary>
        /// The capture
        /// </summary>
        private CaptureEvents _Capture;

        /// <summary>
        /// The is disposed
        /// </summary>
        private bool _IsDisposed = false;

        /// <summary>
        /// Gets the call.
        /// </summary>
        /// <value>The call.</value>
        public ICall Call { get; }

        /// <summary>
        /// Gets the bot media stream.
        /// </summary>
        /// <value>The bot media stream.</value>
        public BotMediaStream BotMediaStream { get; private set; }

        /// <summary>
        /// MSI when there is no dominant speaker.
        /// </summary>
        public const uint DominantSpeakerNone = DominantSpeakerChangedEventArgs.None;

        // hashSet of the available sockets
        private readonly HashSet<uint> _AvailableSocketIds = new HashSet<uint>();

        // this is an LRU cache with the MSI values, we update this Cache with the dominant speaker events
        // this way we can make sure that the muliview sockets are subscribed to the active (speaking) participants
        private readonly LRUCache _CurrentVideoSubscriptions = new LRUCache(BotConstants.NumberOfMultiviewSockets + 1);

        private readonly object _SubscriptionLock = new object();

        // This dictionnary helps maintaining a mapping of the sockets subscriptions
        private readonly ConcurrentDictionary<uint, uint> _MsiToVideoSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _MsiToVBSSSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private ConcurrentDictionary<string, uint> _SubscribeVideoParticipant = new ConcurrentDictionary<string, uint>();

        private ConcurrentDictionary<string, uint> _SubscribeVBSSParticipant = new ConcurrentDictionary<string, uint>();

        private ConcurrentDictionary<uint, uint> _MsiToVideoSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        private ConcurrentDictionary<uint, uint> _MsiToVBSSSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        private readonly IGraphLogger _Logger;

        private int _RecordingStatusIndex = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="CallHandler"/> class.
        /// </summary>
        /// <param name="statefulCall">The stateful call.</param>
        /// <param name="settings">The settings.</param>
        public CallHandler(ICall statefulCall, AzureSettings settings) : base(TimeSpan.FromMinutes(10), statefulCall?.GraphLogger)
        {
            Call = statefulCall;
            _Logger = statefulCall.GraphLogger;
            _Settings = settings;
            NLogHelper.Instance.Debug($"[CallHandler] += CallOnUpdated");
            Call.OnUpdated += CallOnUpdated;
            // subscribe to dominant speaker event on the audioSocket
            var audioSocket = Call.GetLocalMediaSession().AudioSocket;
            // susbscribe to the participants updates, this will inform the bot if a particpant left/joined the conference
            NLogHelper.Instance.Debug($"[CallHandler] += ParticipantsOnUpdated");
            Call.Participants.OnUpdated += ParticipantsOnUpdated;
            foreach (var videoSocket in this.Call.GetLocalMediaSession().VideoSockets)
            {
                _AvailableSocketIds.Add((uint)videoSocket.SocketId);
            }
            // attach the botMediaStream
            BotMediaStream = new BotMediaStream(
                Call.GetLocalMediaSession(),
                GraphLogger,
                Call.Id,
                _Settings,
                _MsiToVideoSocketIdMapping,
                _MsiToVBSSSocketIdMapping,
                _MsiToVideoSerialNumMapping,
                _MsiToVBSSSerialNumMapping);
            if (_Settings.CaptureEvents)
            {
                var path = Path.Combine(_Settings.DefaultOutputFolder, _Settings.EventsFolder, statefulCall.GetLocalMediaSession().MediaSessionId.ToString(), "participants");
                _Capture = new CaptureEvents(path);
            }
        }

        /// <inheritdoc/>
        protected override Task HeartbeatAsync(ElapsedEventArgs args)
        {
            return Call.KeepAliveAsync();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _IsDisposed = true;
            var audioSocket = Call.GetLocalMediaSession().AudioSocket;
            Call.OnUpdated -= CallOnUpdated;
            Call.Participants.OnUpdated -= ParticipantsOnUpdated;
            foreach (var participant in Call.Participants)
            {
                participant.OnUpdated -= OnParticipantUpdated;
            }
            BotMediaStream?.Dispose();
            // Event - Dispose of the call completed ok
            NLogHelper.Instance.Debug($"[CallHandler] Call: {Call.Id} Disposed OK");
        }

        /// <summary>
        /// Called when recording status flip timer fires.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="e">The <see cref="System.Timers.ElapsedEventArgs" /> instance containing the event data.</param>
        private void OnRecordingStatusFlip(ICall source, ElapsedEventArgs e)
        {
            NLogHelper.Instance.Debug($"[CallHandler] OnRecordingStatusFlip start");
            _ = Task.Run(async () =>
            {
                var recordingStatus = new[] { RecordingStatus.Recording, RecordingStatus.NotRecording, RecordingStatus.Failed };
                var recordingIndex = _RecordingStatusIndex + 1;
                if (recordingIndex >= recordingStatus.Length)
                {
                    var recordedParticipantId = Call.Resource.IncomingContext.ObservedParticipantId;
                    NLogHelper.Instance.Debug($"[CallHandler] We've rolled through all the status'... removing participant {recordedParticipantId}");
                    var recordedParticipant = Call.Participants[recordedParticipantId];
                    await recordedParticipant.DeleteAsync().ConfigureAwait(false);
                    // Event - Recording has ended
                    NLogHelper.Instance.Debug($"[CallHandler] CallRecordingFlip Call.Id: {Call.Id} Recording has ended");
                    return;
                }
                var newStatus = recordingStatus[recordingIndex];
                try
                {
                    // Event - Log the recording status
                    var status = Enum.GetName(typeof(RecordingStatus), newStatus);
                    NLogHelper.Instance.Debug($"[CallHandler] CallRecordingFlip Call: {Call.Id} status changed to {status}");

                    // NOTE: if your implementation supports stopping the recording during the call, you can call the same method above with RecordingStatus.NotRecording
                    await source.UpdateRecordingStatusAsync(newStatus).ConfigureAwait(false);

                    _RecordingStatusIndex = recordingIndex;
                }
                catch (Exception exc)
                {
                    // Event - Recording status exception - failed to update
                    // e.g. bot joins via direct join - may not have the permissions
                    NLogHelper.Instance.Debug($"[CallHandler Error] Failed to flip the recording status to {newStatus} Msg: {exc.Message}");
                }
            }).ForgetAndLogExceptionAsync(_Logger);
        }

        /// <summary>
        /// Event fired when the call has been updated.
        /// </summary>
        /// <param name="sender">The call.</param>
        /// <param name="e">The event args containing call changes.</param>
        private async void CallOnUpdated(ICall sender, ResourceEventArgs<Call> e)
        {
            if (e.OldResource.State != e.NewResource.State && e.NewResource.State == CallState.Established)
            {
                NLogHelper.Instance.Debug($"[CallHandler] CallOnUpdated State:{e.NewResource.State}");
                if (!_IsDisposed)
                {
                    // Call is established. We should start receiving Audio, we can inform clients that we have started recording.
                    OnRecordingStatusFlip(sender, null);
                }
            }
            if ((e.OldResource.State == CallState.Established) && (e.NewResource.State == CallState.Terminated))
            {
                NLogHelper.Instance.Debug($"[CallHandler] CallOnUpdated State:{e.NewResource.State}");
                if (BotMediaStream != null)
                {
                    var aQoE = BotMediaStream.GetAudioQualityOfExperienceData();
                    if (aQoE != null)
                    {
                        if (_Settings.CaptureEvents)
                            await _Capture?.Append(aQoE);
                    }

                    NLogHelper.Instance.Debug($"[CallHandler] CallOnUpdated BotMediaStream StopMedia");
                    await BotMediaStream.StopMedia();
                }
                else
                {
                    NLogHelper.Instance.Debug($"[CallHandler] CallOnUpdated BotMediaStream is null");
                }

                if (_Settings.CaptureEvents)
                    await _Capture?.Finalise();
            }
        }

        /// <summary>
        /// Creates the participant update json.
        /// </summary>
        /// <param name="participantId">The participant identifier.</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string createParticipantUpdateJson(string participantId, string participantDisplayName = "")
        {
            if (participantDisplayName.Length == 0)
                return "{" + String.Format($"\"Id\": \"{participantId}\"") + "}";
            else
                return "{" + String.Format($"\"Id\": \"{participantId}\", \"DisplayName\": \"{participantDisplayName}\"") + "}";
        }

        /// <summary>
        /// Updates the participant.
        /// </summary>
        /// <param name="participants">The participants.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        /// <param name="participantDisplayName">Display name of the participant.</param>
        /// <returns>System.String.</returns>
        private string updateParticipant(List<IParticipant> participants, IParticipant participant, bool added, string participantDisplayName = "")
        {
            if (added)
                participants.Add(participant);
            else
                participants.Remove(participant);

            return createParticipantUpdateJson(participant.Id, participantDisplayName);
        }

        /// <summary>
        /// Updates the participants.
        /// </summary>
        /// <param name="eventArgs">The event arguments.</param>
        /// <param name="added">if set to <c>true</c> [added].</param>
        private void updateParticipants(ICollection<IParticipant> eventArgs, bool added = true)
        {
            foreach (var participant in eventArgs)
            {
                var json = string.Empty;
                // todo remove the cast with the new graph implementation,
                // for now we want the bot to only subscribe to "real" participants
                var participantDetails = participant.Resource.Info.Identity.User;
                if (participantDetails != null)
                {
                    json = updateParticipant(BotMediaStream.Participants, participant, added, participantDetails.DisplayName);
                }
                else if (participant.Resource.Info.Identity.AdditionalData?.Count > 0)
                {
                    if (CheckParticipantIsUsable(participant))
                    {
                        json = updateParticipant(BotMediaStream.Participants, participant, added);
                    }
                }
                if (json.Length > 0)
                {
                    if (added)
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] CallParticipantAdded: {json}");
                    }
                    else
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] CallParticipantRemoved: {json}");
                    }
                }
            }
        }

        /// <summary>
        /// Checks the participant is usable.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CheckParticipantIsUsable(IParticipant p)
        {
            foreach (var i in p.Resource.Info.Identity.AdditionalData)
            {
                NLogHelper.Instance.Debug($"[CallHandler] CheckParticipantIsUsable: {i.Key}");
                if (i.Key != "applicationInstance" && i.Value is Identity)
                {
                    NLogHelper.Instance.Debug($"[CallHandler] CheckParticipantIsUsable return true Value is Identity: {i.Key}");
                    return true;
                }
            }
            NLogHelper.Instance.Debug($"[CallHandler] CheckParticipantIsUsable return false");
            return false;
        }

        /// <summary>
        /// Event fired when the participants collection has been updated.
        /// </summary>
        /// <param name="sender">Participants collection.</param>
        /// <param name="args">Event args containing added and removed participants.</param>
        private void ParticipantsOnUpdated(IParticipantCollection sender, CollectionEventArgs<IParticipant> args)
        {
            if (_Settings.CaptureEvents)
            {
                _Capture?.Append(args);
            }
            updateParticipants(args.AddedResources);
            updateParticipants(args.RemovedResources, false);
            foreach (var participant in args.AddedResources)
            {
                NLogHelper.Instance.Debug($"[CallHandler] AddedResources participant: {participant.Id}");
                // todo remove the cast with the new graph implementation,
                // for now we want the bot to only subscribe to "real" participants
                var participantDetails = participant.Resource.Info.Identity.User;
                if (participantDetails != null)
                {
                    NLogHelper.Instance.Debug($"[CallHandler] ParticipantDetailsID(Object Id): {participantDetails.Id}"); //AAD User Object Id
                    BlobHelper blobHelper = null;
                    var json = string.Empty;
                    try
                    {
                        blobHelper = new BlobHelper(_Settings.BlobServiceEndpoint, _Settings.StorageAccountName, _Settings.StorageAccountKey);
                        json = blobHelper.DownloadBlob(BotConstants.AzureStorageContainerName, participantDetails.Id);
                    }
                    catch (Exception ex)
                    {
                        NLogHelper.Instance.Debug($"[CallHandler Error] DownloadBlob Msg: {ex.Message}");
                        json = string.Empty;
                    }
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] DownloadBlob: {json}");
                        var blobModel = JsonConvert.DeserializeObject<BlobModel>(json);
                        blobModel.CallId = Call.Id;
                        var contents = JsonConvert.SerializeObject(blobModel);
                        blobHelper.UploadBlob(BotConstants.AzureStorageContainerName, participantDetails.Id, contents);
                        Bot.Instance.CallMeetingMappingInfo.TryAdd(Call.Id, blobModel);
                        NLogHelper.Instance.Debug($"[CallHandler] CallMeetingMappingInfo TryAdd CallId: {Call.Id} contents: {contents}");
                    }
                }
                else
                    NLogHelper.Instance.Debug("[CallHandler] participantDetails is null");

                // subscribe to the participant updates, this will indicate if the user started to share,
                // or added another modality
                NLogHelper.Instance.Debug($"[CallHandler] += OnParticipantUpdated");
                participant.OnUpdated += OnParticipantUpdated;
                // the behavior here is to avoid subscribing to a new participant video if the VideoSubscription cache is full
                NLogHelper.Instance.Debug($"[CallHandler] SubscribeToParticipantVideo");
                SubscribeToParticipantVideo(participant, forceSubscribe: false);
            }
            foreach (var participant in args.RemovedResources)
            {
                NLogHelper.Instance.Debug($"[CallHandler] -= OnParticipantUpdated RemovedResources participant: {participant.Id}");
                // unsubscribe to the participant updates
                participant.OnUpdated -= OnParticipantUpdated;
                UnsubscribeFromParticipantVideo(participant);
            }
        }

        /// <summary>
        /// Event fired when a participant is updated.
        /// </summary>
        /// <param name="sender">Participant object.</param>
        /// <param name="args">Event args containing the old values and the new values.</param>
        private void OnParticipantUpdated(IParticipant sender, ResourceEventArgs<Participant> args)
        {
            SubscribeToParticipantVideo(sender, forceSubscribe: false);
        }

        /// <summary>
        /// Unsubscribe and free up the video socket for the specified participant.
        /// </summary>
        /// <param name="participant">Particant to unsubscribe the video.</param>
        private void UnsubscribeFromParticipantVideo(IParticipant participant)
        {
            NLogHelper.Instance.Debug($"[CallHandler] UnsubscribeFromParticipantVideo: {participant.Id}");
            var participantSendCapableVideoStream = participant.Resource.MediaStreams.Where(x => x.MediaType == Modality.Video &&
              (x.Direction == MediaDirection.SendReceive || x.Direction == MediaDirection.SendOnly)).FirstOrDefault();
            if (participantSendCapableVideoStream != null)
            {
                var msi = uint.Parse(participantSendCapableVideoStream.SourceId);
                NLogHelper.Instance.Debug($"[CallHandler] UnsubscribeFromParticipantVideo Video MSI: {msi}");
                lock (_SubscriptionLock)
                {
                    if (_CurrentVideoSubscriptions.TryRemove(msi))
                    {
                        if (_MsiToVideoSocketIdMapping.TryRemove(msi, out uint socketId))
                        {
                            BotMediaStream.Unsubscribe(MediaType.Video, socketId);
                            _AvailableSocketIds.Add(socketId);
                        }
                    }
                }
            }

            var vbssParticipantVBSSStream = participant.Resource.MediaStreams.SingleOrDefault(x => x.MediaType == Modality.VideoBasedScreenSharing && x.Direction == MediaDirection.SendOnly);
            if (vbssParticipantVBSSStream != null)
            {
                var msi = uint.Parse(vbssParticipantVBSSStream.SourceId);
                NLogHelper.Instance.Debug($"[CallHandler] VBSS MSI: {msi}");
                if (_MsiToVBSSSocketIdMapping.TryRemove(msi, out uint socketId))
                {
                    BotMediaStream.Unsubscribe(MediaType.Vbss, socketId);
                }
            }
        }

        /// <summary>
        /// Subscribe to video or vbss sharer.
        /// if we set the flag forceSubscribe to true, the behavior is to subscribe to a video even if there is no available socket left.
        /// in that case we use the LRU cache to free socket and subscribe to the new MSI.
        /// </summary>
        /// <param name="participant">Participant sending the video or VBSS stream.</param>
        /// <param name="forceSubscribe">If forced, the least recently used video socket is released if no sockets are available.</param>
        private void SubscribeToParticipantVideo(IParticipant participant, bool forceSubscribe = true)
        {
            try
            {
                NLogHelper.Instance.Debug($"[CallHandler] SubscribeToParticipantVideo callID: {Call.Id} participantID: {participant.Id}");
                bool subscribeToVideo = false;
                uint socketId = uint.MaxValue;
                // filter the mediaStreams to see if the participant has a video send
                var participantSendCapableVideoStream = participant.Resource.MediaStreams.Where(x => x.MediaType == Modality.Video && (x.Direction == MediaDirection.SendReceive || x.Direction == MediaDirection.SendOnly)).FirstOrDefault();
                if (participantSendCapableVideoStream == null)
                {
                    if (_SubscribeVideoParticipant.ContainsKey(participant.Id))
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] participantSendCapableVideoStream is close callID: {Call.Id} participantID: {participant.Id}");
                        BotMediaStream.StopVideo().ConfigureAwait(false);
                        NLogHelper.Instance.Debug($"[CallHandler] BotMediaStream.StopVideo()");
                        lock (_SubscriptionLock)
                        {
                            if (_CurrentVideoSubscriptions.TryRemove(_SubscribeVideoParticipant[participant.Id]))
                            {
                                if (_MsiToVideoSocketIdMapping.TryRemove(_SubscribeVideoParticipant[participant.Id], out uint sktId))
                                {
                                    _AvailableSocketIds.Add(sktId);

                                    NLogHelper.Instance.Debug($"[CallHandler] _MsiToVideoSocketIdMapping.TryRemove participantId: {participant.Id} msi: {_SubscribeVideoParticipant[participant.Id]} socketId: {sktId}");
                                }
                            }
                        }
                    }
                    else
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] participantSendCapableVideoStream is null callID: {Call.Id} participantID: {participant.Id}");
                    }
                }
                else
                {
                    bool updateMSICache = false;
                    var msi = uint.Parse(participantSendCapableVideoStream.SourceId);
                    NLogHelper.Instance.Debug($"[CallHandler] participantSendCapableVideoStream msi: {msi}");
                    lock (_SubscriptionLock)
                    {
                        var callVideoSocketsCount = Call.GetLocalMediaSession().VideoSockets.Count;
                        NLogHelper.Instance.Debug($"[CallHandler] currentVideoSubscriptions.Count: {_CurrentVideoSubscriptions.Count} callVideoSocketsCount: {callVideoSocketsCount}");
                        if (_CurrentVideoSubscriptions.Count < callVideoSocketsCount)
                        {
                            // we want to verify if we already have a socket subscribed to the MSI
                            if (!_MsiToVideoSocketIdMapping.ContainsKey(msi))
                            {
                                subscribeToVideo = true;
                                if (_AvailableSocketIds.Any())
                                {
                                    socketId = _AvailableSocketIds.Last();
                                    _AvailableSocketIds.Remove(socketId);
                                    NLogHelper.Instance.Debug($"[CallHandler] set video SocketId: {socketId}");
                                    uint serialNum;
                                    if (_MsiToVideoSerialNumMapping.TryGetValue(msi, out serialNum))
                                        serialNum++;
                                    else
                                        serialNum = 1;

                                    _MsiToVideoSerialNumMapping.AddOrUpdate(msi, serialNum, (k, v) => serialNum);
                                    NLogHelper.Instance.Debug($"[CallHandler] set video serialNum: {serialNum}");
                                }
                                else
                                {
                                    NLogHelper.Instance.Debug($"[CallHandler] no _AvailableSocketIds can use.");
                                }
                            }
                            else
                                NLogHelper.Instance.Debug($"[CallHandler] _msiToVideoSocketIdMapping ContainsKey msi: {msi}");

                            updateMSICache = true;
                        }
                        else if (forceSubscribe)
                        {
                            NLogHelper.Instance.Debug($"[CallHandler] is forceSubscribe");
                            // here we know that all the sockets subscribed to a video we need to update the msi cache,
                            // and obtain the socketId to reuse with the new MSI
                            updateMSICache = true;
                            subscribeToVideo = true;
                        }
                        else
                        {
                            NLogHelper.Instance.Debug($"[CallHandler] CallID: {Call.Id} ParticipantID:{participant.Id} CurrVideoSubscriptions.Count: {_CurrentVideoSubscriptions.Count} forceSubscribe:{forceSubscribe} updateMSICache: {updateMSICache} subscribeToVideo: {subscribeToVideo}");
                        }
                        if (updateMSICache)
                        {
                            NLogHelper.Instance.Debug($"[CallHandler] updateMSICache");
                            _CurrentVideoSubscriptions.TryInsert(msi, out uint? dequeuedMSIValue);
                            if (dequeuedMSIValue != null)
                            {
                                // Cache was updated, we need to use the new available socket to subscribe to the MSI
                                _MsiToVideoSocketIdMapping.TryRemove((uint)dequeuedMSIValue, out socketId);
                            }
                        }
                    }
                    if (subscribeToVideo && socketId != uint.MaxValue)
                    {
                        _MsiToVideoSocketIdMapping.AddOrUpdate(msi, socketId, (k, v) => socketId);
                        BotMediaStream.Subscribe(MediaType.Video, msi, VideoResolution.HD1080p, socketId);
                        _SubscribeVideoParticipant.AddOrUpdate(participant.Id, msi, (k, v) => msi);
                        NLogHelper.Instance.Debug($"[CallHandler] Subscribe Video CallID: {Call.Id} ParticipantID:{participant.Id} msi: {msi} SocketID: {socketId}");
                    }
                }
                // vbss viewer subscription
                var vbssParticipant = participant.Resource.MediaStreams.SingleOrDefault(x => x.MediaType == Modality.VideoBasedScreenSharing && x.Direction == MediaDirection.SendOnly);
                if (vbssParticipant == null)
                {
                    if (_SubscribeVBSSParticipant.ContainsKey(participant.Id))
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] VBSS Stream is close callID: {Call.Id} participantID: {participant.Id}");
                        BotMediaStream.StopVBSS().ConfigureAwait(false);
                        NLogHelper.Instance.Debug($"[CallHandler] BotMediaStream.StopVBSS()");
                    }
                    else
                    {
                        NLogHelper.Instance.Debug($"[CallHandler] VBSS Stream is null callID: {Call.Id} participantID: {participant.Id}");
                    }
                }
                else
                {
                    var msi = uint.Parse(vbssParticipant.SourceId);
                    uint serialNum;

                    if (_MsiToVBSSSerialNumMapping.TryGetValue(msi, out serialNum))
                        serialNum++;
                    else
                        serialNum = 1;

                    _MsiToVBSSSerialNumMapping.AddOrUpdate(msi, serialNum, (k, v) => serialNum);
                    NLogHelper.Instance.Debug($"[CallHandler] set VBSS serialNum: {serialNum}");
                    // new sharer
                    _MsiToVBSSSocketIdMapping.AddOrUpdate(msi, socketId, (k, v) => socketId);
                    _SubscribeVBSSParticipant.AddOrUpdate(participant.Id, msi, (k, v) => msi);
                    BotMediaStream.Subscribe(MediaType.Vbss, msi, VideoResolution.HD1080p, socketId);
                    NLogHelper.Instance.Debug($"[CallHandler] Subscribe VBSS CallID: {Call.Id} ParticipantID:{participant.Id} msi: {msi} SocketID: {socketId} serialNum: {serialNum}");
                }
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[CallHandler Error] SubscribeToParticipantVideo callID: {Call.Id} participantID: {participant.Id} Msg: {ex.Message}");
            }
        }

        /// <summary>
        /// Listen for dominant speaker changes in the conference.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The dominant speaker changed event arguments.
        /// </param>
        private void OnDominantSpeakerChanged(object sender, DominantSpeakerChangedEventArgs e)
        {
            NLogHelper.Instance.Debug($"[CallHandler] CallID: {Call.Id} OnDominantSpeakerChanged(DominantSpeaker={e.CurrentDominantSpeaker})]");
            if (e.CurrentDominantSpeaker != DominantSpeakerNone)
            {
                IParticipant participant = GetParticipantFromMSI(e.CurrentDominantSpeaker);
                var participantDetails = participant?.Resource?.Info?.Identity?.User;
                if (participantDetails != null)
                {
                    NLogHelper.Instance.Debug($"[CallHandler] CurrentDominantSpeaker participantID: {participant.Id}");

                    // we want to force the video subscription on dominant speaker events
                    SubscribeToParticipantVideo(participant, forceSubscribe: true);
                }
            }
        }

        /// <summary>
        /// Gets the participant with the corresponding MSI.
        /// </summary>
        /// <param name="msi">media stream id.</param>
        /// <returns>
        /// The <see cref="IParticipant"/>.
        /// </returns>
        private IParticipant GetParticipantFromMSI(uint msi)
        {
            return Call.Participants.SingleOrDefault(x =>
            x.Resource.IsInLobby == false && x.Resource.MediaStreams.Any(y => y.SourceId == msi.ToString()));
        }
    }
}