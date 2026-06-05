using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MultiAgentNPC.InputControl
{
    /// <summary>
    /// Push-To-Talk input wrapper (Sprint 8). Honors the input-inversion rule
    /// (Architecture 18.4): business modules never read raw input - they subscribe to
    /// <see cref="TalkStarted"/> / <see cref="TalkEnded"/> instead.
    ///
    /// Uses the Unity Input System Talk action when available. The whole file compiles
    /// with or without <c>ENABLE_INPUT_SYSTEM</c>: no <c>UnityEngine.InputSystem</c> type
    /// is referenced outside the guarded blocks. When the package is unavailable,
    /// <see cref="IsAvailable"/> stays false, a clear install warning is logged, and
    /// push-to-talk simply does nothing (the rest of the game, including Debug text input,
    /// keeps working).
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/Input/Push To Talk Input Controller")]
    public class PushToTalkInputController : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("Input System control path bound to the Talk action.")]
        [SerializeField] private string talkBinding = "<Keyboard>/space";

        /// <summary>Raised when the Talk control is pressed (recording should start).</summary>
        public event Action TalkStarted;

        /// <summary>Raised when the Talk control is released (recording should stop).</summary>
        public event Action TalkEnded;

        /// <summary>True when push-to-talk is actually wired (Input System present + enabled).</summary>
        public bool IsAvailable { get; private set; }

#if ENABLE_INPUT_SYSTEM
        private InputAction _talkAction;
#endif

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            _talkAction = new InputAction(
                name: "Talk",
                type: InputActionType.Button,
                binding: string.IsNullOrWhiteSpace(talkBinding) ? "<Keyboard>/space" : talkBinding);
            _talkAction.started += OnActionStarted;
            _talkAction.canceled += OnActionCanceled;
            _talkAction.Enable();
            IsAvailable = true;
#else
            IsAvailable = false;
            Debug.LogWarning(
                "[PushToTalkInputController] Unity Input System (com.unity.inputsystem) is not enabled. " +
                "Install it via Window > Package Manager and set Player > Active Input Handling to " +
                "'Both' or 'Input System Package'. Push-To-Talk is disabled; Debug text input still works.");
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_talkAction != null)
            {
                _talkAction.started -= OnActionStarted;
                _talkAction.canceled -= OnActionCanceled;
                _talkAction.Disable();
                _talkAction.Dispose();
                _talkAction = null;
            }
#endif
            IsAvailable = false;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnActionStarted(InputAction.CallbackContext context) => RaiseStarted();
        private void OnActionCanceled(InputAction.CallbackContext context) => RaiseEnded();
#endif

        private void RaiseStarted()
        {
            try
            {
                TalkStarted?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PushToTalkInputController] A TalkStarted subscriber threw: {e}");
            }
        }

        private void RaiseEnded()
        {
            try
            {
                TalkEnded?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PushToTalkInputController] A TalkEnded subscriber threw: {e}");
            }
        }
    }
}
