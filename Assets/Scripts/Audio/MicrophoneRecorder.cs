using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Utils;

namespace MultiAgentNPC.Audio
{
    /// <summary>
    /// Captures microphone audio and encodes it to a 16-bit PCM WAV (Sprint 8). Prefers
    /// 16 kHz mono but honors the device's supported range via
    /// <see cref="Microphone.GetDeviceCaps"/>, and always encodes using the ACTUAL
    /// <see cref="AudioClip.frequency"/> / <see cref="AudioClip.channels"/> of the captured
    /// clip (never a hard-coded rate).
    ///
    /// Permission is treated as asynchronous: <see cref="StartAsync"/> awaits microphone
    /// authorization and refuses to start if it is denied. A maximum length triggers an
    /// auto-stop (<see cref="AutoStopped"/>); after that, a normal stop is a no-op so the
    /// audio is never finalized twice.
    ///
    /// Pure capture: it does not call STT or the dialogue system.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Audio/Microphone Recorder")]
    public class MicrophoneRecorder : MonoBehaviour
    {
        public enum MicErrorType
        {
            None,
            NoDevice,
            PermissionDenied,
            TooShort,
            NotRecording,
            Failed
        }

        /// <summary>Outcome of a recording attempt. Failures are typed, never thrown.</summary>
        public class RecordingResult
        {
            public bool Success;
            public MicErrorType ErrorType = MicErrorType.None;
            public string ErrorMessage = string.Empty;
            public byte[] WavBytes;
            public int SampleRate;
            public int Channels;
            public float DurationSeconds;

            public static RecordingResult Ok(byte[] wav, int sampleRate, int channels, float duration) =>
                new RecordingResult
                {
                    Success = true,
                    WavBytes = wav,
                    SampleRate = sampleRate,
                    Channels = channels,
                    DurationSeconds = duration
                };

            public static RecordingResult Fail(MicErrorType type, string message) =>
                new RecordingResult { Success = false, ErrorType = type, ErrorMessage = message };
        }

        private enum State
        {
            Idle,
            Recording,
            Stopped
        }

        [Header("Capture")]
        [Tooltip("Preferred sample rate (Hz). Clamped to the device's supported range.")]
        [SerializeField] private int preferredSampleRate = 16000;

        [Tooltip("Maximum recording length in seconds; recording auto-stops at this cap.")]
        [SerializeField] private int maxSeconds = 15;

        [Tooltip("Minimum recording length in seconds; shorter clips are rejected as too short.")]
        [SerializeField] private float minSeconds = 0.3f;

        /// <summary>Raised on the main thread when recording auto-stops at <c>maxSeconds</c>.</summary>
        public event Action AutoStopped;

        private State _state = State.Idle;
        private string _device;
        private AudioClip _clip;
        private int _sampleRate;
        private int _channels = 1;
        private int _capturedSamples;
        private float _startRealtime;
        private float _stoppedDuration;

        /// <summary>True while the microphone is actively capturing.</summary>
        public bool IsRecording => _state == State.Recording;

        /// <summary>Seconds elapsed in the current (or last) recording, capped at maxSeconds.</summary>
        public float ElapsedSeconds
        {
            get
            {
                if (_state == State.Recording)
                {
                    return Mathf.Min(Time.realtimeSinceStartup - _startRealtime, maxSeconds);
                }

                return _stoppedDuration;
            }
        }

