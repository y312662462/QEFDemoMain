using System;
using UnityEngine;

namespace MultiAgentNPC.DebugTools
{
    /// <summary>
    /// Lightweight static hub for debug-driven input events (Sprint 5). Decouples the
    /// <c>DebugInputBox</c> (publisher) from any later consumer (e.g. the
    /// DialoguePipeline) so neither needs a scene reference to the other.
    ///
    /// This sprint only raises <see cref="DebugTextSubmitted"/>; no consumer is wired
    /// to a pipeline yet.
    /// </summary>
    public static class DebugEvents
    {
        /// <summary>Raised when the debug input box submits a line of text.</summary>
        public static event Action<string> DebugTextSubmitted;

        /// <summary>
        /// Broadcasts <paramref name="text"/> to every <see cref="DebugTextSubmitted"/>
        /// subscriber. Blank input is ignored. Subscriber exceptions are isolated.
        /// </summary>
        public static void Raise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Action<string> handlers = DebugTextSubmitted;
            if (handlers == null)
            {
                Debug.Log($"[DebugEvents] DebugTextSubmitted (no subscribers): '{text}'");
                return;
            }

            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(text);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DebugEvents] A DebugTextSubmitted subscriber threw: {e}");
                }
            }
        }
    }
}
