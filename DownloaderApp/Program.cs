using GraphApiTools;
using Microsoft.Graph;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DownloaderApp
{
    internal class Program
    {
        private static ILogger _Logger { get; set; }
        private static AppConfiguration _AppConfig { get; set; }
        private static GraphApiHelper _GraphHelper { get; set; }
        private static Site _Site { get; set; }
        private static int _ProcessCount { get; set; }

        private static void Main(string[] args)
        {
            try
            {
                Initial();
                var passCount = 0;
                var failCount = 0;
                var successCount = 0;
                var totalMilliseconds = 0L;
                Stopwatch sw = new Stopwatch();
                var fileItems = GetRecordingDriveItems();
                var totalCount = fileItems.Count;
                foreach (var fileItem in fileItems)
                {
                    if (_AppConfig.MaxProcessCount != 0 && _ProcessCount >= _AppConfig.MaxProcessCount)
                    {
                        WriteLog($"Process end to Max: {_AppConfig.MaxProcessCount}");
                        break;
                    }
                    WriteLog($"Process Name: {fileItem.Name} Size: {fileItem.Size} Id: {fileItem.Id} ParentDriveId: {fileItem.ParentReference.DriveId}");
                    try
                    {
                        if (!CheckFileNameAndExtension(fileItem.Name))
                        {
                            passCount++;
                            continue;
                        }
                        var tempFilePath = Path.Combine(_AppConfig.DownloadDirPath, $"{fileItem.Name}.pre");
                        var parentDriveId = fileItem.ParentReference.DriveId;
                        if (!System.IO.File.Exists(tempFilePath) || !CheckFileSize(tempFilePath, fileItem.Size))
                        {
                            if (System.IO.File.Exists(tempFilePath))
                                System.IO.File.Delete(tempFilePath);

                            totalMilliseconds += DownloadFile(sw, parentDriveId, fileItem.Id, fileItem.Name, tempFilePath);
                        }
                        if (!System.IO.File.Exists(tempFilePath))
                        {
                            failCount++;
                            WriteLog($"File not Exists: {tempFilePath}");
                            continue;
                        }
                        if (!CheckFileSize(tempFilePath, fileItem.Size))
                        {
                            failCount++;
                            WriteLog($"File Size check fail DriveItem Size: {fileItem.Size}");
                            continue;
                        }
                        var finalFilePath = Path.Combine(_AppConfig.DownloadDirPath, fileItem.Name);

                        //Rename
                        if (System.IO.File.Exists(finalFilePath))
                            System.IO.File.Delete(finalFilePath);

                        System.IO.File.Move(tempFilePath, finalFilePath);
                        WriteLog($"ReName: {tempFilePath} to {finalFilePath}");
                        _GraphHelper.DeleteItemBySite(_Site.Id, fileItem.Id);
                        WriteLog($"DeleteItem Name: {fileItem.Name} Id: {fileItem.Id}");
                        successCount++;
                        _ProcessCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        var msg = $"Process File Error Name: {fileItem.Name} Msg: {ex.Message}";

                        if (ex.InnerException != null)
                            msg += $" InnerException: {ex.InnerException.Message}";

                        WriteLog(msg);
                    }
                    finally
                    {
                        if (sw.IsRunning)
                            sw.Stop();

                        sw.Reset();
                    }
                }
                WriteLog($"Process Completed File TotalSeconds: {totalMilliseconds / 1000} TotalCount: {totalCount} SuccessCount: {successCount} PassCount: {passCount} FailCount: {failCount}");
            }
            catch (Exception ex)
            {
                var msg = $"Process Error: {ex.Message}";

                if (ex.InnerException != null)
                    msg += $" InnerException: {ex.InnerException.Message}";

                WriteLog(msg);
            }

            //Console.Read();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="fileItemName"></param>
        /// <returns></returns>
        private static bool CheckFileNameAndExtension(string fileItemName)
        {
            var result = true;
            var fileName = Path.GetFileNameWithoutExtension(fileItemName);
            var extension = Path.GetExtension(fileItemName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                result = false;
                WriteLog($"CheckFileNameAndEx: {result} File Name is empty: {fileItemName}");
            }
            else if (extension.ToLower() != ".zip")
            {
                result = false;
                WriteLog($"CheckFileNameAndEx: {result} File is not zip: {fileItemName}");
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="checkSize"></param>
        /// <returns></returns>
        private static bool CheckFileSize(string filePath, long? checkSize)
        {
            var fi = new FileInfo(filePath);
            var result = (fi.Length == checkSize);

            if (!result)
                WriteLog($"CheckFileSize: {result} file size not equals checkSize: {checkSize} filePath: {filePath}");

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="sw"></param>
        /// <param name="parentDriveId"></param>
        /// <param name="fileItemId"></param>
        /// <param name="fileItemName"></param>
        /// <param name="tempFilePath"></param>
        /// <returns></returns>
        private static long DownloadFile(Stopwatch sw, string parentDriveId, string fileItemId, string fileItemName, string tempFilePath)
        {
            sw.Start();
            _GraphHelper.DownloadLargeFile(parentDriveId, fileItemId, tempFilePath);
            sw.Stop();
            WriteLog($"Download Success Name: {fileItemName} Seconds: {sw.ElapsedMilliseconds / 1000}");
            return sw.ElapsedMilliseconds;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private static List<DriveItem> GetRecordingDriveItems()
        {
            var lists = _GraphHelper.GetLists(_Site.Id);
            var listDoc = lists.FirstOrDefault(i => i.DisplayName == "Documents");
            if (listDoc == null)
                throw new Exception("Documents List not found.");

            var listItems = _GraphHelper.GetListItems(_Site.Id, listDoc.Id);
            var itemRec = listItems.FirstOrDefault(i => i.WebUrl.EndsWith(_AppConfig.RelativePath));
            if (itemRec == null)
                throw new Exception($"{_AppConfig.RelativePath} ListItem not found.");

            var driveItemRec = _GraphHelper.GetDriveItem(_Site.Id, listDoc.Id, itemRec.Id);
            if (itemRec == null)
                throw new Exception($"{_AppConfig.RelativePath} DriveItem not found.");

            var fileItems = _GraphHelper.GetChildrenItem(_Site.Id, driveItemRec.Id);
            return fileItems;
        }

        /// <summary>
        ///
        /// </summary>
        private static void Initial()
        {
            InitNLog();
            _AppConfig = new AppConfiguration();
            _GraphHelper = new GraphApiHelper(
                _AppConfig.TenantId, _AppConfig.BotName, _AppConfig.AadAppId, _AppConfig.AadAppSecret);
            var siteRelativePath = _AppConfig.GroupName.Replace(" ", "");
            _Site = _GraphHelper.GetSite(siteRelativePath, _AppConfig.HostName);

            if (_Site == null)
                throw new Exception($"{_AppConfig.GroupName} Site not found.");

            WriteLog($"site: {_Site.DisplayName} id: {_Site.Id}");

            if (!System.IO.Directory.Exists(_AppConfig.DownloadDirPath))
                System.IO.Directory.CreateDirectory(_AppConfig.DownloadDirPath);
        }

        /// <summary>
        ///
        /// </summary>
        private static void InitNLog()
        {
            try
            {
                var config = new NLog.Config.LoggingConfiguration();
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var fileName = $"{baseDir}\\nlogs" + "\\${shortdate}.log";
                var target = new NLog.Targets.FileTarget("f")
                {
                    FileName = fileName,
                    Layout = "${date:format=yyyy-MM-dd HH\\:mm\\:ss} ${message}",
                    MaxArchiveFiles = 50,
                    ArchiveAboveSize = 1024 * 1024 * 10,
                };
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, target);
                LogManager.Configuration = config;
                _Logger = LogManager.GetLogger("debug");
            }
            catch (Exception ex)
            {
                _Logger = null;
                Console.WriteLine($"InitNLog Error: {ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="msg"></param>
        private static void WriteLog(string msg)
        {
            Console.WriteLine(msg);

            if (_Logger != null)
                _Logger.Debug(msg);
        }
    }
}