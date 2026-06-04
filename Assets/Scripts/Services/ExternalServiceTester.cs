using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MultiAgentNPC.Utils;

namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Connectivity test harness for the external services. Run each test from the
    /// component context menu (right-click the component header) or the Inspector
    /// buttons. Running in Play Mode is recommended so async continuations resume on
    /// the main thread and TTS audio can be played.
    ///
    /// This does NOT belong to the dialogue pipeline; it only verifies that a single
    /// LLM / TTS / STT provider is reachable and parsing correctly.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/External Service Tester")]
    public class ExternalServiceTester : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Source of API keys and provider settings. Falls back to a sibling component if unset.")]
        [SerializeField] private AIServiceConfig config;

        [Tooltip("AudioSource used to play synthesized TTS audio.")]
        [SerializeField] private AudioSource audioSource;

        [Header("LLM Test")]
        [TextArea]
        [SerializeField] private string llmUserPrompt =
            "Greet an elementary student in one short English sentence.";

        [Header("TTS Test")]
        [SerializeField] private string ttsText = "Hello! What would you like to buy?";

        [Header("STT Test")]
        [Tooltip("WAV file under StreamingAssets/TestAudio used as STT input.")]
        [SerializeField] private string sttWavFileName = "test.wav";

        [Tooltip("When a TTS test succeeds, also write the audio to StreamingAssets/TestAudio/<sttWavFileName> so the STT test has valid input.")]
        [SerializeField] private bool saveTtsOutputForStt = true;

        public const string TestAudioFolder = "TestAudio";

        private AIServiceSettings Settings
        {
            get
            {
                if (config == null)
                {
                    config = GetComponent<AIServiceConfig>();
                }

                return config != null ? config.Settings : null;
            }
        }

        [ContextMenu("Test LLM")]
        public async void TestLLM()
        {
            await TestLLMAsync();
        }

        [ContextMenu("Test TTS")]
        public async void TestTTS()
        {
            await TestTTSAsync();
        }

        [ContextMenu("Test STT")]
        public async void TestSTT()
        {
            await TestSTTAsync();
        }

        public async Task TestLLMAsync()
        {
            AIServiceSettings settings = Settings;
            if (settings == null)
            {
                Debug.LogError("[ExternalServiceTester] No AIServiceConfig assigned.");
                return;
            }

            ILLMService service = ServiceFactory.CreateLLMService(settings);
            if (service == null)
            {
                return;
            }

            var messages = new List<LLMMessage>
            {
                LLMMessage.System(
                    "You are a test endpoint. Reply with strict JSON only, no Markdown, " +
                    "no code fences. Use the shape {\"message\": string}."),
                LLMMessage.User(llmUserPrompt)
            };

            // Second arg maps to LLMRequest.ForceJson (request strict JSON output).
            var request = new LLMRequest(messages, true);

            Debug.Log($"[ExternalServiceTester] LLM test starting (provider={settings.Llm.Provider}, model={settings.Llm.ResolveModel()})...");
            ServiceResult<string> result = await service.CompleteAsync(request, CancellationToken.None);

            if (!result.IsSuccess)
            {
                Debug.LogError($"[ExternalServiceTester] LLM test FAILED: {result}");
                return;
            }

            string content = result.Value;
            try
            {
                JToken parsed = JToken.Parse(content);
                Debug.Log($"[ExternalServiceTester] LLM test OK. Valid JSON returned:\n{parsed.ToString()}");
            }
            catch (Newtonsoft.Json.JsonException)
            {
                Debug.LogWarning(
                    $"[ExternalServiceTester] LLM responded but content is NOT strict JSON:\n{content}");
            }
        }

        public async Task TestTTSAsync()
        {
            AIServiceSettings settings = Settings;
            if (settings == null)
            {
                Debug.LogError("[ExternalServiceTester] No AIServiceConfig assigned.");
                return;
            }

            ITTSService service = ServiceFactory.CreateTTSService(settings);
            if (service == null)
            {
                return;
            }

            Debug.Log($"[ExternalServiceTester] TTS test starting (voice={settings.Tts.ResolveVoice()})...");
            ServiceResult<byte[]> result = await service.SynthesizeAsync(new TTSRequest(ttsText), CancellationToken.None);

            if (!result.IsSuccess)
            {
                Debug.LogError($"[ExternalServiceTester] TTS test FAILED: {result}");
                return;
            }

            Debug.Log($"[ExternalServiceTester] TTS test OK. Received {result.Value.Length} audio bytes.");

            if (saveTtsOutputForStt)
            {
                SaveTtsAudioForStt(result.Value);
            }

            AudioClip clip = WavUtility.ToAudioClip(result.Value, "TTSTestClip");
            if (clip == null)
            {
                Debug.LogError("[ExternalServiceTester] Failed to decode TTS audio to AudioClip.");
                return;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                Debug.LogWarning("[ExternalServiceTester] No AudioSource assigned; cannot play TTS audio.");
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ExternalServiceTester] Audio playback requires Play Mode.");
                return;
            }

            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log($"[ExternalServiceTester] Playing TTS clip ({clip.length:0.00}s).");
        }

        public async Task TestSTTAsync()
        {
            AIServiceSettings settings = Settings;
            if (settings == null)
            {
                Debug.LogError("[ExternalServiceTester] No AIServiceConfig assigned.");
                return;
            }

            string path = Path.Combine(Application.streamingAssetsPath, TestAudioFolder, sttWavFileName);
            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[ExternalServiceTester] STT test audio not found: {path}\n" +
                    "Add a short English WAV (PCM 16-bit mono 16kHz recommended) there, " +
                    "or run 'Test TTS' first with 'Save Tts Output For Stt' enabled to generate it.");
                return;
            }

            byte[] audioBytes;
            try
            {
                audioBytes = File.ReadAllBytes(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ExternalServiceTester] Failed to read STT test audio: {e.Message}");
                return;
            }

            ISTTService service = ServiceFactory.CreateSTTService(settings);
            if (service == null)
            {
                return;
            }

            Debug.Log($"[ExternalServiceTester] STT test starting (provider={settings.Stt.Provider}, {audioBytes.Length} bytes)...");
            var request = new STTRequest(audioBytes, settings.Stt.ResolveLanguage());
            ServiceResult<string> result = await service.TranscribeAsync(request, CancellationToken.None);

            if (!result.IsSuccess)
            {
                Debug.LogError($"[ExternalServiceTester] STT test FAILED: {result}");
                return;
            }

            Debug.Log($"[ExternalServiceTester] STT test OK. Transcript: \"{result.Value}\"");
        }

        private void SaveTtsAudioForStt(byte[] wavBytes)
        {
            try
            {
                string folder = Path.Combine(Application.streamingAssetsPath, TestAudioFolder);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, sttWavFileName);
                File.WriteAllBytes(path, wavBytes);
                Debug.Log($"[ExternalServiceTester] Saved TTS audio for STT input: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ExternalServiceTester] Could not save TTS audio for STT: {e.Message}");
            }
        }
    }
}
