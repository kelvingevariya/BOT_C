using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.MediaBuffer;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using ComplianceRecordingBot.FrontEnd.Util;
using NAudio.Wave;
using RecordingMergeTools;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ComplianceRecordingBot.FrontEnd.Media
{
    /// <summary>
    /// Class AudioProcessor.
    /// Implements the <see cref="BufferBase{SerializableAudioMediaBuffer}" />
    /// </summary>
    /// <seealso cref="BufferBase{SerializableAudioMediaBuffer}" />
    public class AudioProcessor : BufferBase<SerializableAudioMediaBuffer>
    {
        /// <summary>
        /// The writers
        /// </summary>
        private readonly Dictionary<string, WaveFileWriter> _writers = new Dictionary<string, WaveFileWriter>();

        /// <summary>
        ///
        /// </summary>
        private readonly string _callId = null;

        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _settings;

        /// <summary>
        ///
        /// </summary>
        private string _audioDirPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioProcessor" /> class.
        /// </summary>
        /// <param name="callID"></param>
        public AudioProcessor(string callID)
        {
            _callId = callID;
            _settings = Bot.Bot.Instance.AzureSettings;
            _audioDirPath = Path.Combine(_settings.DefaultOutputFolder, _callId, _settings.AudioFolder);
            NLogHelper.Instance.Debug($"[AudioProcessor] _callId: {_callId} _audioFilePath: {_audioDirPath}");
        }

        /// <summary>
        /// Processes the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        protected override async Task Process(SerializableAudioMediaBuffer data)
        {
            if (data.Timestamp == 0)
            {
                return;
            }
            // First, write all audio buffer, unless the data.IsSilence is checked for true, into the all speakers buffer
            var all = "all";
            var all_writer = _writers.ContainsKey(all) ? _writers[all] : InitialiseWavFileWriter(_audioDirPath, all);
            if (data.Buffer != null)
            {
                // Buffers are saved to disk even when there is silence.
                // If you do not want this to happen, check if data.IsSilence == true.
                await all_writer.WriteAsync(data.Buffer, 0, data.Buffer.Length).ConfigureAwait(false);
                var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
                var audioInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 0);
                if (audioInfo == null)
                {
                    audioInfo = new RecordingFileInfo();
                    recordingFileInfoList.Add(audioInfo);
                    NLogHelper.Instance.Debug($"[AudioProcessor] Add audioInfo FileName: {audioInfo.FileName} CallID: {_callId}");
                }
                audioInfo.SetRecordingTime(data.Timestamp);
            }
            if (data.SerializableUnmixedAudioBuffers != null)
            {
                foreach (var s in data.SerializableUnmixedAudioBuffers)
                {
                    if (string.IsNullOrWhiteSpace(s.AdId) || string.IsNullOrWhiteSpace(s.DisplayName))
                    {
                        continue;
                    }
                    var id = s.AdId;
                    var writer = _writers.ContainsKey(id) ? _writers[id] : InitialiseWavFileWriter(_audioDirPath, id);
                    // Write audio buffer into the WAV file for individual speaker
                    await writer.WriteAsync(s.Buffer, 0, s.Buffer.Length).ConfigureAwait(false);
                    // Write audio buffer into the WAV file for all speakers
                    await all_writer.WriteAsync(s.Buffer, 0, s.Buffer.Length).ConfigureAwait(false);
                    var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
                    var audioInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 0);
                    if (audioInfo == null)
                    {
                        audioInfo = new RecordingFileInfo();
                        recordingFileInfoList.Add(audioInfo);
                        NLogHelper.Instance.Debug($"[AudioProcessor] Add audioInfo FileName: {audioInfo.FileName} CallID: {_callId}");
                    }
                    audioInfo.SetRecordingTime(data.Timestamp);
                }
            }
        }

        /// <summary>
        /// Initialises the wav file writer.
        /// </summary>
        /// <param name="rootFolder">The root folder.</param>
        /// <param name="id">The identifier.</param>
        /// <returns>WavFileWriter.</returns>
        private WaveFileWriter InitialiseWavFileWriter(string rootFolder, string id)
        {
            var path = AudioFileUtils.CreateFilePath(rootFolder, $"{id}.wav");
            // Initialize the Wave Format using the default PCM 16bit 16K supported by Teams audio settings
            var writer = new WaveFileWriter(path, new WaveFormat(
                rate: BotConstants.DefaultSampleRate,
                bits: BotConstants.DefaultBits,
                channels: BotConstants.DefaultChannels));
            _writers.Add(id, writer);
            return writer;
        }

        /// <summary>
        /// Finalises the wav writing and returns a list of all the files created
        /// </summary>
        /// <returns>System.String.</returns>
        public async Task<string> Finalise()
        {
            //drain the un-processed buffers on this object
            while (Buffer.Count > 0)
            {
                await Task.Delay(200);
            }
            try
            {
                // drain all the writers
                foreach (var writer in _writers.Values)
                {
                    var localFileName = writer.Filename;
                    await writer.FlushAsync();
                    writer.Dispose();
                    NLogHelper.Instance.Debug($"[AudioProcessor] Audio File Path: {localFileName}");
                }
            }
            finally
            {
                await End();
            }
            var recordingFileInfoList = Bot.Bot.Instance.RecordingFileInfoList[_callId];
            var audioInfo = recordingFileInfoList.FirstOrDefault(i => i.FileType == 0);
            if (audioInfo != null)
            {
                audioInfo.IsProcessDone = true;
                NLogHelper.Instance.Debug($"[AudioProcessor] Create Audio File Done");
            }
            else
                NLogHelper.Instance.Debug($"[AudioProcessor] Not Find Audio Info");

            return _audioDirPath;
        }
    }
}