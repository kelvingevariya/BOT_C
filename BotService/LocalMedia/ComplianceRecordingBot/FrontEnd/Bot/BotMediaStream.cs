namespace ComplianceRecordingBot.FrontEnd.Bot
{
    using CommonTools.Logging;
    using ComplianceRecordingBot.FrontEnd.Contract;
    using ComplianceRecordingBot.FrontEnd.Media;
    using ComplianceRecordingBot.FrontEnd.ServiceSetup;
    using Microsoft.Graph.Communications.Calls;
    using Microsoft.Graph.Communications.Calls.Media;
    using Microsoft.Graph.Communications.Common;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Skype.Bots.Media;
    using Microsoft.Skype.Internal.Media.Services.Common;
    using NLog;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Class responsible for streaming audio and video.
    /// </summary>
    public class BotMediaStream : ObjectRootDisposable
    {
        private readonly IAudioSocket _AudioSocket;
        private readonly IVideoSocket _VBSSSocket;
        private readonly List<IVideoSocket> _VideoSockets;
        private readonly ILocalMediaSession _MediaSession;

        /// <summary>
        /// The media stream
        /// </summary>
        private readonly IMediaStream _MediaStream;

        /// <summary>
        /// The participants
        /// </summary>
        internal List<IParticipant> Participants;

        /// <summary>
        /// The call identifier
        /// </summary>
        private readonly string _CallId;

        /// <summary>
        /// Return the last read 'audio quality of experience data' in a serializable structure
        /// </summary>
        /// <value>The audio quality of experience data.</value>
        public SerializableAudioQualityOfExperienceData AudioQualityOfExperienceData { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BotMediaStream"/> class.
        /// </summary>
        /// <param name="mediaSession"></param>
        /// <param name="logger"></param>
        /// <param name="callId"></param>
        /// <param name="settings"></param>
        /// <param name="msiToVideoSocketIdMapping"></param>
        /// <param name="msiToVBSSSocketIdMapping"></param>
        /// <param name="msiToVideoSerialNumMapping"></param>
        /// <param name="msiToVBSSSerialNumMapping"></param>
        /// <exception cref="InvalidOperationException">Throws when no audio socket is passed in.</exception>
        public BotMediaStream(ILocalMediaSession mediaSession, IGraphLogger logger, string callId, AzureSettings settings, ConcurrentDictionary<uint, uint> msiToVideoSocketIdMapping, ConcurrentDictionary<uint, uint> msiToVBSSSocketIdMapping, ConcurrentDictionary<uint, uint> msiToVideoSerialNumMapping, ConcurrentDictionary<uint, uint> msiToVBSSSerialNumMapping)
           : base(logger)
        {
            NLogHelper.Instance.Debug($"[BotMediaStream] initial");
            ArgumentVerifier.ThrowOnNullArgument(mediaSession, nameof(mediaSession));
            ArgumentVerifier.ThrowOnNullArgument(logger, nameof(logger));
            ArgumentVerifier.ThrowOnNullArgument(settings, nameof(settings));
            Participants = new List<IParticipant>();
            _CallId = callId;
            _MediaStream = new MediaStream(
               settings,
               logger,
               mediaSession.MediaSessionId.ToString(),
               callId,
               msiToVideoSocketIdMapping,
               msiToVBSSSocketIdMapping,
               msiToVideoSerialNumMapping,
               msiToVBSSSerialNumMapping);
            _MediaSession = mediaSession;
            // Subscribe to the audio media.
            _AudioSocket = _MediaSession.AudioSocket;
            if (_AudioSocket == null)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream] A mediaSession needs to have at least an audioSocket");
                throw new InvalidOperationException("A mediaSession needs to have at least an audioSocket");
            }
            NLogHelper.Instance.Debug($"[BotMediaStream] audioSocket += OnAudioMediaReceived");
            _AudioSocket.AudioMediaReceived += OnAudioMediaReceived;
            // Subscribe to the video media.
            _VideoSockets = _MediaSession.VideoSockets?.ToList();
            if (_VideoSockets?.Any() == true)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream] videoSockets += OnVideoMediaReceived");
                _VideoSockets.ForEach(videoSocket =>
                {
                    videoSocket.VideoMediaReceived += OnVideoMediaReceived;
                });
            }
            // Subscribe to the VBSS media.
            _VBSSSocket = _MediaSession.VbssSocket;
            if (_VBSSSocket != null)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream] VbssSocket += OnVbssMediaReceived");
                _VBSSSocket.VideoMediaReceived += OnVbssMediaReceived;
            }
        }

        /// <summary>
        /// Gets the audio quality of experience data.
        /// </summary>
        /// <returns>SerializableAudioQualityOfExperienceData.</returns>
        public SerializableAudioQualityOfExperienceData GetAudioQualityOfExperienceData()
        {
            AudioQualityOfExperienceData = new SerializableAudioQualityOfExperienceData(
                _CallId, _AudioSocket.GetQualityOfExperienceData());
            return AudioQualityOfExperienceData;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task StopVideo()
        {
            await _MediaStream.VideoEnd();
            NLogHelper.Instance.Debug($"[BotMediaStream] StopVideo");
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task StopVBSS()
        {
            await _MediaStream.VBSSEnd();
            NLogHelper.Instance.Debug($"[BotMediaStream] StopVBSS");
        }

        /// <summary>
        /// Stop media occurs when the call stops recording
        /// </summary>
        public async Task StopMedia()
        {
            await _MediaStream.AudioEnd();
            await _MediaStream.VideoEnd();
            await _MediaStream.VBSSEnd();
            NLogHelper.Instance.Debug($"[BotMediaStream] Call stopped recording");
        }

        /// <summary>
        /// Subscription for video and vbss.
        /// </summary>
        /// <param name="mediaType">vbss or video.</param>
        /// <param name="mediaSourceId">The video source Id.(msi)</param>
        /// <param name="videoResolution">The preferred video resolution.</param>
        /// <param name="socketId">Socket id requesting the video. For vbss it is always 0.</param>
        public void Subscribe(MediaType mediaType, uint mediaSourceId, VideoResolution videoResolution, uint socketId = 0)
        {
            try
            {
                ValidateSubscriptionMediaType(mediaType);
                if (mediaType == MediaType.Vbss)
                {
                    if (_VBSSSocket == null)
                    {
                        GraphLogger.Warn($"[BotMediaStream] vbss socket not initialized");
                        NLogHelper.Instance.Debug($"[BotMediaStream] vbss socket not initialized");
                    }
                    else
                    {
                        _VBSSSocket.Subscribe(videoResolution, mediaSourceId);
                        NLogHelper.Instance.Debug($"[BotMediaStream] Subscribing to the VBSS msi: {mediaSourceId} on socket: {socketId} with the preferred resolution: {videoResolution} and mediaType: {mediaType}");
                    }
                }
                else if (mediaType == MediaType.Video)
                {
                    if (_VideoSockets == null)
                    {
                        GraphLogger.Warn($"[BotMediaStream] video sockets were not created");
                        NLogHelper.Instance.Debug($"[BotMediaStream] video sockets were not created");
                    }
                    else
                    {
                        _VideoSockets[(int)socketId].Subscribe(videoResolution, mediaSourceId);
                        NLogHelper.Instance.Debug($"[BotMediaStream] Subscribing to the Video msi: {mediaSourceId} on socket: {socketId} with the preferred resolution: {videoResolution} and mediaType: {mediaType}");
                    }
                }
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream Error] Video Subscription failed socket: {socketId} msi: {mediaSourceId} Msg: {ex.Message}");
            }
        }

        /// <summary>
        /// Unsubscribe to video.
        /// </summary>
        /// <param name="mediaType">vbss or video.</param>
        /// <param name="socketId">Socket id. For vbss it is always 0.</param>
        public void Unsubscribe(MediaType mediaType, uint socketId = 0)
        {
            try
            {
                ValidateSubscriptionMediaType(mediaType);
                NLogHelper.Instance.Debug($"[BotMediaStream] Unsubscribing to video for the socket: {socketId} and mediaType: {mediaType}");
                if (mediaType == MediaType.Vbss)
                {
                    _VBSSSocket?.Unsubscribe();
                }
                else if (mediaType == MediaType.Video)
                {
                    _VideoSockets[(int)socketId]?.Unsubscribe();
                }
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream Error] Unsubscribing to video failed socket: {socketId}  Msg: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // Event Dispose of the bot media stream object
            base.Dispose(disposing);
            _AudioSocket.AudioMediaReceived -= OnAudioMediaReceived;
            if (_VideoSockets?.Any() == true)
            {
                _VideoSockets.ForEach(videoSocket => videoSocket.VideoMediaReceived -= OnVideoMediaReceived);
            }
            // Unsubscribe to the VBSS media.
            if (_VBSSSocket != null)
            {
                _MediaSession.VbssSocket.VideoMediaReceived -= OnVbssMediaReceived;
            }
        }

        /// <summary>
        /// Ensure media type is video or VBSS.
        /// </summary>
        /// <param name="mediaType">Media type to validate.</param>
        private void ValidateSubscriptionMediaType(MediaType mediaType)
        {
            if (mediaType != MediaType.Vbss && mediaType != MediaType.Video)
            {
                throw new ArgumentOutOfRangeException($"Invalid mediaType: {mediaType}");
            }
        }

        /// <summary>
        /// Receive audio from subscribed participant.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The audio media received arguments.
        /// </param>
        private async void OnAudioMediaReceived(object sender, AudioMediaReceivedEventArgs e)
        {
            try
            {
                await _MediaStream.AppendAudioBuffer(e.Buffer, Participants);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream Error] OnAudioMediaReceived Msg: {ex.Message}");
            }
            finally
            {
                e.Buffer.Dispose();
            }
        }

        /// <summary>
        /// Receive video from subscribed participant.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The video media received arguments.
        /// </param>
        private async void OnVideoMediaReceived(object sender, VideoMediaReceivedEventArgs e)
        {
            try
            {
                await _MediaStream.AppendVideoBuffer(e.Buffer, Participants);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream Error] OnVideoMediaReceived Msg: {ex.Message}");
            }
            finally
            {
                e.Buffer.Dispose();
            }
        }

        /// <summary>
        /// Receive vbss from subscribed participant.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The video media received arguments.
        /// </param>
        private async void OnVbssMediaReceived(object sender, VideoMediaReceivedEventArgs e)
        {
            try
            {
                await _MediaStream.AppendVBSSBuffer(e.Buffer, Participants);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[BotMediaStream Error] OnVbssMediaReceived Msg: {ex.Message}");
            }
            finally
            {
                e.Buffer.Dispose();
            }
        }

        /// <summary>
        /// If the application has configured the VideoSocket to receive media,
        /// this event is raised to inform the application when it is ready to receive media.
        /// When the status is active the application can subscribe to a video source, when inactive video subscription won't be allowed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoReceiveStatusChanged(object sender, VideoReceiveStatusChangedEventArgs e)
        {
            NLogHelper.Instance.Debug($"[BotMediaStream] OnVideoReceiveStatusChanged MediaReceiveStatus: {e.MediaReceiveStatus}");
            if (e.MediaReceiveStatus == MediaReceiveStatus.Active)
            {
                var videoSocket = (IVideoSocket)sender;
                videoSocket.SetReceiveBandwidthLimit(550000);
            }
        }
    }
}