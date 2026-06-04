# Test Plan (Draft)

Verification checklist, grown per sprint.

## Sprint 0 - Baseline verification

1. Open the project in Unity 6 (`6000.3.15f1`) and let it finish importing.
2. Confirm the Console shows 0 compile errors and 0 new warnings from the added scripts.
3. In the Project window confirm the 12 module folders exist under `Assets/Scripts/`:
   `Core`, `Config`, `NPC`, `Dialogue`, `Services`, `Quest`, `UI`, `Audio`, `Animation`,
   `Input`, `DebugTools`, `Utils`.
4. Confirm `Assets/StreamingAssets/Config/` and `Assets/StreamingAssets/Prompts/{System,NPC,QuestEval,Shared}/` exist.
5. Confirm `Docs/` contains `Architecture.md`, `ImplementationLog.md`, `TestPlan.md`, `CursorWorkflows.md`.
6. Create an empty GameObject in a sandbox scene and confirm `GameBootstrap` can be added as a
   component (proves it compiled as a `MonoBehaviour`). Do not save the scene.
7. Confirm no existing scenes, prefabs, materials, or `Assets/Tools/ResourceTools/StandardMaterialConvert.cs`
   were modified.

## Sprint 3 - Quest system verification

1. Open `Assets/Scenes/DemoMain.unity`, add an empty GameObject and add the `QuestSystemTester`
   and `AIServiceConfig` components. Enter your LLM API key / provider settings in the Inspector.
2. Enter Play Mode. Confirm the Console logs all quests `Inactive` and the start quest `10001`
   transitions to `Active` (with a `QuestStateChanged` event log).
3. Run ContextMenu `Complete Quest (questIdField)` with `questIdField = 10001`: expect
   `10001 Completed`, `10002 Active`, and children `10003 / 10004 / 10005 Active`.
4. Complete `10003`, then `10004`, then `10005` (set `questIdField` each time): after the last child,
   expect `10002` to auto-`Completed` and `10006` to become `Active`.
5. Run `Init Quest System` to reset, then `Evaluate Dialogue Quest via LLM` with `npcIdField = 10001`
   and `playerTextField = "I want an apple, please."`: expect a real LLM JSON verdict logged and,
   if `isCompleted = true`, quest `10003` becomes `Completed`.
6. Confirm the TargetNPCID filter: with `npcIdField` set to a non-matching NPC, the evaluator reports
   "No active TargetDialogue quest for NPC ...". Confirm 0 compile errors and 0 new warnings.

## Future sprints

- Dialogue pipeline, NPC system, Quest system, Services, UI, Debug panel test cases to be added
  as those features land. See requirements doc section 19 (MVP acceptance criteria).
