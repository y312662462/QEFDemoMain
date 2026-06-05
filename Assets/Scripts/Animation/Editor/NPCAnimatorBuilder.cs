using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MultiAgentNPC.Animation.EditorTools
{
    /// <summary>
    /// One-click builder (Sprint 10) for a FLAT NPC Animator Controller that matches the
    /// built-in default action table (<see cref="ActionIdMapper.BuildDefaultTable"/>).
    ///
    /// It creates a single-layer controller with an <c>Idle</c> default state plus one state
    /// per action, wires each action state back to Idle via an exit-time transition, and
    /// assigns the matching Layer Lab clip to every state. Because the runtime drives actions
    /// with <c>Animator.CrossFade(stateName)</c>, no Animator parameters/triggers are needed.
    ///
    /// Menu: <c>MultiAgentNPC &gt; Build NPC Animator Controller</c>.
    /// </summary>
    public static class NPCAnimatorBuilder
    {
        private const string ClipFolder = "Assets/Resouce/Layer Lab/3D Casual Character/Animation";
        private const string OutputPath = "Assets/Resouce/Character/NPC_ActionController.controller";

        [MenuItem("MultiAgentNPC/Build NPC Animator Controller")]
        public static void Build()
        {
            string dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            // Recreate from scratch so re-running gives a clean, deterministic result.
            AssetDatabase.DeleteAsset(OutputPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            int idleActionId = ActionIdMapper.DefaultIdleActionId;
            var missing = new List<string>();
            AnimatorState idleState = null;
            var actionStates = new List<AnimatorState>();

            foreach (ActionDefinition def in ActionIdMapper.BuildDefaultTable())
            {
                if (def == null || string.IsNullOrEmpty(def.stateName))
                {
                    continue;
                }

                AnimationClip clip = FindClipForState(def.stateName);
                if (clip == null)
                {
                    missing.Add(def.stateName);
                }

                AnimatorState state = sm.AddState(def.stateName);
                state.motion = clip;

                if (def.actionId == idleActionId)
                {
                    idleState = state;
                }
                else
                {
                    actionStates.Add(state);
                }
            }

            if (idleState == null)
            {
                // No 1001 row resolved to a clip/state: synthesise a bare Idle so the
                // controller still has a sane default and return target.
                idleState = sm.AddState("Idle");
            }

            sm.defaultState = idleState;

            // Each action plays once (CrossFade by name) then returns to Idle on exit time.
            foreach (AnimatorState action in actionStates)
            {
                AnimatorStateTransition toIdle = action.AddTransition(idleState);
                toIdle.hasExitTime = true;
                toIdle.exitTime = 0.9f;
                toIdle.hasFixedDuration = true;
                toIdle.duration = 0.2f;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(controller);
            Selection.activeObject = controller;

            Debug.Log(
                $"[NPCAnimatorBuilder] Built '{OutputPath}' with {actionStates.Count + 1} states " +
                $"(default '{idleState.name}'). Assign it to each NPC's Animator, then add " +
                "NPCAnimationController + ExpressionController to the prefab.");

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    "[NPCAnimatorBuilder] No clip found for: " + string.Join(", ", missing) +
                    $". Those states were created empty - check the FBX names under '{ClipFolder}'.");
            }
        }

        /// <summary>
        /// Resolves the Layer Lab clip for a state name. Idle uses <c>Anim@Stand_Idle1</c>;
        /// every other state maps to <c>Anim@&lt;stateName&gt;</c>. Tries both .FBX/.fbx.
        /// </summary>
        private static AnimationClip FindClipForState(string stateName)
        {
            string baseName = stateName == "Idle" ? "Anim@Stand_Idle1" : "Anim@" + stateName;

            foreach (string ext in new[] { ".FBX", ".fbx" })
            {
                string path = $"{ClipFolder}/{baseName}{ext}";
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null)
                {
                    continue;
                }

                foreach (Object obj in assets)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }
    }
}
