using TMPro;
using UnityEngine;
using MultiAgentNPC.DebugTools;

namespace MultiAgentNPC.UI
{
    /// <summary>
    /// Debug text entry (Sprint 5). Wraps a single-line <see cref="TMP_InputField"/>;
    /// pressing Enter broadcasts the text via <see cref="DebugEvents.Raise"/> so later
    /// modules (e.g. the DialoguePipeline) can subscribe. No pipeline is connected yet.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/UI/Debug Input Box")]
    public class DebugInputBox : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Single-line input field the player types debug text into.")]
        [SerializeField] private TMP_InputField inputField;

        [Header("Behaviour")]
        [Tooltip("Clear the field after a successful submit.")]
        [SerializeField] private bool clearOnSubmit = true;

        [Tooltip("Also mirror the submitted text into the DebugStateStore (last STT slot).")]
        [SerializeField] private bool echoToDebugState = true;

        private void Awake()
        {
            if (inputField == null)
            {
                inputField = GetComponent<TMP_InputField>();
            }
        }

        private void OnEnable()
        {
            if (inputField == null)
            {
                Debug.LogWarning("[DebugInputBox] No TMP_InputField assigned; debug input disabled.");
                return;
            }

            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.onSubmit.AddListener(OnSubmit);
        }

        private void OnDisable()
        {
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(OnSubmit);
            }
        }

        private void OnSubmit(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DebugEvents.Raise(text);

            if (echoToDebugState)
            {
                DebugStateStore.Instance.SetLastSttText(text);
            }

            if (clearOnSubmit && inputField != null)
            {
                inputField.text = string.Empty;
                inputField.ActivateInputField();
            }
        }
    }
}
