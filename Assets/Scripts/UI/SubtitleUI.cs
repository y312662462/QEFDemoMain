using TMPro;
using UnityEngine;

namespace MultiAgentNPC.UI
{
    /// <summary>
    /// Minimal subtitle surface (Sprint 5). Shows the player's text, the NPC's current
    /// sentence, and error messages. Updated only through public methods so business
    /// modules never touch the underlying TMP labels.
    ///
    /// Intentionally simple this sprint: no fade animations or visual polish.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/UI/Subtitle UI")]
    public class SubtitleUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Label for the player's recognized/typed text.")]
        [SerializeField] private TMP_Text playerLine;

        [Tooltip("Label for the NPC's current sentence.")]
        [SerializeField] private TMP_Text npcLine;

        [Tooltip("Label for error messages.")]
        [SerializeField] private TMP_Text errorLine;

        [Tooltip("Root toggled active whenever any line has content. Defaults to this GameObject.")]
        [SerializeField] private GameObject root;

        [Header("Style")]
        [Tooltip("Color applied to the error line.")]
        [SerializeField] private Color errorColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (errorLine != null)
            {
                errorLine.color = errorColor;
            }

            ClearInternal();
        }

        /// <summary>Sets the player line. Empty/blank clears it.</summary>
        public void ShowPlayerText(string text)
        {
            SetLine(playerLine, text);
            RefreshVisibility();
        }

        /// <summary>Sets the NPC sentence line. Empty/blank clears it.</summary>
        public void ShowNpcSentence(string text)
        {
            SetLine(npcLine, text);
            RefreshVisibility();
        }

        /// <summary>Sets the error line. Empty/blank clears it.</summary>
        public void ShowError(string text)
        {
            SetLine(errorLine, text);
            RefreshVisibility();
        }

        /// <summary>Clears every subtitle line and hides the root.</summary>
        public void Clear()
        {
            ClearInternal();
        }

        private void ClearInternal()
        {
            SetLine(playerLine, string.Empty);
            SetLine(npcLine, string.Empty);
            SetLine(errorLine, string.Empty);
            RefreshVisibility();
        }

        private static void SetLine(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private void RefreshVisibility()
        {
            if (root == null || root == gameObject)
            {
                return;
            }

            bool hasContent =
                HasContent(playerLine) || HasContent(npcLine) || HasContent(errorLine);
            root.SetActive(hasContent);
        }

        private static bool HasContent(TMP_Text label)
        {
            return label != null && !string.IsNullOrEmpty(label.text);
        }
    }
}
