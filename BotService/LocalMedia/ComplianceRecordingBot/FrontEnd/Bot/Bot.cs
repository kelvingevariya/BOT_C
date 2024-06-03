using AzureStorageTools;
using CommonTools;
using CommonTools.Authentication;
using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.Contract;
using ComplianceRecordingBot.FrontEnd.Models;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using ComplianceRecordingBot.FrontEnd.Util;
using GraphApiTools;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Calls.Media;
using Microsoft.Graph.Communications.Client;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using Microsoft.Skype.Bots.Media;
using Newtonsoft.Json;
using PowerShellTools;
using RecordingMergeTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ComplianceRecordingBot.FrontEnd.Bot
{
    /// <summary>
    /// The core bot logic.
    /// </summary>
    public class Bot : IDisposable, IBotService
    {
        /// <summary>
        /// The settings
        /// </summary>
        public AzureSettings AzureSettings;

        /// <summary>
        /// Gets the instance of the bot.
        /// </summary>
        public static Bot Instance { get; } = new Bot();

        /// <summary>
        /// Gets the Graph Logger instance.
        /// </summary>
        public IGraphLogger Logger { get; private set; }

        /// <summary>
        /// Gets the sample log observer.
        /// </summary>
        public CommonObserver Observer { get; private set; }

        /// <summary>
        /// Gets the collection of call handlers.
        /// Key: Call ID
        /// </summary>
        public ConcurrentDictionary<string, CallHandler> CallHandlers { get; } = new ConcurrentDictionary<string, CallHandler>();

        public ConcurrentDictionary<string, List<RecordingFileInfo>> RecordingFileInfoList { get; } = new ConcurrentDictionary<string, List<RecordingFileInfo>>();

        public ConcurrentDictionary<string, BlobModel> CallMeetingMappingInfo { get; } = new ConcurrentDictionary<string, BlobModel>();

        /// <summary>
        /// Gets the entry point for stateful bot.
        /// </summary>
        public ICommunicationsClient Client { get; private set; }

        private static string _DefaultApproot { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Observer?.Dispose();
            Observer = null;
            Logger = null;
            Client?.Dispose();
            Client = null;
        }

        /// <summary>
        /// Initialize the instance.
        /// </summary>
        /// <param name="service">Service instance.</param>
        /// <param name="logger">Graph logger.</param>
        /// <param name="settings">The settings.</param>
        public void Initialize(Service service, IGraphLogger logger, AzureSettings settings)
        {
            Validator.IsNull(Logger, "Multiple initializations are not allowed.");
            Logger = logger;
            Observer = new CommonObserver(logger);
            AzureSettings = settings;
            Logger.Info("[Bot] Initialize");
            _DefaultApproot = AppDomain.CurrentDomain.BaseDirectory;
            NLogHelper.Instance.Debug($"[Bot] Initialize _DefaultApproot: {_DefaultApproot}");
            var name = GetType().Assembly.GetName().Name;
            var builder = new CommunicationsClientBuilder(
                name,
                service.Configuration.AadAppId,
                Logger);
            var authProvider = new AuthenticationProvider(
                service.Configuration.BotName,
                service.Configuration.AadAppId,
                service.Configuration.AadAppSecret,
                Logger);
            var auth = new AuthenticationWrapper(authProvider, service.Configuration.AuthWrapperTenantId);
            builder.SetAuthenticationProvider(auth);
            builder.SetNotificationUrl(service.Configuration.CallControlBaseUrl);
            try
            {
                builder.SetMediaPlatformSettings(service.Configuration.MediaPlatformSettings);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[Bot Error] SetMediaPlatformSettings Msg: {ex.Message}");
                if (ex.InnerException != null)
                {
                    NLogHelper.Instance.Debug($"[Bot Error] InnerException: {ex.InnerException.Message}");
                }
            }
            builder.SetServiceBaseUrl(service.Configuration.PlaceCallEndpointUrl);
            Client = builder.Build();
            NLogHelper.Instance.Debug("[Bot] += CallsOnIncoming");
            Client.Calls().OnIncoming += CallsOnIncoming;
            NLogHelper.Instance.Debug("[Bot] += CallsOnUpdated");
            Client.Calls().OnUpdated += CallsOnUpdated;
        }

        /// <summary>
        /// Joins the call asynchronously.
        /// </summary>
        /// <param name="joinCallBody">The join call body.</param>
        /// <returns>The <see cref="ICall" /> that was requested to join.</returns>
        public async Task<ICall> JoinCallAsync(JoinCallBody joinCallBody)
        {
            Logger.Info($"[Bot] JoinCallAsync JoinURL: {joinCallBody.JoinURL}");
            NLogHelper.Instance.Debug($"[Bot] JoinCallAsync JoinURL: {joinCallBody.JoinURL}");
            // A tracking id for logging purposes. Helps identify this call in logs.
            var scenarioId = Guid.NewGuid();
            Logger.Info($"[Bot] ScenarioId: {scenarioId}");
            NLogHelper.Instance.Debug($"[Bot] ScenarioId: {scenarioId}");
            var (chatInfo, meetingInfo) = JoinInfo.ParseJoinURL(joinCallBody.JoinURL);
            var tenantId = (meetingInfo as OrganizerMeetingInfo).Organizer.GetPrimaryIdentity().GetTenantId();
            var mediaSession = CreateLocalMediaSession();
            var joinParams = new JoinMeetingParameters(chatInfo, meetingInfo, mediaSession)
            {
                TenantId = tenantId,
            };
            if (!string.IsNullOrWhiteSpace(joinCallBody.DisplayName))
            {
                Logger.Info($"[Bot] DisplayName: {joinCallBody.DisplayName}");
                NLogHelper.Instance.Debug($"[Bot] DisplayName: {joinCallBody.DisplayName}");
                // Teams client does not allow changing of ones own display name.
                // If display name is specified, we join as anonymous (guest) user
                // with the specified display name.  This will put bot into lobby
                // unless lobby bypass is disabled.
                joinParams.GuestIdentity = new Identity
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = joinCallBody.DisplayName,
                };
            }
            var statefulCall = await Client.Calls().AddAsync(joinParams, scenarioId).ConfigureAwait(false);
            statefulCall.GraphLogger.Info($"[Bot] JoinCallAsync complete CallID: {statefulCall.Id}");
            NLogHelper.Instance.Debug($"[Bot] JoinCallAsync complete CallID: {statefulCall.Id}");
            return statefulCall;
        }

        /// <summary>
        /// End a particular call.
        /// </summary>
        /// <param name="callLegId">
        /// The call leg id.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task EndCallByCallLegIdAsync(string callLegId)
        {
            Logger.Info($"[Bot] EndCallByCallLegIdAsync callLegId: {callLegId}");
            NLogHelper.Instance.Debug($"[Bot] EndCallByCallLegIdAsync callLegId: {callLegId}");
            try
            {
                await GetHandlerOrThrow(callLegId).Call.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Manually remove the call from SDK state.
                // This will trigger the ICallCollection.OnUpdated event with the removed resource.
                Client.Calls().TryForceRemove(callLegId, out ICall call);
            }
        }

        /// <summary>
        /// Creates the local media session.
        /// </summary>
        /// <param name="mediaSessionId">
        /// The media session identifier.
        /// This should be a unique value for each call.
        /// </param>
        /// <returns>The <see cref="ILocalMediaSession"/>.</returns>
        private ILocalMediaSession CreateLocalMediaSession(Guid mediaSessionId = default(Guid))
        {
            var videoSocketSettings = new List<VideoSocketSettings>();
            // create the receive only sockets settings for the multiview support
            for (int i = 0; i < BotConstants.NumberOfMultiviewSockets; i++)
            {
                videoSocketSettings.Add(new VideoSocketSettings
                {
                    StreamDirections = StreamDirection.Recvonly,
                    ReceiveColorFormat = VideoColorFormat.H264,
                });
            }
            // Create the VBSS socket settings
            var vbssSocketSettings = new VideoSocketSettings
            {
                StreamDirections = StreamDirection.Recvonly,
                ReceiveColorFormat = VideoColorFormat.H264,
                MediaType = MediaType.Vbss,
                SupportedSendVideoFormats = new List<VideoFormat>
                {
                    // fps 1.875 is required for h264 in vbss scenario.
                    VideoFormat.H264_1920x1080_1_875Fps,
                },
            };
            // create media session object, this is needed to establish call connections
            var mediaSession = Client.CreateMediaSession(
                new AudioSocketSettings
                {
                    StreamDirections = StreamDirection.Recvonly,
                    SupportedAudioFormat = AudioFormat.Pcm16K,
                    ReceiveUnmixedMeetingAudio = true //get the extra buffers for the speakers
                },
                videoSocketSettings,
                vbssSocketSettings,
                mediaSessionId: mediaSessionId);
            return mediaSession;
        }

        /// <summary>
        /// Incoming call handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="CollectionEventArgs{TEntity}"/> instance containing the event data.</param>
        private void CallsOnIncoming(ICallCollection sender, CollectionEventArgs<ICall> args)
        {
            args.AddedResources.ForEach(call =>
            {
                try
                {
                    // Get the compliance recording parameters.
                    // The context associated with the incoming call.
                    IncomingContext incomingContext = call.Resource.IncomingContext;
                    // The RP participant.
                    string observedParticipantId = incomingContext.ObservedParticipantId;
                    // If the observed participant is a delegate.
                    IdentitySet onBehalfOfIdentity = incomingContext.OnBehalfOf;
                    // If a transfer occured, the transferor.
                    IdentitySet transferorIdentity = incomingContext.Transferor;
                    string countryCode = null;
                    EndpointType? endpointType = null;
                    // Note: this should always be true for CR calls.
                    if (observedParticipantId == incomingContext.SourceParticipantId)
                    {
                        // The dynamic location of the RP.
                        countryCode = call.Resource.Source.CountryCode;
                        // The type of endpoint being used.
                        endpointType = call.Resource.Source.EndpointType;
                    }
                    IMediaSession mediaSession = Guid.TryParse(call.Id, out Guid callId)
                        ? CreateLocalMediaSession(callId)
                        : CreateLocalMediaSession();
                    // Answer call
                    call?.AnswerAsync(mediaSession).ForgetAndLogExceptionAsync(
                        call.GraphLogger,
                        $"Answering call {call.Id} with scenario {call.ScenarioId}.");
                    Logger.Info($"[Bot] CallsOnIncoming Answering callID {call.Id} with ScenarioId {call.ScenarioId} ObservedParticipantId: {observedParticipantId}");
                    NLogHelper.Instance.Debug($"[Bot] CallsOnIncoming Answering callID {call.Id} with ScenarioId {call.ScenarioId} ObservedParticipantId: {observedParticipantId}");
                    if (!RecordingFileInfoList.ContainsKey(call.Id))
                    {
                        RecordingFileInfoList.TryAdd(call.Id, new List<RecordingFileInfo>());
                        NLogHelper.Instance.Debug($"[Bot] CallsOnIncoming RecordingFileInfoList TryAdd");
                    }
                }
                catch (Exception ex)
                {
                    NLogHelper.Instance.Debug($"[Bot Error] CallsOnIncoming Msg: {ex.Message}");

                    if (ex.InnerException != null)
                    {
                        NLogHelper.Instance.Debug($"[Bot Error] InnerException: {ex.InnerException.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Updated call handler.
        /// </summary>
        /// <param name="sender">The <see cref="ICallCollection"/> sender.</param>
        /// <param name="args">The <see cref="CollectionEventArgs{ICall}"/> instance containing the event data.</param>
        private void CallsOnUpdated(ICallCollection sender, CollectionEventArgs<ICall> args)
        {
            try
            {
                NLogHelper.Instance.Debug("[Bot] CallsOnUpdated start");
                foreach (var call in args.AddedResources)
                {
                    NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated AddedResources callID: {call.Id}");
                    var callHandler = new CallHandler(call, AzureSettings);
                    CallHandlers[call.Id] = callHandler;
                }
                foreach (var call in args.RemovedResources)
                {
                    NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated RemovedResources callID: {call.Id}");
                    if (CallHandlers.TryRemove(call.Id, out CallHandler handler))
                    {
                        handler.Dispose();
                    }
                    CallMeetingMappingInfo.TryGetValue(call.Id, out BlobModel blobModel);
                    var meetingId = string.Empty;
                    var meetingSubject = string.Empty;
                    if (blobModel != null && !string.IsNullOrWhiteSpace(blobModel.MeetingId))
                    {
                        meetingId = blobModel.MeetingId;
                        meetingSubject = blobModel.MeetingSubject;
                        NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated CallMeetingMappingInfo MeetingSubject: {meetingSubject} MeetingId: {meetingId}");
                    }
                    else
                    {
                        meetingId = call.Id;
                        NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated CallMeetingMappingInfo no blob data (no meeting info).");
                    }

                    if (blobModel != null && !string.IsNullOrWhiteSpace(blobModel.AgentUPN))
                        RemovePolicy(blobModel.AgentUPN);
                    else
                        NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated not RemovePolicy callID: {call.Id}");

                    if (blobModel != null && !string.IsNullOrWhiteSpace(blobModel.UserObjectId))
                    {
                        var blobHelper = new BlobHelper(AzureSettings.BlobServiceEndpoint, AzureSettings.StorageAccountName, AzureSettings.StorageAccountKey);
                        blobHelper.DeleteBlob(BotConstants.AzureStorageContainerName, blobModel.UserObjectId);
                        NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated DeleteBlob: {blobModel.UserObjectId}");
                    }
                    else
                        NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated not DeleteBlob callID: {call.Id}");

                    if (blobModel != null)
                    {
                        CallMeetingMappingInfo.TryRemove(blobModel.CallId, out BlobModel bm);
                    }
                    NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated Create File Start callID: {call.Id} MeetingSubject: {meetingSubject} MeetingId: {meetingId}");
                    var baseDirPath = Path.Combine(AzureSettings.DefaultOutputFolder, call.Id);
                    var audioDirPath = Path.Combine(baseDirPath, AzureSettings.AudioFolder);
                    var videoDirPath = Path.Combine(baseDirPath, AzureSettings.VideoFolder);
                    var vbssDirPath = Path.Combine(baseDirPath, AzureSettings.VBSSFolder);
                    var recordingFileInfoList = RecordingFileInfoList[call.Id];
                    if (!UtilityHelper.CheckFileGenerated(call.Id, recordingFileInfoList))
                    {
                        throw new FileNotFoundException("Recording Files Not Exists.");
                    }
                    var recordingFileInfoListFilePath = Path.Combine(baseDirPath, "recordingFileInfoList.json");
                    var json = JsonConvert.SerializeObject(recordingFileInfoList);
                    var result = CommonUtilities.WriteFile(recordingFileInfoListFilePath, json);
                    NLogHelper.Instance.Debug($"[Bot] CallsOnUpdated WriteFile result: {result} Path: {recordingFileInfoListFilePath}");
                    var zipFilePath = string.Empty;
                    var audioFile = recordingFileInfoList.FirstOrDefault(i => i.FileType == 0);
                    if (audioFile == null)
                        throw new NullReferenceException("Audio File Info is null");
                    else
                    {
                        if (string.IsNullOrWhiteSpace(meetingSubject))// call id or meeting id
                        {
                            zipFilePath = Path.Combine(baseDirPath, $"{meetingId}_{audioFile.RecordingStartTime.ToString("yyyyMMddHHmmss")}_{audioFile.RecordingEndTime.ToString("yyyyMMddHHmmss")}.zip");
                        }
                        else // meeting subject
                        {
                            zipFilePath = Path.Combine(baseDirPath, $"{meetingSubject}_{audioFile.RecordingStartTime.ToString("yyyyMMddHHmmss")}_{audioFile.RecordingEndTime.ToString("yyyyMMddHHmmss")}.zip");
                        }
                    }
                    var uploadFilePath = string.Empty;
                    if (GenerateZipFileByCall(call.Id, zipFilePath))
                        uploadFilePath = zipFilePath;
                    else
                        throw new Exception($"GenerateZipFile Fail CallID: {call.Id} zipFilePath: {zipFilePath}");

                    UploadFileToSharePoint(uploadFilePath, baseDirPath);
                    RecordingFileInfoList.TryRemove(call.Id, out List<RecordingFileInfo> rfi);
                    NLogHelper.Instance.Debug($"[Bot] RecordingFileInfoList Remove CallID: {call.Id}");
                }
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[Bot Error] CallsOnUpdated Msg: {ex.Message}");
                if (ex.InnerException != null)
                    NLogHelper.Instance.Debug($"[Bot Error] InnerException: {ex.InnerException.Message}");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="agentUPN"></param>
        private void RemovePolicy(string agentUPN)
        {
            var processStatus = ComplianceRecordingPolicyHelper.ClearPolicy(AzureSettings.TeamsAdminAccount, AzureSettings.TeamsAdminPwd, agentUPN);
            if (processStatus.IsSuccess)
                NLogHelper.Instance.Debug($"[Bot] RemovePolicy ClearPolicy success");
            else
                NLogHelper.Instance.Debug($"[Bot] RemovePolicy ClearPolicy fail: {processStatus.Msg}");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="uploadFilePath"></param>
        /// <param name="baseDirPath">D:\teams-recording-bot\</param>
        private void UploadFileToSharePoint(string uploadFilePath, string baseDirPath)
        {
            NLogHelper.Instance.Debug($"[Bot] UploadFileToSharePoint Start");
            var graphHelper = new GraphApiHelper(AzureSettings.TenantId, AzureSettings.BotName, AzureSettings.AadAppId, AzureSettings.AadAppSecret);
            var uploadResult = graphHelper.UploadLargeFile(AzureSettings.GroupName, AzureSettings.HostName, AzureSettings.RelativePath, uploadFilePath);
            if (uploadResult.UploadSucceeded)
            {
                NLogHelper.Instance.Debug($"[Bot] UploadFileToSharePoint Succeed ItemID: {uploadResult.ItemResponse.Id} WebUrl: {uploadResult.ItemResponse.WebUrl}");

                System.IO.Directory.Delete(baseDirPath, true);
                NLogHelper.Instance.Debug($"[Bot] OnMergeRunExited Directory Delete baseDirPath: {baseDirPath}");
            }
            else
                NLogHelper.Instance.Debug($"[Bot] UploadFileToSharePoint Fail uploadFilePath: {uploadFilePath}");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callID"></param>
        /// <param name="destFilePath"></param>
        /// <returns></returns>
        private bool GenerateZipFileByCall(string callID, string destFilePath)
        {
            //D:\teams-recording-bot\{Call ID}
            var baseDirPath = Path.Combine(AzureSettings.DefaultOutputFolder, callID);
            bool result = false;
            List<string> allFilePathList = new List<string>();
            try
            {
                string[] files = System.IO.Directory.GetFiles(baseDirPath);
                foreach (var file in files)
                {
                    allFilePathList.Add(file);
                }
                string[] subDirs = System.IO.Directory.GetDirectories(baseDirPath);
                foreach (var dir in subDirs)
                {
                    string[] subFiles = System.IO.Directory.GetFiles(dir);
                    foreach (var file in subFiles)
                    {
                        allFilePathList.Add(file);
                    }
                }
                result = ZipFileHelper.CreateZipFile(destFilePath, allFilePathList);
            }
            catch (Exception ex)
            {
                result = false;
                NLogHelper.Instance.Debug($"[Bot Error] GenerateZipFileByCall Msg: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// The get handler or throw.
        /// </summary>
        /// <param name="callLegId">
        /// The call leg id.
        /// </param>
        /// <returns>
        /// The <see cref="CallHandler"/>.
        /// </returns>
        /// <exception cref="ObjectNotFoundException">
        /// Throws an exception if handler is not found.
        /// </exception>
        private CallHandler GetHandlerOrThrow(string callLegId)
        {
            if (!CallHandlers.TryGetValue(callLegId, out CallHandler handler))
            {
                NLogHelper.Instance.Debug($"[Bot] call ({callLegId}) not found");
                throw new ObjectNotFoundException($"[Bot] call ({callLegId}) not found");
            }
            return handler;
        }
    }
}