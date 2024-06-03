using CommonTools.Authentication;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Common.Telemetry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace GraphApiTools
{
    /// <summary>
    /// GraphApiHelper
    /// </summary>
    public class GraphApiHelper
    {
        private GraphServiceClient _GraphClient { get; set; }
        private string _TenantId { get; set; }
        private string _ClientId { get; set; }
        private string _ClientSecret { get; set; }

        private const int _DefaultChunkSize = 320 * 1024 * 10;

        /// <summary>
        ///
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="clientName">Aad App Name</param>
        /// <param name="clientId">Aad App Id</param>
        /// <param name="clientSecret">Aad App Secret</param>
        public GraphApiHelper(string tenantId, string clientName, string clientId, string clientSecret)
        {
            _TenantId = tenantId;
            _ClientId = clientId;
            _ClientSecret = clientSecret;
            IGraphLogger logger = new GraphLogger("GraphApiHelper", redirectToTrace: true);
            var authProvider = new AuthenticationProvider(clientName, _ClientId, _ClientSecret, logger);
            var auth = new AuthenticationWrapper(authProvider, _TenantId);
            _GraphClient = new GraphServiceClient(auth);
        }

        #region User

        /// <summary>
        ///
        /// </summary>
        /// <param name="objectId">or userPrincipalName</param>
        /// <returns></returns>
        public User GetUser(string objectId)
        {
            return _GraphClient.Users[objectId]
                .Request()
                //.Select(u => new
                //{
                //    u.Id,
                //    u.UserPrincipalName,
                //    u.DisplayName,
                //    u.Mail
                //})
                .GetAsync()
                .Result;
        }

        #endregion User

        #region Group

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public Group GetGroupById(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return null;
            else
            {
                return _GraphClient.Groups[groupId]
                .Request()
                .GetAsync()
                .Result;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupDisplayName"></param>
        /// <returns></returns>
        public Group GetGroup(string groupDisplayName)
        {
            if (string.IsNullOrWhiteSpace(groupDisplayName))
                return null;
            else
            {
                var groups = _GraphClient.Groups
                .Request()
                .Header("ConsistencyLevel", "eventual")
                .Filter($"displayName eq '{groupDisplayName}'")
                //.Filter($"startswith(displayName,'{groupDisplayName}')")
                //.Select("id,displayName")
                .GetAsync()
                .Result;
                return groups.CurrentPage.FirstOrDefault();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public IEnumerable<DirectoryObject> GetGroupMember(string groupId)
        {
            var members = _GraphClient.Groups[groupId]
                .Members
                .Request()
                //.Header("ConsistencyLevel", "eventual")
                //.Filter($"displayName eq '{userDisplayName}'")
                //.Filter($"startswith(displayName,'{groupDisplayName}')")
                //.Select("id,mail,displayName")
                .GetAsync()
                .Result;
            return members.CurrentPage;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="userObjectId"></param>
        /// <returns></returns>
        public bool AddGroupMember(string groupId, string userObjectId)
        {
            var directoryObject = new DirectoryObject { Id = userObjectId };
            _GraphClient.Groups[groupId]
                 .Members
                 .References
                 .Request()
                 .AddAsync(directoryObject)
                 .Wait();
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="userObjectId"></param>
        /// <returns></returns>
        public bool RemoveGroupMember(string groupId, string userObjectId)
        {
            _GraphClient.Groups[groupId]
                .Members[userObjectId]
                .Reference
                .Request()
                .DeleteAsync()
                .Wait();
            return true;
        }

        #endregion Group

        #region Meeting

        /// <summary>
        ///
        /// </summary>
        /// <param name="meetingId"></param>
        /// <param name="organizerObjectId"></param>
        /// <returns></returns>
        public OnlineMeeting GetMeeting(string meetingId, string organizerObjectId)
        {
            return _GraphClient.Users[organizerObjectId]
                .OnlineMeetings[meetingId]
                .Request()
                .GetAsync()
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="videoTeleconferenceId"></param>
        /// <returns></returns>
        public IEnumerable<OnlineMeeting> GetMeetingByVideoTeleconferenceId(string videoTeleconferenceId)
        {
            return _GraphClient.Communications
                .OnlineMeetings
                .Request()
                .Filter($"VideoTeleconferenceId eq '{videoTeleconferenceId}'")
                .GetAsync()
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="joinWebUrl"></param>
        /// <returns></returns>
        public IEnumerable<OnlineMeeting> GetMeetingByJoinWebUrl(string joinWebUrl)
        {
            return _GraphClient.Communications
                .OnlineMeetings
                .Request()
                .Filter($"JoinWebUrl eq '{joinWebUrl}'")
                .GetAsync()
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="meetingModel"></param>
        /// <param name="organizerObjectId"></param>
        /// <param name="channelId"></param>
        /// <param name="isTestMeeting"></param>
        /// <returns></returns>
        public OnlineMeeting CreateMeeting(OnlineMeetingRequestModel meetingModel, string organizerObjectId, string channelId, bool isTestMeeting = false)
        {
            var meeting = new OnlineMeeting
            {
                Subject = meetingModel.Subject,
                StartDateTime = meetingModel.StartDateTime,
                EndDateTime = meetingModel.EndDateTime,
                AllowedPresenters = OnlineMeetingPresenters.Everyone,
                LobbyBypassSettings = new LobbyBypassSettings
                {
                    IsDialInBypassEnabled = true,
                    Scope = LobbyBypassScope.Organization
                },
                Participants = new MeetingParticipants()
                {
                    Organizer = new MeetingParticipantInfo()
                    {
                        Identity = new IdentitySet()
                        {
                            User = new Identity()
                            {
                                Id = organizerObjectId
                            }
                        }
                    },
                }
            };
            if (!string.IsNullOrWhiteSpace(channelId) && !isTestMeeting)
            {
                meeting.ChatInfo = new ChatInfo
                {
                    ThreadId = channelId
                };
            }
            return _GraphClient.Users[organizerObjectId]
                .OnlineMeetings
                .Request()
                .AddAsync(meeting)
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="meetingId"></param>
        /// <param name="organizerObjectId"></param>
        /// <returns></returns>
        public OnlineMeeting UpdateMeetingLobbyBypassSettings(string meetingId, string organizerObjectId)
        {
            var meetingInfo = GetMeeting(meetingId, organizerObjectId);
            if (meetingInfo != null)
            {
                var meeting = new OnlineMeeting
                {
                    LobbyBypassSettings = new LobbyBypassSettings
                    {
                        IsDialInBypassEnabled = true,
                        Scope = LobbyBypassScope.Organization
                    }
                };
                return _GraphClient.Users[organizerObjectId]
                    .OnlineMeetings[meetingId]
                    .Request()
                    .UpdateAsync(meeting)
                    .Result;
            }
            else
                return null;
        }

        #endregion Meeting

        #region Calls

        /// <summary>
        /// Create call enables your bot to create a new outgoing peer-to-peer or group call,
        /// or join an existing meeting
        /// </summary>
        /// <returns></returns>
        public Call CreateCall(string joinWebUrl)
        {
            MeetingInfo meetingInfo;
            ChatInfo chatInfo;
            (chatInfo, meetingInfo) = JoinInfoHelper.ParseJoinURL(joinWebUrl);
            var call = new Call
            {
                TenantId = _TenantId,
                ChatInfo = chatInfo,
                MeetingInfo = meetingInfo,
                RequestedModalities = new List<Modality>()
                    {
                        Modality.Audio
                    },
                MediaConfig = new ServiceHostedMediaConfig
                {
                }
            };
            return _GraphClient.Communications
                .Calls
                .Request()
                .AddAsync(call)
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public Call GetCall(string callId)
        {
            return _GraphClient.Communications
                .Calls[callId]
                .Request()
                .GetAsync()
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public bool DeleteCall(string callId)
        {
            _GraphClient.Communications
                .Calls[callId]
                .Request()
                .DeleteAsync()
                .Wait();
            return true;
        }

        /// <summary>
        /// Retrieve a list of participant objects in the call.
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public IEnumerable<Participant> GetParticipants(string callId)
        {
            return _GraphClient.Communications
                .Calls[callId]
                .Participants
                .Request()
                .GetAsync()
                .Result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public async Task<UpdateRecordingStatusOperation> StartRecording(string callId)
        {
            return await UpdateRecordingStatus(callId, RecordingStatus.Recording);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callId"></param>
        /// <returns></returns>
        public async Task<UpdateRecordingStatusOperation> StopRecording(string callId)
        {
            return await UpdateRecordingStatus(callId, RecordingStatus.NotRecording);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callId"></param>
        /// <param name="recordingStatus">RecordingStatus.NotRecording | RecordingStatus.Recording | RecordingStatus.Failed</param>
        /// <returns></returns>
        public async Task<UpdateRecordingStatusOperation> UpdateRecordingStatus(string callId, RecordingStatus recordingStatus)
        {
            var clientContext = Guid.NewGuid().ToString();
            return await _GraphClient.Communications
                .Calls[callId]
                .UpdateRecordingStatus(recordingStatus, clientContext)
                .Request()
                .PostAsync();
        }

        #endregion Calls

        #region Sites

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteRelativePath"></param>
        /// <param name="hostname"></param>
        /// <returns></returns>
        public Site GetSite(string siteRelativePath, string hostname)
        {
            var site = _GraphClient.Sites
                .GetByPath($"/sites/{siteRelativePath}", hostname)
                .Request()
                .GetAsync()
                .Result;
            return site;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="userObjectID"></param>
        /// <returns></returns>
        public List<Site> GetFollowedSites(string userObjectID)
        {
            var followedSites = _GraphClient.Users[userObjectID]
                .FollowedSites
                .Request()
                .GetAsync()
                .Result;
            return followedSites.CurrentPage.ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <returns></returns>
        public List<Site> GetSubSites(string siteID)
        {
            var sites = _GraphClient.Sites[siteID]
                .Sites
                .Request()
                .GetAsync()
                .Result;
            return sites.CurrentPage.ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <returns></returns>
        public List<List> GetLists(string siteID)
        {
            var lists = _GraphClient.Sites[siteID]
                .Lists
                .Request()
                .GetAsync()
                .Result;
            return lists.CurrentPage.ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="listID"></param>
        /// <returns></returns>
        public List<ListItem> GetListItems(string siteID, string listID)
        {
            //var queryOptions = new List<QueryOption>()
            //{
            //    new QueryOption("expand", "fields(select=Name,Color,Quantity)")
            //};
            var items = _GraphClient.Sites[siteID]
                .Lists[listID]
                .Items
                //.Request(queryOptions)
                .Request()
                .GetAsync()
                .Result;
            return items.CurrentPage.ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="listID"></param>
        /// <param name="listItemID"></param>
        /// <returns></returns>
        public ListItem GetListItem(string siteID, string listID, string listItemID)
        {
            //var queryOptions = new List<QueryOption>()
            //{
            //    new QueryOption("expand", "fields")
            //};
            var listItem = _GraphClient.Sites[siteID]
                .Lists[listID]
                .Items[listItemID]
                //.Request(queryOptions)
                .Request()
                .GetAsync()
                .Result;
            return listItem;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="listID"></param>
        /// <returns></returns>
        public Drive GetDrive(string siteID, string listID)
        {
            var drive = _GraphClient.Sites[siteID]
                .Lists[listID]
                .Drive
                .Request()
                .GetAsync()
                .Result;
            return drive;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="listID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public DriveItem GetDriveItem(string siteID, string listID, string itemID)
        {
            var driveItem = _GraphClient.Sites[siteID]
                .Lists[listID]
                .Items[itemID]
                .DriveItem
                .Request()
                .GetAsync()
                .Result;
            return driveItem;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="itemID"></param>
        public List<DriveItem> GetChildrenItem(string siteID, string itemID)
        {
            var children = _GraphClient.Sites[siteID]
                .Drive
                .Items[itemID]
                .Children
                .Request()
                .GetAsync()
                .Result;
            return children.CurrentPage.ToList();
        }

        /// <summary>
        ///
        /// </summary>
        public void DeleteItemByDrive(string driveId, string itemId)
        {
            _GraphClient.Drives[driveId]
                .Items[itemId]
                .Request()
                .DeleteAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///
        /// </summary>
        public void DeleteItemBySite(string siteId, string itemId)
        {
            _GraphClient.Sites[siteId]
                .Drive
                .Items[itemId]
                .Request()
                .DeleteAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="driveId"></param>
        /// <param name="itemId"></param>
        /// <param name="filePath"></param>
        public void DownloadLargeFile(string driveId, string itemId, string filePath)
        {
            var driveItem = _GraphClient.Drives[driveId].Items[itemId].Request().GetAsync().Result;
            driveItem.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var downloadUrl);
            var size = (int)driveItem.Size;
            int offset = 0;

            while (offset < size)
            {
                int chunkSize = Math.Min(size - offset, _DefaultChunkSize);
                var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl.ToString());
                req.Headers.Range = new RangeHeaderValue(offset, chunkSize + offset - 1);
                var response = _GraphClient.HttpProvider.SendAsync(req).Result;
                byte[] buffer;
                using (var rs = response.Content.ReadAsStreamAsync().Result)
                {
                    buffer = new byte[chunkSize];
                    rs.Read(buffer, 0, buffer.Length);
                }
                using (var fs = new FileStream(filePath, FileMode.Append))
                {
                    fs.Write(buffer, 0, buffer.Length);
                    fs.Flush();
                }
                offset += _DefaultChunkSize;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public Stream DownloadFile(string siteID, string itemID)
        {
            var stream = _GraphClient.Sites[siteID]
                .Drive
                .Items[itemID]
                .Content
                .Request()
                .GetAsync()
                .Result;
            return stream;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="siteID"></param>
        /// <param name="listID"></param>
        /// <param name="relativePath"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public DriveItem UploadSmallFile(string siteID, string listID, string relativePath, string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var targetFolder = _GraphClient.Sites[siteID]
                       .Lists[listID]
                       .Drive
                       .Root;
                var uploadedItem = targetFolder
                       .ItemWithPath($"{relativePath}/{Path.GetFileName(filePath)}")
                       .Content
                       .Request()
                       .PutAsync<DriveItem>(fs)
                       .Result;
                return uploadedItem;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="hostName"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public UploadResult<DriveItem> UploadLargeFile(string groupName, string hostName, string relativePath, string filePath)
        {
            var site = GetSite(groupName.Replace(" ", ""), hostName);
            var lists = GetLists(site.Id);
            var listDoc = lists.FirstOrDefault(i => i.DisplayName == "Documents");
            var uploadResult = UploadFile(site.Id, listDoc.Id, relativePath, filePath);
            return uploadResult;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public UploadResult<DriveItem> UploadFile(string siteID, string listID, string relativePath, string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Create upload session
                var targetFolder = _GraphClient.Sites[siteID]
                   .Lists[listID]
                   .Drive
                   .Root;
                // Create the upload session
                // itemPath does not need to be a path to an existing item
                var uploadSession = targetFolder
                   .ItemWithPath($"{relativePath}/{Path.GetFileName(filePath)}")
                   .CreateUploadSession()
                   .Request()
                   .PostAsync()
                   .Result;
                // Max slice size must be a multiple of 320 KiB
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, fs, _DefaultChunkSize);
                var totalLength = fs.Length;
                // Create a callback that is invoked after each slice is uploaded
                IProgress<long> progress = new Progress<long>(prog =>
                {
                    Console.WriteLine($"Uploaded {prog} bytes of {totalLength} bytes");
                });
                // Upload the file
                var uploadResult = fileUploadTask.UploadAsync(progress).Result;
                return uploadResult;
            }
        }

        #endregion Sites
    }
}