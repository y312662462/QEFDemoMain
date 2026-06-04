using UnityEngine;

namespace MultiAgentNPC.NPC
{
    /// <summary>
    /// Smoothly yaws the NPC to face the player while it is the ActiveNPC, and returns
    /// to its cached default rotation once inactive. Rotation is constrained to the Y
    /// axis (yaw only) so the NPC never tilts.
    ///
    /// Reads the target player from the sibling <see cref="NPCController.CurrentPlayerTransform"/>
    /// and toggles on <see cref="NPCController.ActivationChanged"/>.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/NPC Facing Controller")]
    public class NPCFacingController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("NPC whose activation drives facing. Auto-resolved from this object / parents when unset.")]
        [SerializeField] private NPCController controller;

        [Tooltip("Transform to rotate. Defaults to this GameObject's transform.")]
        [SerializeField] private Transform pivotOverride;

        [Header("Tuning")]
        [Tooltip("Degrees-per-second of the smoothing slerp.")]
        [SerializeField] private float rotationSpeed = 360f;

        [Tooltip("Stop rotating when within this many degrees of the target (avoids jitter).")]
        [SerializeField] private float angleDeadzone = 0.5f;

        private Transform _pivot;
        private Quaternion _defaultRotation;
        private bool _active;

        private void Awake()
        {
            _pivot = pivotOverride != null ? pivotOverride : transform;
            _defaultRotation = _pivot.rotation;

            if (controller == null)
            {
                controller = GetComponentInParent<NPCController>();
            }

            if (controller == null)
            {
                Debug.LogError(
                    $"[NPCFacingController] '{name}' found no NPCController; facing will be disabled.");
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.ActivationChanged += OnActivationChanged;
                _active = controller.IsActive;
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
            _active = active;
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            Quaternion target = _active ? ComputeFacingTarget() : _defaultRotation;

            float step = rotationSpeed * Time.deltaTime;
            if (Quaternion.Angle(_pivot.rotation, target) <= angleDeadzone)
            {
                _pivot.rotation = target;
                return;
            }

            _pivot.rotation = Quaternion.RotateTowards(_pivot.rotation, target, step);
        }

        private Quaternion ComputeFacingTarget()
        {
            Transform player = controller.CurrentPlayerTransform;
            if (player == null)
            {
                return _defaultRotation;
            }

            Vector3 toPlayer = player.position - _pivot.position;
            toPlayer.y = 0f; // yaw only

            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return _pivot.rotation;
            }

            return Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }
    }
}
