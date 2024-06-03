using CommonTools.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ComplianceRecordingBot.FrontEnd.Util
{
    /// <summary>
    ///
    /// </summary>
    public class ZipFileHelper
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="destFilePath"></param>
        /// <param name="srcFilePathList"></param>
        /// <param name="delSrcFile"></param>
        /// <returns></returns>
        public static bool CreateZipFile(string destFilePath, List<string> srcFilePathList, bool delSrcFile = false)
        {
            bool result = false;
            if (srcFilePathList.Count == 0)
            {
                NLogHelper.Instance.Debug($"[ZipFileHelper] CreateZipFile srcFilePathList is empty.");
                return result;
            }
            try
            {
                if (File.Exists(destFilePath))
                {
                    File.Delete(destFilePath);
                    NLogHelper.Instance.Debug($"[ZipFileHelper] CreateZipFile Exists File Delete: {destFilePath}");
                }
                else
                {
                    var dirPath = Path.GetDirectoryName(destFilePath);
                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);
                }
                using (var stream = File.OpenWrite(destFilePath))
                {
                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
                    {
                        foreach (var localFile in srcFilePathList)
                        {
                            var localArchive = archive; //protect the closure below
                            if (File.Exists(localFile))
                            {
                                var fInfo = new FileInfo(localFile);
                                localArchive.CreateEntryFromFile(localFile, fInfo.Name, CompressionLevel.Optimal);
                                NLogHelper.Instance.Debug($"[ZipFileHelper] CreateZipFile CreateEntryFromFile: {localFile}");

                                if (delSrcFile)
                                    File.Delete(localFile);
                            }
                            else
                                NLogHelper.Instance.Debug($"[ZipFileHelper] CreateZipFile srcFile not exists: {localFile}");
                        }
                        result = true;
                        NLogHelper.Instance.Debug($"[ZipFileHelper] CreateZipFile Success: {destFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                NLogHelper.Instance.Debug($"[ZipFileHelper Error] CreateZipFile Msg: {ex.Message}");
            }
            return result;
        }
    }
}