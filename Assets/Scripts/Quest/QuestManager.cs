using System;
using System.Collections.Generic;
using UnityEngine;
using MultiAgentNPC.Config;

namespace MultiAgentNPC.Quest
{
    /// <summary>
    /// Runtime state machine for the quest system (requirements doc section 10).
    /// Owns one <see cref="QuestRuntimeState"/> per configured quest and drives the
    /// transitions Inactive -> Active -> Completed.
    ///
    /// Behaviour:
    /// - The start quest is auto-activated on <see cref="Initialize"/>.
    /// - Activating a <see cref="QuestType.Composite"/> auto-activates its children.
    /// - Completing the last child of a composite auto-completes the parent (cascade).
    /// - Completing a quest auto-activates its <c>NextQuestID</c>.
    /// - A <see cref="QuestType.Composite"/> with no children never auto-completes.
    ///
    /// Plain C# class (no Unity dependency beyond logging) so it can be reused
    /// outside MonoBehaviours. Defensive: illegal transitions log and return false
    /// rather than throwing.
    /// </summary>
    public class QuestManager
    {
        private readonly ConfigManager _configManager;
        private readonly int _startQuestIdOverride;
        private readonly RuleQuestEvaluator _ruleEvaluator = new RuleQuestEvaluator();

        private readonly Dictionary<int, QuestRuntimeState> _states = new Dictionary<int, QuestRuntimeState>();

        /// <summary>Fired after every successful state transition.</summary>
        public event Action<QuestStateChangedEventArgs> QuestStateChanged;

        /// <summary>All quest runtime states, keyed by quest id.</summary>
        public IReadOnlyDictionary<int, QuestRuntimeState> States => _states;

        /// <summary>True once <see cref="Initialize"/> has run.</summary>
        public bool Initialized { get; private set; }

        /// <param name="configManager">Loaded config source for quest rows.</param>
        /// <param name="startQuestIdOverride">When non-zero, forces the start quest id; otherwise it is auto-detected.</param>
        public QuestManager(ConfigManager configManager, int startQuestIdOverride = 0)
        {
            _configManager = configManager;
            _startQuestIdOverride = startQuestIdOverride;
            BuildStates();
        }

        private void BuildStates()
        {
            _states.Clear();
            if (_configManager == null)
            {
                Debug.LogError("[QuestManager] ConfigManager is null; no quests will be tracked.");
                return;
            }

            foreach (var kvp in _configManager.QuestConfigs)
            {
                _states[kvp.Key] = new QuestRuntimeState(kvp.Value);
            }
        }

        /// <summary>
        /// Activates the start quest. Safe to call once after construction. Re-running
        /// is a no-op if it has already initialized.
        /// </summary>
        public void Initialize()
        {
            if (Initialized)
            {
                Debug.LogWarning("[QuestManager] Initialize called again; ignoring.");
                return;
            }

            Initialized = true;

            int startId = ResolveStartQuestId();
            if (startId == 0)
            {
                Debug.LogWarning("[QuestManager] No start quest could be determined.");
                return;
            }

            Debug.Log($"[QuestManager] Initializing with start quest {startId}.");
            TryActivateQuest(startId);
        }

        /// <summary>Current state of a quest, or <see cref="QuestState.Inactive"/> if unknown.</summary>
        public QuestState GetState(int questId)
        {
            return _states.TryGetValue(questId, out QuestRuntimeState state)
                ? state.State
                : QuestState.Inactive;
        }

        public bool TryGetRuntimeState(int questId, out QuestRuntimeState state)
        {
            return _states.TryGetValue(questId, out state);
        }

        /// <summary>All quests currently in <see cref="QuestState.Active"/>.</summary>
        public IEnumerable<QuestRuntimeState> GetActiveQuests()
        {
            foreach (var state in _states.Values)
            {
                if (state.State == QuestState.Active)
                {
                    yield return state;
                }
            }
        }

        /// <summary>
        /// Active <see cref="QuestType.TargetDialogue"/> quests targeting a given NPC.
        /// Implements the TargetNPCID filter so dialogue evaluation only fires for the
        /// NPC a quest is bound to.
        /// </summary>
        public IEnumerable<QuestRuntimeState> GetActiveTargetDialogueQuestsForNpc(int npcId)
        {
            foreach (var state in _states.Values)
            {
                if (state.State == QuestState.Active &&
                    state.Config.QuestType == QuestType.TargetDialogue &&
                    state.Config.TargetNpcId == npcId)
                {
                    yield return state;
                }
            }
        }

