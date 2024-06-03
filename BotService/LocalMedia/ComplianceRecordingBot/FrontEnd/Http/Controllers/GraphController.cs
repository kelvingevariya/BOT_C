using AzureStorageTools;
using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.Models;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using GraphApiTools;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Core.Serialization;
using Newtonsoft.Json;
using PowerShellTools;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace ComplianceRecordingBot.FrontEnd.Http
{
    /// <summary>
    ///
    /// </summary>
    public class GraphController : ApiController
    {
        /// <summary>
        /// Gets the logger instance.
        /// </summary>
        private IGraphLogger _logger => Bot.Bot.Instance.Logger;

        private AzureSettings _settings => Bot.Bot.Instance.AzureSettings;

        /// <summary>
        ///
        /// </summary>
        /// <param name="meetingModel"></param>
        /// <returns></returns>
        [HttpPost]
        [Route(HttpRouteConstants.CreateMeeting)]
        public HttpResponseMessage CreateMeeting(OnlineMeetingRequestModel meetingModel)
        {
            this._logger.Info("CreateMeeting");
            NLogHelper.Instance.Debug($"[GraphController] CreateMeeting start");
            var result = new OnlineMeetingResponseModel() { IsSuccess = false };
            Stopwatch full = new Stopwatch();
            full.Start();
            try
            {
                var graphHelper = new GraphApiHelper(_settings.TenantId, _settings.BotName, _settings.AadAppId, _settings.AadAppSecret);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var user = graphHelper.GetUser(meetingModel.AgentUPN);
                sw.Stop();
                NLogHelper.Instance.Debug($"[GraphController] GetUser Seconds: {sw.ElapsedMilliseconds / 1000}");
                sw.Reset();
                if (user != null)
                {
                    NLogHelper.Instance.Debug($"[GraphController] CreateMeeting get user Id: {user.Id}  UPN: {user.UserPrincipalName}");
                    var blobHelper = new BlobHelper(_settings.BlobServiceEndpoint, _settings.StorageAccountName, _settings.StorageAccountKey);
                    sw.Start();
                    result = CheckStorageAndPolicy(meetingModel.AgentUPN, user.Id, blobHelper);
                    sw.Stop();
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy Seconds: {sw.ElapsedMilliseconds / 1000}");
                    sw.Reset();
                    if (result.IsSuccess)
                    {
                        sw.Start();
                        result = CreateMeeting(meetingModel, graphHelper);
                        sw.Stop();
                        NLogHelper.Instance.Debug($"[GraphController] CreateMeeting Seconds: {sw.ElapsedMilliseconds / 1000}");
                        sw.Reset();
                        if (result.IsSuccess)
                        {
                            sw.Start();
                            BlobModel blobModel = null;
                            var json = blobHelper.DownloadBlob(BotConstants.AzureStorageContainerName, user.Id);
                            if (string.IsNullOrWhiteSpace(json))
                            {
                                blobModel = new BlobModel();
                                blobModel.AgentUPN = meetingModel.AgentUPN;
                                blobModel.UserObjectId = user.Id;
                                NLogHelper.Instance.Debug($"[GraphController] CreateMeeting blobModel not exists: {user.Id}");
                            }
                            else
                                blobModel = JsonConvert.DeserializeObject<BlobModel>(json);

                            blobModel.MeetingId = result.MeetingId;
                            blobModel.MeetingSubject = result.MeetingSubject;
                            var contents = JsonConvert.SerializeObject(blobModel);
                            NLogHelper.Instance.Debug($"[GraphController] CreateMeeting contents: {contents}");
                            blobHelper.UploadBlob(BotConstants.AzureStorageContainerName, user.Id, contents);
                            NLogHelper.Instance.Debug($"[GraphController] CreateMeeting UploadBlob.");
                            sw.Stop();
                            NLogHelper.Instance.Debug($"[GraphController] DownloadBlob & UploadBlob Seconds: {sw.ElapsedMilliseconds / 1000}");
                            sw.Reset();
                        }
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Can not get Agent UPN data.";
                    NLogHelper.Instance.Debug($"[GraphController] CreateMeeting Can not get Agent UPN data.");
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                NLogHelper.Instance.Debug($"[GraphController Error] CreateMeeting Msg: {ex.Message}");
            }
            var serializer = new CommsSerializer(pretty: true);
            var jsonResult = serializer.SerializeObject(result);
            NLogHelper.Instance.Debug($"[GraphController] Create Meeting Result: {jsonResult}");
            var response = this.Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(jsonResult, Encoding.UTF8, "application/json");
            full.Stop();
            NLogHelper.Instance.Debug($"[GraphController] Full CreateMeeting Seconds: {full.ElapsedMilliseconds / 1000}");
            full.Reset();
            return response;
        }

        private OnlineMeetingResponseModel CheckStorageAndPolicy(string agentUPN, string userObjectId, BlobHelper blobHelper)
        {
            var result = new OnlineMeetingResponseModel() { IsSuccess = false };
            try
            {
                BlobModel blobModel = null;
                var json = blobHelper.DownloadBlob(BotConstants.AzureStorageContainerName, userObjectId);
                if (string.IsNullOrWhiteSpace(json))
                {
                    blobModel = new BlobModel();
                    blobModel.IsAddPolicy = false;
                    blobModel.AgentUPN = agentUPN;
                    blobModel.UserObjectId = userObjectId;
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy Blob not Exists.");
                }
                else
                {
                    blobModel = JsonConvert.DeserializeObject<BlobModel>(json);
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy Blob: {json}");
                }
                if (blobModel.IsAddPolicy == false)
                {
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy GrantPolicy start.");
                    result = GrantPolicy(agentUPN);
                    blobModel.IsAddPolicy = result.IsSuccess;
                    var contents = JsonConvert.SerializeObject(blobModel);
                    blobHelper.UploadBlob(BotConstants.AzureStorageContainerName, userObjectId, contents);
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy UploadBlob: {contents}");
                }
                else
                {
                    result.IsSuccess = true;
                    NLogHelper.Instance.Debug($"[GraphController] CheckStorageAndPolicy IsAddPolicy.");
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                NLogHelper.Instance.Debug($"[GraphController Error] CheckStorageAndPolicy Msg: {ex.Message}");
            }
            return result;
        }

        private OnlineMeetingResponseModel GrantPolicy(string agentUPN)
        {
            var result = new OnlineMeetingResponseModel() { IsSuccess = false };
            try
            {
                var processStatus = ComplianceRecordingPolicyHelper.GrantPolicy(_settings.TeamsAdminAccount, _settings.TeamsAdminPwd, _settings.ComplianceRecordingPolicyName, agentUPN);
                if (processStatus.IsSuccess)
                {
                    result.IsSuccess = true;
                    NLogHelper.Instance.Debug($"[GraphController] GrantPolicy success.");
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Grant Policy fail: {processStatus.Msg}";
                    NLogHelper.Instance.Debug($"[GraphController] GrantPolicy fail: {processStatus.Msg}");
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                NLogHelper.Instance.Debug($"[GraphController Error] GrantPolicy Msg: {ex.Message}");
            }
            return result;
        }

        private OnlineMeetingResponseModel CreateMeeting(OnlineMeetingRequestModel meetingModel, GraphApiHelper graphHelper)
        {
            var result = new OnlineMeetingResponseModel() { IsSuccess = false };
            try
            {
                if (!string.IsNullOrWhiteSpace(meetingModel.MeetingId))
                {
                    var meeting = graphHelper.GetMeeting(meetingModel.MeetingId, _settings.OrganizerObjectId);
                    if (meeting != null)
                    {
                        result.IsSuccess = true;
                        result.JoinWebUrl = meeting.JoinWebUrl;
                        result.MeetingId = meeting.Id;
                        result.MeetingSubject = meeting.Subject;
                        NLogHelper.Instance.Debug($"[GraphController] Get Meeting success.");
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "Get Meeting fail.";
                        NLogHelper.Instance.Debug($"[GraphController] Get Meeting fail.");
                    }
                }
                else
                {
                    var meeting = graphHelper.CreateMeeting(meetingModel, _settings.OrganizerObjectId, _settings.ChannelId);
                    if (meeting != null)
                    {
                        result.IsSuccess = true;
                        result.JoinWebUrl = meeting.JoinWebUrl;
                        result.MeetingId = meeting.Id;
                        result.MeetingSubject = meeting.Subject;
                        NLogHelper.Instance.Debug($"[GraphController] Create Meeting success.");
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = "Create Meeting fail.";
                        NLogHelper.Instance.Debug($"[GraphController] Create Meeting fail.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                NLogHelper.Instance.Debug($"[GraphController Error] CreateMeeting Msg: {ex.Message}");
            }
            return result;
        }
    }
}