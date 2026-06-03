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

## Future sprints

- Dialogue pipeline, NPC system, Quest system, Services, UI, Debug panel test cases to be added
  as those features land. See requirements doc section 19 (MVP acceptance criteria).
