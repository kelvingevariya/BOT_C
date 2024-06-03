using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.MediaBuffer;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using RecordingMergeTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ComplianceRecordingBot.FrontEnd.Media
{
    /// <summary>
    /// Class VideoProcessor.
    /// Implements the <see cref="BufferBase{SerializableVideoMediaBuffer}" />
    /// </summary>
    /// <seealso cref="BufferBase{SerializableVideoMediaBuffer}" />
    public class VideoProcessor : BufferBase<SerializableVideoMediaBuffer>
    {
        /// <summary>
        /// The video byte array list
        /// Key: participantID_msi_socketID
        /// </summary>
        private readonly Dictionary<string, List<byte[]>> _videoBufferList = new Dictionary<string, List<byte[]>>();

        private readonly string _callId = null;

        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _settings;

        private string _videoDirPath;

        // This dictionnary helps maintaining a mapping of the sockets subscriptions
        private readonly ConcurrentDictionary<uint, uint> _msiToSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _msiToSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioProcessor" /> class.
        /// </summary>
        /// <param name="callID"></param>
        /// <param name="msiToSocketIdMapping"></param>
        /// <param name="msiToSerialNumMapping"></param>
        public VideoProcessor(string callID, ConcurrentDictionary<uint, uint> msiToSocketIdMapping, ConcurrentDictionary<uint, uint> msiToSerialNumMapping)
        {
            _callId = callID;
            _settings = Bot.Bot.Instance.AzureSettings;
            _msiToSocketIdMapping = msiToSocketIdMapping;
            _msiToSerialNumMapping = msiToSerialNumMapping;
            _videoDirPath = Path.Combine(_settings.DefaultOutputFolder, _callId, _settings.VideoFolder);
            NLogHelper.Instance.Debug($"[VideoProcessor] _callId: {_callId} _audioFilePath: {_videoDirPath}");
        }

        /// <summary>
        /// Processes the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        protected override async Task Process(SerializableVideoMediaBuffer data)
        {
            if (data.Timestamp == 0)
            {
                return;
            }
            if (data.Buffer != null)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var key = $"{data.ParticipantID}_{data.MediaSourceId}_{_msiToSocketIdMapping[data.MediaSourceId]}_{_msiToSerialNumMapping[data.MediaSourceId]}";
                        if (_videoBufferList.ContainsKey(key))
                        {
                            _videoBufferList[key].Add(data.Buffer);
                        }
                        else
                        {
                            _videoBufferList.Add(key, new List<byte[]> { data.Buffer });
                            NLogHelper.Instance.Debug($"[VideoProcessor] Process Add new key: {key}");
                        }
                        var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
                        var videoInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 1 && i.Key == key);
                        if (videoInfo == null)
                        {
                            videoInfo = new RecordingFileInfo(key, 1);
                            recordingFileInfoList.Add(videoInfo);
                            NLogHelper.Instance.Debug($"[VideoProcessor] Add videoInfo FileName: {videoInfo.FileName} CallID: {_callId}");
                        }
                        videoInfo.SetRecordingTime(data.Timestamp, true);
                        NLogHelper.Instance.Debug($"[VideoProcessor] SetRecordingTime videoInfo DuringSeconds: {videoInfo.DuringSeconds}");
                    }
                    catch (Exception ex)
                    {
                        NLogHelper.Instance.Debug($"[VideoProcessor Error] Process: {ex.Message}");
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Finalises the wav writing and returns a list of all the files created
        /// </summary>
        /// <returns>System.String.</returns>
        public async Task<string> Finalise()
        {
            NLogHelper.Instance.Debug($"[VideoProcessor] Finalise Video start Buffer.Count:{Buffer.Count}");
            //drain the un-processed buffers on this object
            while (Buffer.Count > 0)
            {
                await Task.Delay(200);
            }

            if (!Directory.Exists(_videoDirPath))
                Directory.CreateDirectory(_videoDirPath);

            try
            {
                await Task.Run(() =>
                {
                    foreach (var kv in _videoBufferList)
                    {
                        var filePath = Path.Combine(_videoDirPath, $"V_{kv.Key}.h264");
                        int fileLength = 0;
                        int frameCount = 0;

                        if (File.Exists(filePath))
                            NLogHelper.Instance.Debug($"[VideoProcessor] File.Exists: {filePath}");

                        foreach (var buffer in kv.Value)
                        {
                            using (var fs = new FileStream(filePath, FileMode.Append))
                            {
                                fileLength += buffer.Length;
                                frameCount++;
                                fs.Write(buffer, 0, buffer.Length);
                                fs.Flush();
                            }
                        }
                        var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
                        var videoInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 1 && i.Key == kv.Key);
                        if (videoInfo != null)
                        {
                            videoInfo.FrameCount = frameCount;
                            videoInfo.IsProcessDone = true;
                            NLogHelper.Instance.Debug($"[VideoProcessor] Create Video Path: {filePath} DuringSeconds: {videoInfo.DuringSeconds} FrameCount:{videoInfo.FrameCount} Length:{fileLength}");
                        }
                        else
                            NLogHelper.Instance.Debug($"[VideoProcessor] Not Find Video Info");
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[VideoProcessor Error] Finalise: {ex.Message}");
            }
            finally
            {
                await End();
                NLogHelper.Instance.Debug($"[VideoProcessor] Finalise End");
            }

            return _videoDirPath;
        }
    }
}