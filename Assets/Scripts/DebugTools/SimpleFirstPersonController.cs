using UnityEngine;

namespace MultiAgentNPC.DebugTools
{
    /// <summary>
    /// Minimal first-person player controller used for testing NPC proximity triggers.
    /// Self-contained, uses the classic Input manager (Input.GetAxis / Input.GetKey)
    /// so it works without the new Input System package.
    ///
    /// SCENE SETUP:
    ///   1. Create an empty GameObject named "Player".
    ///   2. Add a CharacterController component to it (added automatically via
    ///      [RequireComponent], but you can tune its Center / Height / Radius).
    ///   3. Add this script (SimpleFirstPersonController) to the Player.
    ///   4. Create a child Camera named "PlayerCamera" (delete the default scene
    ///      Main Camera or remove its AudioListener to avoid duplicate listeners).
    ///   5. Assign PlayerCamera's Transform to the "cameraTransform" field in the
    ///      inspector. If left empty, the controller searches its children for a
    ///      Camera and falls back to Camera.main.
    ///   6. Set the Player GameObject's tag to "Player".
    ///
    /// NPC TRIGGER DETECTION:
    ///   - The CharacterController acts as a collider, so NPC trigger volumes
    ///     (a Collider with "Is Trigger" enabled) will detect this Player through
    ///     OnTriggerEnter / OnTriggerStay / OnTriggerExit using CompareTag("Player").
    ///   - A Rigidbody is NOT required here: a moving CharacterController is treated
    ///     by Unity as a kinematic body and still fires trigger callbacks against
    ///     static trigger colliders. Only add a Rigidbody if your NPC triggers rely
    ///     on Rigidbody-based collision in your specific scene; this controller does
    ///     not add or depend on one.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Horizontal move speed in meters per second.")]
        public float moveSpeed = 5f;

        [Tooltip("Mouse look sensitivity multiplier.")]
        public float mouseSensitivity = 2f;

        [Tooltip("Downward gravity acceleration (positive value, applied as -gravity).")]
        public float gravity = 9.81f;

        [Header("References")]
        [Tooltip("Camera transform used for pitch (look up/down). If empty, a child Camera or Camera.main is used.")]
        public Transform cameraTransform;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _cameraPitch;

        private const float PitchMin = -80f;
        private const float PitchMax = 80f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (cameraTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                cameraTransform = childCamera != null ? childCamera.transform
                    : (Camera.main != null ? Camera.main.transform : null);
            }
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleMouseLook();
            HandleMovement();
        }

        private void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(false);
            }
            // Click back into the game view to re-lock the cursor.
            else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                LockCursor(true);
            }
        }

        private void HandleMouseLook()
        {
            // Ignore look input while the cursor is unlocked (e.g. after pressing Escape).
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Yaw rotates the whole body so movement follows where we look.
            transform.Rotate(Vector3.up, mouseX);

            // Pitch only tilts the camera, clamped to avoid flipping over.
            if (cameraTransform != null)
            {
                _cameraPitch = Mathf.Clamp(_cameraPitch - mouseY, PitchMin, PitchMax);
                cameraTransform.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 move = (transform.right * horizontal + transform.forward * vertical);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            // Apply gravity on the vertical axis. No jump: the Space key is reserved
            // for a separate (not implemented here) push-to-record voice input.
            if (_controller.isGrounded)
            {
                // Small downward force keeps isGrounded stable while grounded.
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 velocity = move * moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
