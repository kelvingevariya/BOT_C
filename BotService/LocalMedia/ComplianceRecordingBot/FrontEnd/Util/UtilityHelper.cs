namespace ComplianceRecordingBot.FrontEnd.Util
{
    using CommonTools.Logging;
    using Microsoft.Skype.Bots.Media;
    using RecordingMergeTools;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// The utility class.
    /// </summary>
    public static class UtilityHelper
    {
        /// <summary>
        /// Helper function get id.
        /// </summary>
        /// <param name="videoFormat">Video format.</param>
        /// <returns>The <see cref="int"/> of the video format.</returns>
        public static int GetId(this VideoFormat videoFormat)
        {
            return $"{videoFormat.VideoColorFormat}{videoFormat.Width}{videoFormat.Height}".GetHashCode();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="callID"></param>
        /// <param name="recordingFileInfoList"></param>
        /// <returns></returns>
        public static bool CheckFileGenerated(string callID, List<RecordingFileInfo> recordingFileInfoList)
        {
            NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated start");
            var result = false;
            var audioFile = recordingFileInfoList.FirstOrDefault(i => i.FileType == 0);
            if (audioFile == null)
            {
                NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated audioFile not found callID: {callID}");
                return result;
            }
            var settings = Bot.Bot.Instance.AzureSettings;
            var audioDirPath = Path.Combine(settings.DefaultOutputFolder, callID, settings.AudioFolder);
            var videoDirPath = Path.Combine(settings.DefaultOutputFolder, callID, settings.VideoFolder);
            var vbssDirPath = Path.Combine(settings.DefaultOutputFolder, callID, settings.VBSSFolder);
            var videoFiles = recordingFileInfoList.Where(i => i.FileType == 1);
            var vbssFiles = recordingFileInfoList.Where(i => i.FileType == 2);
            var waitSeconds = TimeSpan.FromSeconds(settings.WaitForCheckFileSeconds);
            var deadline = DateTime.Now.Add(waitSeconds);
            var timeOut = false;
            do
            {
                //Audio
                var aPath = Path.Combine(audioDirPath, audioFile.FileName);
                var aPass = (File.Exists(aPath) && audioFile.IsProcessDone);
                NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated audio File Process Done: {aPass}");
                //Video
                var vPass = true;
                if (videoFiles.Count() > 0)
                {
                    foreach (var vFile in videoFiles)
                    {
                        var vPath = Path.Combine(videoDirPath, vFile.FileName);
                        if (!File.Exists(vPath) || !vFile.IsProcessDone)
                        {
                            vPass = false;
                            NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated video File not Done: {vPath}");
                            break;
                        }
                    }
                }
                //VBSS
                var vbPass = true;
                if (vbssFiles.Count() > 0)
                {
                    foreach (var vbFile in vbssFiles)
                    {
                        var vbPath = Path.Combine(vbssDirPath, vbFile.FileName);
                        if (!File.Exists(vbPath) || !vbFile.IsProcessDone)
                        {
                            vbPass = false;
                            NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated vbss File not Done: {vbPath}");
                            break;
                        }
                    }
                }
                if (aPass && vPass && vbPass)
                {
                    NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated all files Process Done callID: {callID}");
                    result = true;
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                    timeOut = DateTime.Now > deadline;
                }
            } while (!timeOut);
            if (timeOut)
            {
                result = false;
                NLogHelper.Instance.Debug($"[Utilities] CheckFileGenerated timeOut callID: {callID}");
            }
            return result;
        }
    }
}