using CommonTools.Logging;
using ComplianceRecordingBot.FrontEnd.Contract;
using ComplianceRecordingBot.FrontEnd.MediaBuffer;
using ComplianceRecordingBot.FrontEnd.ServiceSetup;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Skype.Bots.Media;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ComplianceRecordingBot.FrontEnd.Media
{
    /// <summary>
    /// Class MediaStream.
    /// Implements the <see cref="IMediaStream" />
    /// </summary>
    /// <seealso cref="IMediaStream" />
    public class MediaStream : IMediaStream
    {
        /// <summary>
        /// The audio buffer
        /// </summary>
        private BufferBlock<SerializableAudioMediaBuffer> _audioBuffer;

        /// <summary>
        /// The video buffer
        /// </summary>
        private BufferBlock<SerializableVideoMediaBuffer> _videoBuffer;

        /// <summary>
        /// The video buffer
        /// </summary>
        private BufferBlock<SerializableVideoMediaBuffer> _vbssBuffer;

        /// <summary>
        /// The token source
        /// </summary>
        private CancellationTokenSource _audioTokenSource;

        /// <summary>
        /// The token source
        /// </summary>
        private CancellationTokenSource _videoTokenSource;

        /// <summary>
        /// The token source
        /// </summary>
        private CancellationTokenSource _vbssTokenSource;

        /// <summary>
        /// The is the indicator if the media stream is running
        /// </summary>
        private bool _isAudioRunning = false;

        /// <summary>
        /// The is the indicator if the media stream is running
        /// </summary>
        private bool _isVideoRunning = false;

        /// <summary>
        /// The is the indicator if the media stream is running
        /// </summary>
        private bool _isVBSSRunning = false;

        /// <summary>
        /// The is draining indicator
        /// </summary>
        protected bool _isAudioDraining;

        /// <summary>
        /// The is draining indicator
        /// </summary>
        protected bool _isVideoDraining;

        /// <summary>
        /// The is draining indicator
        /// </summary>
        protected bool _isVBSSDraining;

        /// <summary>
        /// The synchronize audio lock
        /// </summary>
        private readonly SemaphoreSlim _syncAudioLock = new SemaphoreSlim(1);

        /// <summary>
        /// The synchronize video lock
        /// </summary>
        private readonly SemaphoreSlim _syncVideoLock = new SemaphoreSlim(1);

        /// <summary>
        /// The synchronize VBSS lock
        /// </summary>
        private readonly SemaphoreSlim _syncVBSSLock = new SemaphoreSlim(1);

        /// <summary>
        /// The media identifier
        /// </summary>
        private readonly string _mediaId;

        /// <summary>
        /// The settings
        /// </summary>
        private readonly AzureSettings _settings;

        /// <summary>
        /// The logger
        /// </summary>
        private readonly IGraphLogger _logger;

        /// <summary>
        /// The current audio processor
        /// </summary>
        private AudioProcessor _currentAudioProcessor;

        /// <summary>
        /// The current video processor
        /// </summary>
        private VideoProcessor _currentVideoProcessor;

        /// <summary>
        /// The current video processor
        /// </summary>
        private VBSSProcessor _currentVBSSProcessor;

        /// <summary>
        /// The capture
        /// </summary>
        private CaptureEvents _audioCapture;

        /// <summary>
        /// The capture
        /// </summary>
        private CaptureEvents _videoCapture;

        /// <summary>
        /// The capture
        /// </summary>
        private CaptureEvents _vbssCapture;

        private string _callID;

        // This dictionnary helps maintaining a mapping of the sockets subscriptions
        private readonly ConcurrentDictionary<uint, uint> _msiToVideoSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _msiToVBSSSocketIdMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _msiToVideoSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        private readonly ConcurrentDictionary<uint, uint> _msiToVBSSSerialNumMapping = new ConcurrentDictionary<uint, uint>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaStream" /> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="mediaId">The media identifier.</param>
        /// <param name="callID"></param>
        /// <param name="msiToVideoSocketIdMapping"></param>
        /// <param name="msiToVBSSSocketIdMapping"></param>
        /// <param name="msiToVideoSerialNumMapping"></param>
        /// <param name="msiToVBSSSerialNumMapping"></param>
        public MediaStream(AzureSettings settings, IGraphLogger logger, string mediaId, string callID, ConcurrentDictionary<uint, uint> msiToVideoSocketIdMapping, ConcurrentDictionary<uint, uint> msiToVBSSSocketIdMapping, ConcurrentDictionary<uint, uint> msiToVideoSerialNumMapping, ConcurrentDictionary<uint, uint> msiToVBSSSerialNumMapping)
        {
            _settings = settings;
            _logger = logger;
            _mediaId = mediaId;
            _callID = callID;
            _msiToVideoSocketIdMapping = msiToVideoSocketIdMapping;
            _msiToVBSSSocketIdMapping = msiToVBSSSocketIdMapping;
            _msiToVideoSerialNumMapping = msiToVideoSerialNumMapping;
            _msiToVBSSSerialNumMapping = msiToVBSSSerialNumMapping;
        }

        /// <summary>
        /// Appends the audio buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="participants">The participants.</param>
        public async Task AppendAudioBuffer(AudioMediaBuffer buffer, List<IParticipant> participants)
        {
            if (!_isAudioRunning)
            {
                await _startAudio();
            }
            try
            {
                await _audioBuffer.SendAsync(new SerializableAudioMediaBuffer(buffer, participants), _audioTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                _audioBuffer?.Complete();
                _logger.Info($"[MediaStream] Audio Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
                NLogHelper.Instance.Debug($"[MediaStream] Audio Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
            }
        }

        /// <summary>
        /// Appends the video buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="participants"></param>
        /// <returns></returns>
        public async Task AppendVideoBuffer(VideoMediaBuffer buffer, List<IParticipant> participants)
        {
            if (!_isVideoRunning)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _startVideo msi: {buffer.MediaSourceId}");
                await _startVideo();
            }
            try
            {
                await _videoBuffer.SendAsync(new SerializableVideoMediaBuffer(buffer, participants), _videoTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                _videoBuffer?.Complete();
                _logger.Info($"[MediaStream] Video Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
                NLogHelper.Instance.Debug($"[MediaStream Error] Video Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
            }
        }

        /// <summary>
        /// Appends the VBSS buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="participants"></param>
        /// <returns></returns>
        public async Task AppendVBSSBuffer(VideoMediaBuffer buffer, List<IParticipant> participants)
        {
            if (!_isVBSSRunning)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _startVBSS msi: {buffer.MediaSourceId}");
                await _startVBSS();
            }
            try
            {
                await _vbssBuffer.SendAsync(new SerializableVideoMediaBuffer(buffer, participants), _vbssTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException e)
            {
                _vbssBuffer?.Complete();
                _logger.Info($"[MediaStream] VBSS Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
                NLogHelper.Instance.Debug($"[MediaStream] VBSS Cannot enqueue because queuing operation has been cancelled. TaskCanceledException: {e.Message}");
            }
        }

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task AudioEnd()
        {
            NLogHelper.Instance.Debug($"[MediaStream] AudioEnd");
            if (!_isAudioRunning)
            {
                return;
            }
            await _syncAudioLock.WaitAsync().ConfigureAwait(false);
            if (_isAudioRunning)
            {
                _isAudioDraining = true;
                while (_audioBuffer.Count > 0)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
                _audioBuffer.Complete();
                _audioBuffer.TryDispose();
                _audioBuffer = null;
                _audioTokenSource.Cancel();
                _audioTokenSource.Dispose();
                _isAudioRunning = false;
                while (_isAudioDraining)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
            _syncAudioLock.Release();
        }

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task VideoEnd()
        {
            NLogHelper.Instance.Debug($"[MediaStream] VideoEnd start");
            if (!_isVideoRunning)
            {
                return;
            }
            await _syncVideoLock.WaitAsync().ConfigureAwait(false);
            if (_isVideoRunning)
            {
                _isVideoDraining = true;
                while (_videoBuffer.Count > 0)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
                _videoBuffer.Complete();
                _videoBuffer.TryDispose();
                _videoBuffer = null;
                _videoTokenSource.Cancel();
                _videoTokenSource.Dispose();
                _isVideoRunning = false;
                while (_isVideoDraining)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
            _syncVideoLock.Release();
            NLogHelper.Instance.Debug($"[MediaStream] VideoEnd end");
        }

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task VBSSEnd()
        {
            NLogHelper.Instance.Debug($"[MediaStream] VBSSEnd start");
            if (!_isVBSSRunning)
            {
                return;
            }
            await _syncVBSSLock.WaitAsync().ConfigureAwait(false);
            if (_isVBSSRunning)
            {
                _isVBSSDraining = true;
                while (_vbssBuffer.Count > 0)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
                _vbssBuffer.Complete();
                _vbssBuffer.TryDispose();
                _vbssBuffer = null;
                _vbssTokenSource.Cancel();
                _vbssTokenSource.Dispose();
                _isVBSSRunning = false;
                while (_isVBSSDraining)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
            _syncVBSSLock.Release();
            NLogHelper.Instance.Debug($"[MediaStream] VBSSEnd end");
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private async Task _startAudio()
        {
            await _syncAudioLock.WaitAsync().ConfigureAwait(false);
            if (!_isAudioRunning)
            {
                _audioTokenSource = new CancellationTokenSource();
                _audioBuffer = new BufferBlock<SerializableAudioMediaBuffer>(new DataflowBlockOptions { CancellationToken = _audioTokenSource.Token });
                await Task.Factory.StartNew(_audioProcess).ConfigureAwait(false);
                _isAudioRunning = true;
            }
            _syncAudioLock.Release();
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private async Task _startVideo()
        {
            await _syncVideoLock.WaitAsync().ConfigureAwait(false);
            if (!_isVideoRunning)
            {
                _videoTokenSource = new CancellationTokenSource();
                _videoBuffer = new BufferBlock<SerializableVideoMediaBuffer>(new DataflowBlockOptions { CancellationToken = _videoTokenSource.Token });
                await Task.Factory.StartNew(_videoProcess).ConfigureAwait(false);
                _isVideoRunning = true;
            }
            _syncVideoLock.Release();
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        private async Task _startVBSS()
        {
            await _syncVBSSLock.WaitAsync().ConfigureAwait(false);
            if (!_isVBSSRunning)
            {
                _vbssTokenSource = new CancellationTokenSource();
                _vbssBuffer = new BufferBlock<SerializableVideoMediaBuffer>(new DataflowBlockOptions { CancellationToken = _vbssTokenSource.Token });
                await Task.Factory.StartNew(_vbssProcess).ConfigureAwait(false);
                _isVBSSRunning = true;
            }
            _syncVBSSLock.Release();
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        private async Task _audioProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _audioProcess start");
            _currentAudioProcessor = new AudioProcessor(_callID);
            if (_settings.CaptureEvents && !_isAudioDraining && _audioCapture == null)
            {
                _audioCapture = new CaptureEvents(Path.Combine(_settings.DefaultOutputFolder, _settings.EventsFolder, _mediaId, "media"));
            }
            try
            {
                while (await _audioBuffer.OutputAvailableAsync(_audioTokenSource.Token).ConfigureAwait(false))
                {
                    SerializableAudioMediaBuffer data = await _audioBuffer.ReceiveAsync(_audioTokenSource.Token).ConfigureAwait(false);
                    if (_settings.CaptureEvents)
                    {
                        await _audioCapture?.Append(data);
                    }
                    await _currentAudioProcessor.Append(data);
                    _audioTokenSource.Token.ThrowIfCancellationRequested();
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "[MediaStream] _audioProcess: The queue processing task has been cancelled.");
            }
            catch (ObjectDisposedException ex)
            {
                _logger.Error(ex, "[MediaStream] _audioProcess: The queue processing task object has been disposed.");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                NLogHelper.Instance.Debug($"[MediaStream Error] Audio Process Msg: {ex.Message}");
                // Continue processing elements in the queue
                await _audioProcess().ConfigureAwait(false);
            }
            //send final segment as a last precation in case the loop did not process it
            if (_currentAudioProcessor != null)
            {
                await _chunkAudioProcess();
            }
            if (_settings.CaptureEvents)
            {
                await _audioCapture?.Finalise();
            }
            _isAudioDraining = false;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        private async Task _videoProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _videoProcess start");
            _currentVideoProcessor = new VideoProcessor(_callID, _msiToVideoSocketIdMapping, _msiToVideoSerialNumMapping);
            if (_settings.CaptureEvents && !_isVideoDraining && _videoCapture == null)
            {
                _videoCapture = new CaptureEvents(Path.Combine(_settings.DefaultOutputFolder, _settings.EventsFolder, _mediaId, "media"));
            }
            try
            {
                while (await _videoBuffer.OutputAvailableAsync(_videoTokenSource.Token).ConfigureAwait(false))
                {
                    SerializableVideoMediaBuffer data = await _videoBuffer.ReceiveAsync(_videoTokenSource.Token).ConfigureAwait(false);
                    if (_settings.CaptureEvents)
                    {
                        await _videoCapture?.Append(data);
                    }
                    await _currentVideoProcessor.Append(data);
                    _videoTokenSource.Token.ThrowIfCancellationRequested();
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "[MediaStream] The queue processing task has been cancelled.");
                NLogHelper.Instance.Debug($"[MediaStream] The queue processing task has been cancelled. TaskCanceledException: {ex.Message}");
            }
            catch (ObjectDisposedException ex)
            {
                _logger.Error(ex, "[MediaStream] The queue processing task object has been disposed.");
                NLogHelper.Instance.Debug($"[MediaStream] The queue processing task has been cancelled. ObjectDisposedException: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                NLogHelper.Instance.Debug($"[MediaStream Error] Video Process Msg: {ex.Message}");
                // Continue processing elements in the queue
                await _videoProcess().ConfigureAwait(false);
            }
            //send final segment as a last precation in case the loop did not process it
            if (_currentVideoProcessor != null)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _chunkVideoProcess");
                await _chunkVideoProcess();
            }
            else
            {
                NLogHelper.Instance.Debug($"[MediaStream] _currentVideoProcessor is null");
            }
            if (_settings.CaptureEvents)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _videoCapture Finalise");
                await _videoCapture?.Finalise();
            }
            _isVideoDraining = false;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        private async Task _vbssProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _vbssProcess start");
            _currentVBSSProcessor = new VBSSProcessor(_callID, _msiToVBSSSocketIdMapping, _msiToVBSSSerialNumMapping);
            if (_settings.CaptureEvents && !_isVBSSDraining && _vbssCapture == null)
            {
                _vbssCapture = new CaptureEvents(Path.Combine(_settings.DefaultOutputFolder, _settings.EventsFolder, _mediaId, "media"));
            }
            try
            {
                while (await _vbssBuffer.OutputAvailableAsync(_vbssTokenSource.Token).ConfigureAwait(false))
                {
                    SerializableVideoMediaBuffer data = await _vbssBuffer.ReceiveAsync(_vbssTokenSource.Token).ConfigureAwait(false);
                    if (_settings.CaptureEvents)
                    {
                        await _vbssCapture?.Append(data);
                    }
                    await _currentVBSSProcessor.Append(data);
                    _vbssTokenSource.Token.ThrowIfCancellationRequested();
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "[MediaStream] The queue processing task has been cancelled.");
                NLogHelper.Instance.Debug($"[MediaStream] The queue processing task has been cancelled. TaskCanceledException: {ex.Message}");
            }
            catch (ObjectDisposedException ex)
            {
                _logger.Error(ex, "[MediaStream] The queue processing task object has been disposed.");
                NLogHelper.Instance.Debug($"[MediaStream] The queue processing task has been cancelled. ObjectDisposedException: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions and log
                NLogHelper.Instance.Debug($"[MediaStream Error] VBSS Process Msg: {ex.Message}");
                // Continue processing elements in the queue
                await _vbssProcess().ConfigureAwait(false);
            }
            //send final segment as a last precation in case the loop did not process it
            if (_currentVBSSProcessor != null)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _chunkVBSSProcess");
                await _chunkVBSSProcess();
            }
            else
            {
                NLogHelper.Instance.Debug($"[MediaStream] _currentVBSSProcessor is null");
            }
            if (_settings.CaptureEvents)
            {
                NLogHelper.Instance.Debug($"[MediaStream] _vbssCapture Finalise");
                await _vbssCapture?.Finalise();
            }
            _isVBSSDraining = false;
        }

        /// <summary>
        /// audio Chunks the process.
        /// </summary>
        private async Task _chunkAudioProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _chunkAudioProcess start");
            try
            {
                var finalData = await _currentAudioProcessor.Finalise();
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[MediaStream Error] _chunkAudioProcess Msg: {ex.Message}");
            }
        }

        /// <summary>
        /// video Chunks the process.
        /// </summary>
        private async Task _chunkVideoProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _chunkVideoProcess start");
            try
            {
                var finalData = await _currentVideoProcessor.Finalise();
                NLogHelper.Instance.Debug($"[MediaStream] Video Recording saved to: {finalData}");
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[MediaStream Error] _chunkVideoProcess Msg: {ex.Message}");
            }
        }

        /// <summary>
        /// VBSS Chunks the process.
        /// </summary>
        private async Task _chunkVBSSProcess()
        {
            NLogHelper.Instance.Debug($"[MediaStream] _chunkVBSSProcess start");
            try
            {
                var finalData = await _currentVBSSProcessor.Finalise();
                NLogHelper.Instance.Debug($"[MediaStream] VBSS Recording saved to: {finalData}");
            }
            catch (Exception ex)
            {
                NLogHelper.Instance.Debug($"[MediaStream Error] _chunkVBSSProcess Msg: {ex.Message}");
            }
        }
    }
}