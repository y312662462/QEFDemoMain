using System;
using System.Threading;

namespace MultiAgentNPC.Dialogue
{
    /// <summary>
    /// Identity + cancellation handle for a single dialogue turn (Sprint 7). Every turn
    /// runs under exactly one session: the <see cref="Id"/> uniquely identifies it and
    /// the owned <see cref="CancellationTokenSource"/> is tripped when the player leaves
    /// the NPC's range (or switches NPC). Side effects (UI, subtitles, History, future
    /// task results) must be gated on BOTH the <see cref="Token"/> and on the session
    /// still being the host's current session, so a late LLM/TTS return from an expired
    /// session can never pollute a newer turn.
    ///
    /// Plain C# (no Unity dependency) so it can be unit-driven and owned by the host.
    /// </summary>
    public sealed class DialogueSession : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _disposed;

        /// <summary>Monotonic, host-assigned id. Unique per turn within a session counter.</summary>
        public int Id { get; }

        public DialogueSession(int id)
        {
            Id = id;
        }

        /// <summary>The cancellation token for this turn's async work.</summary>
        public CancellationToken Token => _cts.Token;

        /// <summary>True once this session has been cancelled (or disposed).</summary>
        public bool IsCancelled
        {
            get
            {
                if (_disposed)
                {
                    return true;
                }

                try
                {
                    return _cts.IsCancellationRequested;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Requests cancellation of this turn. Safe to call multiple times and after
        /// disposal; never throws to the caller.
        /// </summary>
        public void Cancel()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed; nothing to cancel.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Dispose();
        }

        public override string ToString() => $"DialogueSession#{Id}{(IsCancelled ? " [cancelled]" : string.Empty)}";
    }
}
