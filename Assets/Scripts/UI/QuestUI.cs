using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using MultiAgentNPC.Quest;

namespace MultiAgentNPC.UI
{
    /// <summary>
    /// Top-left quest tracker (Sprint 5). Binds to a live <see cref="QuestManager"/>
    /// (supplied by <see cref="QuestRuntimeHost"/> this sprint) and re-renders on every
    /// <see cref="QuestManager.QuestStateChanged"/>.
    ///
    /// Display rules:
    /// - Show the first Active root quest (ParentQuestId == 0 and ShowInUI), ordered by
    ///   SortOrder then QuestId.
    /// - If that quest is Composite, list its configured children as subtasks.
    /// - If no Active root quest exists, show the first Active ShowInUI quest.
    /// - Completed quests are hidden unless <see cref="showCompletedQuests"/> is set.
    ///
    /// It never reaches into QuestManager internals beyond the public read API and never
    /// depends on the debug QuestSystemTester.
    /// </summary>
    [AddComponentMenu("MultiAgentNPC/UI/Quest UI")]
    public class QuestUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Single text block rendering the current quest and its subtasks.")]
        [SerializeField] private TMP_Text questText;

        [Tooltip("Root toggled active while there is a quest to show. Defaults to this GameObject.")]
        [SerializeField] private GameObject root;

        [Tooltip("Quest runtime owner to bind to. Auto-found in the scene when left empty.")]
        [SerializeField] private QuestRuntimeHost questHost;

        [Header("Display")]
        [Tooltip("Also show Completed quests instead of hiding them.")]
        [SerializeField] private bool showCompletedQuests;

        private QuestManager _manager;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }

        private void OnEnable()
        {
            if (questHost == null)
            {
                questHost = FindFirstObjectByType<QuestRuntimeHost>();
            }

            if (questHost != null)
            {
                questHost.QuestManagerInitialized += OnQuestManagerInitialized;
                // Cover the case where the host already initialized before we enabled.
                if (questHost.QuestManager != null)
                {
                    Bind(questHost.QuestManager);
                }
            }
            else
            {
                Debug.LogWarning(
                    "[QuestUI] No QuestRuntimeHost found and none assigned; quest UI stays empty. " +
                    "Assign one or call Bind(QuestManager).");
                Render();
            }
        }

        private void OnDisable()
        {
            if (questHost != null)
            {
                questHost.QuestManagerInitialized -= OnQuestManagerInitialized;
            }

            Unsubscribe();
        }

        /// <summary>
        /// Binds to a quest manager, replacing any previous binding, and renders. Public
        /// so future production wiring can supply the manager without a host.
        /// </summary>
        public void Bind(QuestManager manager)
        {
            if (_manager == manager)
            {
                Render();
                return;
            }

            Unsubscribe();
            _manager = manager;

            if (_manager != null)
            {
                _manager.QuestStateChanged += OnQuestStateChanged;
                Debug.Log($"[QuestUI] Bound to QuestManager (instance {_manager.GetHashCode()}).");
            }

            Render();
        }

        private void OnQuestManagerInitialized(QuestManager manager)
        {
            Bind(manager);
        }

        private void Unsubscribe()
        {
            if (_manager != null)
            {
                _manager.QuestStateChanged -= OnQuestStateChanged;
                _manager = null;
            }
        }

        private void OnQuestStateChanged(QuestStateChangedEventArgs args)
        {
            Render();
        }

        private void Render()
        {
            QuestRuntimeState current = ResolveDisplayedQuest();

            if (current == null)
            {
                if (questText != null)
                {
                    questText.text = string.Empty;
                }

                SetVisible(false);
                return;
            }

            var sb = new StringBuilder();
            sb.Append(current.Config.QuestName);

            if (current.Config.QuestType == QuestType.Composite)
            {
                AppendSubtasks(sb, current.Config);
            }

            if (questText != null)
            {
                questText.text = sb.ToString();
            }
            else
            {
                Debug.Log($"[QuestUI] {sb.ToString().Replace('\n', ' ')}");
            }

            SetVisible(true);
        }

        /// <summary>
        /// Picks the quest to display per the Sprint 5 rules: the first Active root
        /// quest (ShowInUI), else the first Active ShowInUI quest, ordered by SortOrder
        /// then QuestId. Completed quests are considered only when enabled.
        /// </summary>
        private QuestRuntimeState ResolveDisplayedQuest()
        {
            if (_manager == null)
            {
                return null;
            }

            QuestRuntimeState bestRoot = null;
            QuestRuntimeState bestAny = null;

            foreach (QuestRuntimeState state in _manager.States.Values)
            {
                QuestConfig config = state.Config;
                if (config == null || !config.ShowInUI)
                {
                    continue;
                }

                if (!IsDisplayable(state.State))
                {
                    continue;
                }

                if (IsHigherPriority(state, bestAny))
                {
                    bestAny = state;
                }

                if (config.ParentQuestId == 0 && IsHigherPriority(state, bestRoot))
                {
                    bestRoot = state;
                }
            }

            return bestRoot ?? bestAny;
        }

        private bool IsDisplayable(QuestState state)
        {
            if (state == QuestState.Active)
            {
                return true;
            }

            return showCompletedQuests && state == QuestState.Completed;
        }

        private static bool IsHigherPriority(QuestRuntimeState candidate, QuestRuntimeState current)
        {
            if (current == null)
            {
                return true;
            }

            QuestConfig a = candidate.Config;
            QuestConfig b = current.Config;
            if (a.SortOrder != b.SortOrder)
            {
                return a.SortOrder < b.SortOrder;
            }

            return a.QuestId < b.QuestId;
        }

        private void AppendSubtasks(StringBuilder sb, QuestConfig parent)
        {
            List<int> childIds = parent.GetChildQuestIds();
            foreach (int childId in childIds)
            {
                if (!_manager.TryGetRuntimeState(childId, out QuestRuntimeState child))
                {
                    sb.Append($"\n  [?] (missing quest {childId})");
                    continue;
                }

                string marker = child.State == QuestState.Completed ? "[x]" : "[ ]";
                string name = child.Config != null ? child.Config.QuestName : $"Quest {childId}";
                sb.Append($"\n  {marker} {name}");
            }
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root != gameObject)
            {
                root.SetActive(visible);
            }
            else if (questText != null)
            {
                questText.gameObject.SetActive(visible);
            }
        }
    }
}