        /// <summary>
        /// Transitions a quest Inactive -> Active. Activating a composite also activates
        /// its child quests. Returns false for unknown ids or non-Inactive quests.
        /// </summary>
        public bool TryActivateQuest(int questId)
        {
            if (!_states.TryGetValue(questId, out QuestRuntimeState state))
            {
                Debug.LogWarning($"[QuestManager] Cannot activate unknown quest {questId}.");
                return false;
            }

            if (state.State != QuestState.Inactive)
            {
                Debug.LogWarning(
                    $"[QuestManager] Cannot activate quest {questId}: state is {state.State}, expected Inactive.");
                return false;
            }

            SetState(state, QuestState.Active);

            if (state.Config.QuestType == QuestType.Composite)
            {
                foreach (int childId in state.Config.GetChildQuestIds())
                {
                    if (_states.TryGetValue(childId, out QuestRuntimeState child) &&
                        child.State == QuestState.Inactive)
                    {
                        TryActivateQuest(childId);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Transitions a quest Active -> Completed, then runs follow-up automation:
        /// activate NextQuestID, and complete the parent composite if all its children
        /// are now complete. Returns false for unknown ids or non-Active quests.
        /// </summary>
        public bool TryCompleteQuest(int questId)
        {
            if (!_states.TryGetValue(questId, out QuestRuntimeState state))
            {
                Debug.LogWarning($"[QuestManager] Cannot complete unknown quest {questId}.");
                return false;
            }

            if (state.State == QuestState.Completed)
            {
                Debug.LogWarning($"[QuestManager] Quest {questId} is already Completed.");
                return false;
            }

            if (state.State != QuestState.Active)
            {
                Debug.LogWarning(
                    $"[QuestManager] Cannot complete quest {questId}: state is {state.State}, expected Active.");
                return false;
            }

            SetState(state, QuestState.Completed);

            ActivateNextQuest(state.Config);
            TryAutoCompleteParent(state.Config);

            return true;
        }

        private void ActivateNextQuest(QuestConfig quest)
        {
            int nextId = quest.NextQuestId;
            if (nextId == 0)
            {
                return;
            }

            if (!_states.TryGetValue(nextId, out QuestRuntimeState next))
            {
                Debug.LogWarning(
                    $"[QuestManager] Quest {quest.QuestId} NextQuestID {nextId} does not exist.");
                return;
            }

            if (next.State == QuestState.Inactive)
            {
                TryActivateQuest(nextId);
            }
        }

        private void TryAutoCompleteParent(QuestConfig quest)
        {
            int parentId = quest.ParentQuestId;
            if (parentId == 0)
            {
                return;
            }

            if (!_states.TryGetValue(parentId, out QuestRuntimeState parent))
            {
                Debug.LogWarning(
                    $"[QuestManager] Quest {quest.QuestId} ParentQuestID {parentId} does not exist.");
                return;
            }

            if (parent.State != QuestState.Active)
            {
                return;
            }

            var childStates = new Dictionary<int, QuestState>();
            foreach (int childId in parent.Config.GetChildQuestIds())
            {
                childStates[childId] = GetState(childId);
            }

            QuestEvalResult verdict = _ruleEvaluator.Evaluate(
                new QuestEvalRequest(parent.Config, childStates: childStates));

            if (verdict.IsSuccess && verdict.IsCompleted)
            {
                Debug.Log(
                    $"[QuestManager] All children of composite quest {parentId} are complete ({verdict.Reason}).");
                TryCompleteQuest(parentId);
            }
        }

        private void SetState(QuestRuntimeState state, QuestState newState)
        {
            QuestState previous = state.State;
            state.State = newState;

            var args = new QuestStateChangedEventArgs(state.Config, previous, newState);
            Debug.Log($"[QuestManager] {args}");

            try
            {
                QuestStateChanged?.Invoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestManager] A QuestStateChanged subscriber threw: {e}");
            }
        }

        /// <summary>
        /// Resolves the start quest: the override when set, otherwise the first root
        /// quest (ParentQuestID == 0) that no other quest references via NextQuestID,
        /// preferring the lowest SortOrder then lowest QuestID for determinism.
        /// </summary>
        private int ResolveStartQuestId()
        {
            if (_startQuestIdOverride != 0)
            {
                if (_states.ContainsKey(_startQuestIdOverride))
                {
                    return _startQuestIdOverride;
                }

                Debug.LogWarning(
                    $"[QuestManager] startQuestIdOverride {_startQuestIdOverride} is not a known quest; auto-detecting instead.");
            }

            var referencedAsNext = new HashSet<int>();
            foreach (var state in _states.Values)
            {
                if (state.Config.NextQuestId != 0)
                {
                    referencedAsNext.Add(state.Config.NextQuestId);
                }
            }

            QuestConfig best = null;
            foreach (var state in _states.Values)
            {
                QuestConfig quest = state.Config;
                if (quest.ParentQuestId != 0 || referencedAsNext.Contains(quest.QuestId))
                {
                    continue;
                }

                if (best == null ||
                    quest.SortOrder < best.SortOrder ||
                    (quest.SortOrder == best.SortOrder && quest.QuestId < best.QuestId))
                {
                    best = quest;
                }
            }

            return best != null ? best.QuestId : 0;
        }
    }
}
