using UnityEngine;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Minimal overhead label. Shows the NPC name and the proximity hint while the NPC
    /// is active, hides otherwise. Intentionally tiny: the real UI contract is the
    /// ActiveNPC events; this is only a debug/visual aid.
    ///
    /// Drives an optional legacy <see cref="TextMesh"/> (built-in, no package
    /// dependency) and/or toggles an optional visual root GameObject. With neither
    /// assigned it only logs.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Nameplate")]
    public class NPCNameplate : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("NPC whose activation drives the label. Auto-resolved from this object / parents when unset.")]
        [SerializeField] private NPCController controller;

        [Tooltip("Optional legacy 3D TextMesh used to render the label.")]
        [SerializeField] private TextMesh label;

        [Tooltip("Optional GameObject shown only while active (e.g. a world-space canvas root).")]
        [SerializeField] private GameObject visualRoot;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<NPCController>();
            }

            if (controller == null)
            {
                Debug.LogError($"[NPCNameplate] '{name}' found no NPCController; nameplate disabled.");
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.ActivationChanged += OnActivationChanged;
                OnActivationChanged(controller.IsActive);
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.ActivationChanged -= OnActivationChanged;
            }
        }

        private void OnActivationChanged(bool active)
        {
            if (active)
            {
                string text = $"{controller.NpcName}\n{controller.HintText}";
                if (label != null)
                {
                    label.text = text;
                }
                else
                {
                    Debug.Log($"[NPCNameplate] {text.Replace('\n', ' ')}");
                }
            }

            SetVisible(active);
        }

        private void SetVisible(bool visible)
        {
            if (label != null)
            {
                label.gameObject.SetActive(visible);
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(visible);
            }
        }
    }
}