        private void Update()
        {
            if (_state != State.Recording)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _startRealtime >= maxSeconds)
            {
                CaptureAndStopMic();
                try
                {
                    AutoStopped?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MicrophoneRecorder] An AutoStopped subscriber threw: {e}");
                }
            }
        }

        /// <summary>
        /// Ensures microphone authorization (asynchronously) and starts capture. Returns a
        /// typed failure for no device / permission denied / start failure; does not throw.
        /// </summary>
        public async Task<RecordingResult> StartAsync()
        {
            if (_state == State.Recording)
            {
                return RecordingResult.Fail(MicErrorType.Failed, "Already recording.");
            }

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                return RecordingResult.Fail(MicErrorType.NoDevice, "麦克风不可用 (no microphone device).");
            }

            // Permission is asynchronous: await authorization before touching Microphone.Start.
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                AsyncOperation op = Application.RequestUserAuthorization(UserAuthorization.Microphone);
                if (op != null)
                {
                    while (!op.isDone)
                    {
                        await Task.Yield();
                    }
                }

                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    return RecordingResult.Fail(MicErrorType.PermissionDenied, "麦克风权限不足 (permission denied).");
                }
            }

            try
            {
                _device = Microphone.devices[0];
                _sampleRate = ChooseSampleRate(_device, preferredSampleRate);

                _clip = Microphone.Start(_device, false, Mathf.Max(1, maxSeconds), _sampleRate);
                if (_clip == null)
                {
                    return RecordingResult.Fail(MicErrorType.Failed, "Microphone.Start returned null.");
                }

                // Use the ACTUAL clip parameters for later WAV encoding.
                _sampleRate = _clip.frequency > 0 ? _clip.frequency : _sampleRate;
                _channels = Mathf.Max(1, _clip.channels);
                _capturedSamples = 0;
                _startRealtime = Time.realtimeSinceStartup;
                _stoppedDuration = 0f;
                _state = State.Recording;

                return RecordingResult.Ok(null, _sampleRate, _channels, 0f);
            }
            catch (Exception e)
            {
                CleanupClip();
                _state = State.Idle;
                return RecordingResult.Fail(MicErrorType.Failed, $"Microphone start failed: {e.Message}");
            }
        }

        /// <summary>
        /// Stops capture (if needed) and returns the recorded audio as WAV bytes. Idempotent
        /// after finalization: a second call (or a call following an auto-stop that was
        /// already consumed) returns a NotRecording failure, so the audio is never produced
        /// twice.
        /// </summary>
        public RecordingResult StopAndGetWav()
        {
            if (_state == State.Idle)
            {
                return RecordingResult.Fail(MicErrorType.NotRecording, "Not recording.");
            }

            if (_state == State.Recording)
            {
                CaptureAndStopMic();
            }

            try
            {
                if (_clip == null || _capturedSamples <= 0)
                {
                    return RecordingResult.Fail(MicErrorType.TooShort, "没有听清 (empty recording).");
                }

                int channels = Mathf.Max(1, _channels);
                int sampleRate = _sampleRate > 0 ? _sampleRate : preferredSampleRate;
                float duration = (float)_capturedSamples / sampleRate;

                if (duration < minSeconds)
                {
                    return RecordingResult.Fail(MicErrorType.TooShort, "没有听清 (recording too short).");
                }

                var samples = new float[_capturedSamples * channels];
                _clip.GetData(samples, 0);

                byte[] wav = WavUtility.EncodeToWav16(samples, channels, sampleRate);
                if (wav == null)
                {
                    return RecordingResult.Fail(MicErrorType.Failed, "WAV encoding failed.");
                }

                return RecordingResult.Ok(wav, sampleRate, channels, duration);
            }
            finally
            {
                CleanupClip();
                _state = State.Idle;
            }
        }

        /// <summary>
        /// Aborts any active recording without producing audio (mid-leave cancellation).
        /// Safe to call at any time.
        /// </summary>
        public void Cancel()
        {
            if (_state == State.Recording)
            {
                CaptureAndStopMic();
            }

            CleanupClip();
            _state = State.Idle;
            _stoppedDuration = 0f;
        }

        private void CaptureAndStopMic()
        {
            if (!string.IsNullOrEmpty(_device))
            {
                // GetPosition must be read before End (End resets it to 0).
                int pos = Microphone.GetPosition(_device);
                if (Microphone.IsRecording(_device))
                {
                    Microphone.End(_device);
                }

                _capturedSamples = Mathf.Max(0, pos);
            }

            _stoppedDuration = _sampleRate > 0 ? (float)_capturedSamples / _sampleRate : 0f;
            _state = State.Stopped;
        }

        private void CleanupClip()
        {
            if (_clip != null)
            {
                Destroy(_clip);
                _clip = null;
            }
        }

        private static int ChooseSampleRate(string device, int preferred)
        {
            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);

            // (0, 0) means the device supports any rate.
            if (minFreq == 0 && maxFreq == 0)
            {
                return preferred > 0 ? preferred : 16000;
            }

            int rate = preferred > 0 ? preferred : 16000;
            if (rate < minFreq)
            {
                rate = minFreq;
            }

            if (maxFreq > 0 && rate > maxFreq)
            {
                rate = maxFreq;
            }

            return rate;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
