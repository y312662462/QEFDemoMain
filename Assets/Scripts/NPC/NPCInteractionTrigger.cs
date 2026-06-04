using UnityEngine;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Proximity trigger for one NPC. Intended to live on a child GameObject named
    /// "InteractionTrigger" carrying a trigger <see cref="SphereCollider"/> (and
    /// optionally a kinematic, gravity-off <see cref="Rigidbody"/> for stable trigger
    /// events). Detects the player strictly by the "Player" tag and forwards range
    /// enter/exit to the owning <see cref="NPCController"/>.
    ///
    /// The sphere radius is sized at runtime from the controller's effective interaction
    /// radius so designers only set it once in NPCConfig.csv (or the Inspector override).
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Interaction Trigger")]
    [RequireComponent(typeof(SphereCollider))]
    public class NPCInteractionTrigger : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Tag the entering collider must have to count as the player.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Owning NPC. Auto-resolved from this object / its parents when unset.")]
        [SerializeField] private NPCController controller;

        [Tooltip("Apply the controller's effective radius to the SphereCollider on Start.")]
        [SerializeField] private bool applyConfiguredRadius = true;

        private SphereCollider _collider;

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;

            if (controller == null)
            {
                controller = GetComponentInParent<NPCController>();
            }

            if (controller == null)
            {
                Debug.LogError(
                    $"[NPCInteractionTrigger] '{name}' found no NPCController on itself or its parents. " +
                    "Place this on a child of an NPC that has an NPCController, or assign one.");
            }
        }

        private void Start()
        {
            if (applyConfiguredRadius && controller != null)
            {
                float radius = controller.InteractionRadius;
                if (radius > 0f)
                {
                    _collider.radius = radius;
                }
                else
                {
                    Debug.LogWarning(
                        $"[NPCInteractionTrigger] NPC {controller.NpcId} has no positive interaction radius; " +
                        $"keeping the SphereCollider radius ({_collider.radius}).");
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (controller == null || !other.CompareTag(playerTag))
            {
                return;
            }

            controller.OnPlayerEnteredRange(other.transform);
        }

        private void OnTriggerExit(Collider other)
        {
            if (controller == null || !other.CompareTag(playerTag))
            {
                return;
            }

            controller.OnPlayerExitedRange(other.transform);
        }
    }
}
