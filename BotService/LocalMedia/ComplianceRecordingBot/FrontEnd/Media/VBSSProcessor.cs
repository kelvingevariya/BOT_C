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
    /// Class VBSSProcessor.
    /// Implements the <see cref="BufferBase{SerializableVideoMediaBuffer}" />
    /// </summary>
    /// <seealso cref="BufferBase{SerializableVideoMediaBuffer}" />
    public class VBSSProcessor : BufferBase<SerializableVideoMediaBuffer>
    {
        /// <summary>
        /// The vbss byte array list
        /// Key: AdId_MediaSourceId
        /// </summary>
        private readonly Dictionary<string, List<byte[]>> _vbssBufferList = new Dictionary<string, List<byte[]>>();

        private readonly string _callId = null;

        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _settings;

        private string _vbssDirPath;

        // This dictionnary helps maintaining a mapping of the sockets subscriptions
        private readonly ConcurrentDictionary<uint, uint> _msiToSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _msiToSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        /// <summary>
        /// Initializes a new instance of the <see cref="VBSSProcessor" /> class.
        /// </summary>
        /// <param name="callID"></param>
        /// <param name="msiToSocketIdMapping"></param>
        /// <param name="msiToSerialNumMapping"></param>
        public VBSSProcessor(string callID, ConcurrentDictionary<uint, uint> msiToSocketIdMapping, ConcurrentDictionary<uint, uint> msiToSerialNumMapping)
        {
            _callId = callID;
            _settings = Bot.Bot.Instance.AzureSettings;
            _msiToSocketIdMapping = msiToSocketIdMapping;
            _msiToSerialNumMapping = msiToSerialNumMapping;
            _vbssDirPath = Path.Combine(_settings.DefaultOutputFolder, _callId, _settings.VBSSFolder);
            NLogHelper.Instance.Debug($"[VBSSProcessor] _callId: {_callId} _audioFilePath: {_vbssDirPath}");
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
                        if (_vbssBufferList.ContainsKey(key))
                        {
                            _vbssBufferList[key].Add(data.Buffer);
                        }
                        else
                        {
                            _vbssBufferList.Add(key, new List<byte[]> { data.Buffer });
                            NLogHelper.Instance.Debug($"[VBSSProcessor] Process Add new key: {key}");
                        }
                        var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
                        var vbssInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 2 && i.Key == key);
                        if (vbssInfo == null)
                        {
                            vbssInfo = new RecordingFileInfo(key, 2);
                            recordingFileInfoList.Add(vbssInfo);
                            NLogHelper.Instance.Debug($"[VBSSProcessor] Add vbssInfo FileName: {vbssInfo.FileName} CallID: {_callId}");
                        }
                        vbssInfo.SetRecordingTime(data.Timestamp);
                    }
                    catch (Exception ex)
                    {
                        NLogHelper.Instance.Debug($"[VBSSProcessor Error] Process Msg: {ex.Message}");
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
            NLogHelper.Instance.Debug($"[VBSSProcessor] Finalise VBSS start Buffer.Count:{Buffer.Count}");
            //drain the un-processed buffers on this object
            while (Buffer.Count > 0)
            {
                await Task.Delay(200);
            }

            if (!Directory.Exists(_vbssDirPath))
                Directory.CreateDirectory(_vbssDirPath);

            try
            {
                await Task.Run(() =>
                {
                    foreach (var kv in _vbssBufferList)
                    {
                        var filePath = Path.Combine(_vbssDirPath, $"VB_{kv.Key}.h264");
                        int fileLength = 0;
                        int frameCount = 0;
                        if (File.Exists(filePath))
                            NLogHelper.Instance.Debug($"[VBSSProcessor] File.Exists: {filePath}");

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
                        var vbssInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 2 && i.Key == kv.Key);
                        if (vbssInfo != null)
                        {
                            vbssInfo.FrameCount = frameCount;
                            vbssInfo.IsProcessDone = true;
                            NLogHelper.Instance.Debug($"[VBSSProcessor] Create VBSS Path: {filePath} DuringSeconds: {vbssInfo.DuringSeconds} FrameCount:{vbssInfo.FrameCount} Length:{fileLength}");
                        }
                        else
                            NLogHelper.Instance.Debug($"[VBSSProcessor] Not Find VBSS Info");
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[VBSSProcessor Error] Finalise Msg: {ex.Message}");
            }
            finally
            {
                await End();
                NLogHelper.Instance.Debug($"[VBSSProcessor] Finalise End");
            }
            return _vbssDirPath;
        }
    }
}