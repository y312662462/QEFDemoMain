using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MultiAgentNPC.Audio;
using MultiAgentNPC.DebugTools;
using MultiAgentNPC.InputControl;
using MultiAgentNPC.NPC;
using MultiAgentNPC.Services;
using MultiAgentNPC.UI;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Owns the Push-To-Talk voice lifecycle (Sprint 8): record on Space-down, stop on
    /// Space-up, transcribe via the real <see cref="ISTTService"/>, then hand the text to
    /// <see cref="DialogueManager.SubmitPlayerText"/> - the SAME entry point Debug text
    /// uses. It deliberately does NOT own the dialogue turn (that stays in
    /// <see cref="DialogueManager"/>); it only owns recording + STT + the submit decision.
    ///
    /// Safety:
    /// - Recording only starts when <see cref="DialogueManager.CanStartTalking"/> and no
    ///   local record/transcribe is in flight.
    /// - A per-attempt voice <see cref="CancellationTokenSource"/> is cancelled when the
    ///   active NPC clears; the mic stops, audio/transcript are discarded, and a late STT
    ///   result for a cancelled session is ignored.
    /// - Auto-stop (max length) finalizes the audio once; a later Space release is ignored
    ///   so the clip is never finalized or submitted twice.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Dialogue/Voice Input Controller")]
    public class VoiceInputController : MonoBehaviour
    {
        private const string NotHeardMessage = "没有听清，请再说一遍。";

        [Header("Scene References")]
        [Tooltip("Source of AI service settings + secrets. Auto-found if left empty.")]
        [SerializeField] private AIServiceConfig aiServiceConfig;

        [Tooltip("Push-To-Talk input wrapper. Auto-found if left empty.")]
        [SerializeField] private PushToTalkInputController talkInput;

        [Tooltip("Microphone capture component. Auto-found if left empty.")]
        [SerializeField] private MicrophoneRecorder micRecorder;

        [Tooltip("Dialogue host that owns the turn lifecycle. Auto-found if left empty.")]
        [SerializeField] private DialogueManager dialogueManager;

        [Tooltip("Subtitle surface used to show the retry message. Auto-found if left empty.")]
        [SerializeField] private SubtitleUI subtitleUI;

        [Header("Behaviour")]
        [Tooltip("Log voice lifecycle to the Console.")]
        [SerializeField] private bool logEvents = true;

        private ISTTService _stt;
        private string _language = ServiceDefaults.DefaultSttLanguage;

        private CancellationTokenSource _voiceCts;
        private bool _recording;
        private bool _transcribing;

        private ActiveNPCService _npcService;
        private bool _npcBound;
        private bool _subscribed;

        private void Start()
        {
            ResolveReferences();
            BuildStt();
            Subscribe();
            TryBindNpcEvents();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CancelVoiceSession();

            if (_voiceCts != null)
            {
                _voiceCts.Dispose();
                _voiceCts = null;
            }
        }

        private void Update()
        {
            if (!_npcBound)
            {
                TryBindNpcEvents();
            }

            if (_recording && micRecorder != null)
            {
                DebugStateStore.Instance.SetRecording(true, micRecorder.ElapsedSeconds);
            }
        }

        private void ResolveReferences()
        {
            if (aiServiceConfig == null)
            {
                aiServiceConfig = FindFirstObjectByType<AIServiceConfig>();
            }

            if (talkInput == null)
            {
                talkInput = FindFirstObjectByType<PushToTalkInputController>();
            }

            if (micRecorder == null)
            {
                micRecorder = FindFirstObjectByType<MicrophoneRecorder>();
            }

            if (dialogueManager == null)
            {
                dialogueManager = FindFirstObjectByType<DialogueManager>();
            }

            if (subtitleUI == null)
            {
                subtitleUI = FindFirstObjectByType<SubtitleUI>();
            }

            if (talkInput == null)
            {
                Debug.LogWarning("[VoiceInputController] No PushToTalkInputController found; voice input disabled.");
            }

            if (micRecorder == null)
            {
                Debug.LogWarning("[VoiceInputController] No MicrophoneRecorder found; voice input disabled.");
            }

            if (dialogueManager == null)
            {
                Debug.LogError("[VoiceInputController] No DialogueManager found; cannot submit player text.");
            }
        }

        private void BuildStt()
        {
            if (aiServiceConfig == null)
            {
                Debug.LogError("[VoiceInputController] No AIServiceConfig; STT will be unavailable.");
                return;
            }

            AIServiceSettings settings = aiServiceConfig.Settings;
            _stt = ServiceFactory.CreateSTTService(settings);
            _language = settings?.Stt != null ? settings.Stt.ResolveLanguage() : ServiceDefaults.DefaultSttLanguage;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (talkInput != null)
            {
                talkInput.TalkStarted += OnTalkStarted;
                talkInput.TalkEnded += OnTalkEnded;
            }

            if (micRecorder != null)
            {
                micRecorder.AutoStopped += OnRecordingAutoStopped;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (talkInput != null)
            {
                talkInput.TalkStarted -= OnTalkStarted;
                talkInput.TalkEnded -= OnTalkEnded;
            }

            if (micRecorder != null)
            {
                micRecorder.AutoStopped -= OnRecordingAutoStopped;
            }

            if (_npcService != null)
            {
                _npcService.ActiveNPCCleared -= OnActiveNpcCleared;
            }

            _npcBound = false;
            _subscribed = false;
        }

        private void TryBindNpcEvents()
        {
            if (_npcBound)
            {
                return;
            }

            NPCManager manager = NPCManager.Instance;
            if (manager == null || manager.ActiveService == null)
            {
                return;
            }

            _npcService = manager.ActiveService;
            _npcService.ActiveNPCCleared += OnActiveNpcCleared;
            _npcBound = true;
        }

        // ----- Push-To-Talk -----

        private async void OnTalkStarted()
        {
            if (_recording || _transcribing)
            {
                return;
            }

            if (micRecorder == null || dialogueManager == null)
            {
                return;
            }

            if (!dialogueManager.CanStartTalking)
            {
                if (logEvents)
                {
                    Debug.Log("[VoiceInputController] Talk ignored: cannot start talking (busy or no active NPC).");
                }
                return;
            }

            _voiceCts?.Dispose();
            _voiceCts = new CancellationTokenSource();
            CancellationToken token = _voiceCts.Token;
            _recording = true;
            DebugStateStore.Instance.SetLastSttError(string.Empty);

            MicrophoneRecorder.RecordingResult start = await micRecorder.StartAsync();

            // The session may have been cancelled while awaiting microphone authorization.
            if (token.IsCancellationRequested)
            {
                micRecorder.Cancel();
                _recording = false;
                DebugStateStore.Instance.SetRecording(false, 0f);
                return;
            }

            if (!start.Success)
            {
                _recording = false;
                DebugStateStore.Instance.SetRecording(false, 0f);
                FailWithMessage(start.ErrorMessage, attemptedSpeech: true);
                return;
            }

            DebugStateStore.Instance.SetRecording(true, 0f);
            if (logEvents)
            {
                Debug.Log($"[VoiceInputController] Recording started ({start.SampleRate} Hz, {start.Channels} ch).");
            }
        }

        private void OnTalkEnded()
        {
            // Ignored when not recording (e.g. an auto-stop already finalized this session),
            // which prevents a second StopAndGetWav / double submission.
            if (!_recording)
            {
                return;
            }

            _ = FinishRecordingAndTranscribeAsync();
        }

        private void OnRecordingAutoStopped()
        {
            if (!_recording)
            {
                return;
            }

            if (logEvents)
            {
                Debug.Log("[VoiceInputController] Recording auto-stopped at max length.");
            }

            _ = FinishRecordingAndTranscribeAsync();
        }

        private async Task FinishRecordingAndTranscribeAsync()
        {
            // Guard against re-entrancy (TalkEnded + AutoStopped racing): only the first
            // caller flips _recording false and finalizes the audio.
            if (!_recording)
            {
                return;
            }

            _recording = false;

            CancellationToken token = _voiceCts != null ? _voiceCts.Token : CancellationToken.None;

            MicrophoneRecorder.RecordingResult result = micRecorder.StopAndGetWav();
            DebugStateStore.Instance.SetRecording(false, result.Success ? result.DurationSeconds : 0f);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!result.Success || result.WavBytes == null || result.WavBytes.Length == 0)
            {
                FailWithMessage(result.ErrorMessage, attemptedSpeech: true);
                return;
            }

            if (_stt == null)
            {
                FailWithMessage("STT service unavailable.", attemptedSpeech: true);
                return;
            }

            _transcribing = true;
            ServiceResult<string> stt;
            try
            {
                stt = await _stt.TranscribeAsync(new STTRequest(result.WavBytes, _language, "wav"), token);
            }
            catch (OperationCanceledException)
            {
                _transcribing = false;
                return;
            }
            catch (Exception e)
            {
                _transcribing = false;
                FailWithMessage($"STT error: {e.Message}", attemptedSpeech: true);
                return;
            }

            _transcribing = false;

            // Late result from a cancelled voice session must be ignored (mid-leave).
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (stt == null || !stt.IsSuccess || string.IsNullOrWhiteSpace(stt.Value))
            {
                string reason = stt != null && !stt.IsSuccess
                    ? $"{stt.ErrorType} - {stt.ErrorMessage}"
                    : "empty transcript";
                FailWithMessage(reason, attemptedSpeech: true);
                return;
            }

            string text = stt.Value.Trim();

            // Pre-submit re-checks (requirement 7): token live, still able to talk, NPC present.
            if (token.IsCancellationRequested
                || dialogueManager == null
                || !dialogueManager.CanStartTalking
                || NPCManager.Instance == null
                || NPCManager.Instance.ActiveNpc == null)
            {
                if (logEvents)
                {
                    Debug.Log("[VoiceInputController] Transcript discarded: gate no longer satisfied.");
                }
                return;
            }

            DebugStateStore.Instance.SetLastSttText(text);
            dialogueManager.SubmitPlayerText(text);
        }

        private void OnActiveNpcCleared(NPCController previous)
        {
            CancelVoiceSession();
        }

        /// <summary>
        /// Cancels the current voice attempt (if any): trips the token, stops the mic,
        /// discards audio/transcript, and clears Recording state. Shows no message - the
        /// player left, they did not fail to be understood.
        /// </summary>
        private void CancelVoiceSession()
        {
            if (_voiceCts != null)
            {
                try
                {
                    _voiceCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            if (micRecorder != null)
            {
                micRecorder.Cancel();
            }

            _recording = false;
            _transcribing = false;
            DebugStateStore.Instance.SetRecording(false, 0f);
        }

        private void FailWithMessage(string error, bool attemptedSpeech)
        {
            if (!string.IsNullOrEmpty(error))
            {
                DebugStateStore.Instance.SetLastSttError(error);
                if (logEvents)
                {
                    Debug.LogWarning($"[VoiceInputController] {error}");
                }
            }

            // Only nag the player when they actually tried to speak, and only while still
            // in range (state stays InRange; the dialogue host owns terminal state).
            if (attemptedSpeech
                && subtitleUI != null
                && dialogueManager != null
                && dialogueManager.CanStartTalking)
            {
                subtitleUI.ShowError(NotHeardMessage);
            }
        }
    }
}
