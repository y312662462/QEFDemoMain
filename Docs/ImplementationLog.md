# Implementation Log

Chronological log of what was implemented each sprint. Newest entries on top.

## Sprint 0 - Collaboration Baseline, Directory Structure & Cursor Constraints (2026-06-03)

Goal: establish the engineering baseline. No business logic.

Done:

- Created `Assets/Scripts/` module folders: `Core`, `Config`, `NPC`, `Dialogue`, `Services`,
  `Quest`, `UI`, `Audio`, `Animation`, `Input`, `DebugTools`, `Utils` (placeholders via `.gitkeep`).
- Created `Assets/StreamingAssets/Config/` and `Assets/StreamingAssets/Prompts/{System,NPC,QuestEval,Shared}/`
  (placeholders via `.gitkeep`).
- Added empty entry points under `MultiAgentNPC.Core`:
  - `Assets/Scripts/Core/GameBootstrap.cs` (empty `MonoBehaviour`).
  - `Assets/Scripts/Core/RuntimeContext.cs` (empty plain class).
- Created `Docs/` drafts: `Architecture.md`, `ImplementationLog.md`, `TestPlan.md`, `CursorWorkflows.md`.

Decisions:

- Config/Prompts live under `StreamingAssets/` (not `Assets/Config` / `Assets/Prompts` as in the doc).
  See `Architecture.md` for rationale.
- No `.asmdef` this sprint; default `Assembly-CSharp` is used.

Not done (out of scope, by design):

- No NPC/Quest/Dialogue/LLM/TTS/STT business logic.
- No scene/prefab/material/third-party asset changes.
- No API keys, no external API calls.
