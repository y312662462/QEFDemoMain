# Implementation Log

Chronological log of what was implemented each sprint. Newest entries on top.

## Sprint 8 - Microphone Recording, Push-To-Talk & Real STT (2026-06-05)

Sprint-order note: the original roadmap placed ActionID-driven animation before real voice
input. That order was adjusted - real STT (microphone recording + Push-To-Talk + ISTTService)
is implemented in this Sprint 8, BEFORE ActionID animation, which is deferred to a later sprint.

Goal: feed real voice input into the existing Sprint 6/7 dialogue flow. Hold Space to record,
release to stop, encode to WAV, transcribe via the real `ISTTService`, and submit the text
through the same `DialogueManager.SubmitPlayerText` entry as Debug text. No changes to the
LLM/TTS flow, `DialoguePipeline`, quest judgment, or UI layout. Debug text input is retained.

Done:

- `Assets/Scripts/Utils/WavUtility.cs`: added `EncodeToWav16` (float samples -> 16-bit PCM
  RIFF/WAVE bytes) that writes the ACTUAL clip frequency and channel count into the header.
- `Assets/Scripts/Input/PushToTalkInputController.cs`: Input System Talk action wrapper raising
  `TalkStarted`/`TalkEnded`. Compiles with or without `ENABLE_INPUT_SYSTEM`; when the package is
  absent it logs an install warning and disables push-to-talk gracefully.
- `Assets/Scripts/Audio/MicrophoneRecorder.cs`: async-permission-aware capture (16 kHz mono
  preferred via `Microphone.GetDeviceCaps`, falls back to a supported rate), max-length auto-stop,
  and typed failures for no-device / permission-denied / too-short.
- `Assets/Scripts/Dialogue/VoiceInputController.cs`: owns the full voice lifecycle - builds its
  own `ISTTService`, runs record -> STT -> submit, holds the voice `CancellationTokenSource`,
  performs mid-leave cancellation (by subscribing to `ActiveNPCService.ActiveNPCCleared`), and
  runs the pre-submit re-checks before calling `DialogueManager.SubmitPlayerText`.
- `Assets/Scripts/Dialogue/DialogueManager.cs`: exposes `CanStartTalking` and public
  `SubmitPlayerText(string)` (shared by Debug text + STT); `OnDebugTextSubmitted` now delegates to
  it. No microphone/STT logic lives in the manager (ownership boundary).
- `Assets/Scripts/DebugTools/DebugStateStore.cs` + `Assets/Scripts/UI/DebugPanelUI.cs`: added
  `Recording`, `RecordingSeconds`, `LastSttError` observability.

## Sprint 4 - NPC Activation, Interaction Range & Facing (2026-06-04)

Goal: detect the player near an NPC, promote a single global ActiveNPC, expose name +
"按住空格说话" hint via events, and yaw the NPC toward the player while active (returning to
its default heading on exit). No dialogue pipeline, no LLM/TTS/STT, no QuestSystem changes,
no complex UI.

Done:

- `Assets/Scripts/NPC/NPCEvents.cs`: `ActiveNPCChangedEventArgs` (id/name/proximity text + computed
  `HintText`) and the default hint constant `按住空格说话`.
- `Assets/Scripts/NPC/ActiveNPCService.cs`: plain class enforcing the single-active invariant;
  `ActiveNPCChanged` / `ActiveNPCCleared` events (clear is the reserved Dialogue-rollback hook).
- `Assets/Scripts/NPC/NPCManager.cs`: scene singleton owning a shared `ConfigManager`
  (`LoadNpcConfigs`), a duplicate-free ordered candidate list, and the conflict rule
  (first-come keeps focus; new entrants queue; on exit the next candidate is promoted).
- `Assets/Scripts/NPC/NPCController.cs`: resolves its `NPCConfig`, registers with the manager,
  stores `CurrentPlayerTransform`, exposes read-only `IsActive` / `NpcName` / `ProximityPromptText`
  / `HintText` / `InteractionRadius`, and raises `ActivationChanged(bool)`.
- `Assets/Scripts/NPC/NPCInteractionTrigger.cs`: child `SphereCollider` trigger, filters by the
  `Player` tag, sizes its radius from the controller's effective radius, forwards enter/exit.
- `Assets/Scripts/NPC/NPCFacingController.cs`: caches the default rotation, reads the player from
  the sibling controller, yaw-only `RotateTowards` smoothing while active, returns to default when
  inactive.
- `Assets/Scripts/NPC/NPCNameplate.cs`: minimal label driving an optional `TextMesh` / visual root
  (events remain the primary contract).

Decisions:

- Trigger lives on a child `InteractionTrigger` (SphereCollider `isTrigger`, optional kinematic
  gravity-off Rigidbody). The player needs only tag `Player` + a collider/CharacterController; the
  kinematic Rigidbody fallback goes on the NPC trigger, never forced onto the player.
- Single-active conflict resolved first-come-first-served; overlapping NPCs are queued, not stolen.
- Defensive errors: missing `NPCManager`, unknown `NPCID`, and trigger without an `NPCController`.

Not done (out of scope, by design): DialoguePipeline, LLM/TTS/STT calls, QuestSystem changes, full UI.

## Sprint 3 - Quest System Runtime (2026-06-04)

Goal: a runtime quest state machine on top of the Sprint 1 config and the Sprint 2
`ILLMService`. No NPC interaction, dialogue pipeline, TTS, or real UI.

Done:

- `Assets/Scripts/Quest/QuestRuntimeState.cs`: per-quest runtime wrapper (config + `QuestState`
  + last LLM eval info).
- `Assets/Scripts/Quest/QuestEvents.cs`: `QuestStateChangedEventArgs` broadcast payload.
- `Assets/Scripts/Quest/IQuestEvaluator.cs`: `IQuestEvaluator`, `QuestEvalRequest`, `QuestEvalResult`.
- `Assets/Scripts/Quest/RuleQuestEvaluator.cs`: Composite completion rule (all children Completed).
- `Assets/Scripts/Quest/LLMQuestEvaluator.cs`: TargetDialogue evaluator that calls `ILLMService`
  only (no vendor SDK), renders the QuestEval prompt, and parses the `{isCompleted,reason,confidence}` JSON.
- `Assets/Scripts/Quest/QuestManager.cs`: state machine (Inactive/Active/Completed) with start-quest
  auto-activation, Composite child auto-activation, parent auto-complete cascade, NextQuestID chaining,
  TargetNPCID filtering, and a `QuestStateChanged` event.
- `Assets/Scripts/Quest/QuestSystemTester.cs`: Debug-only MonoBehaviour with ContextMenu hooks
  (init / activate / complete / LLM-evaluate / log states).

Decisions:

- Start quest is auto-detected (root with `ParentQuestID==0` not referenced by any `NextQuestID`,
  lowest SortOrder/QuestID) with an optional Inspector override.
- A Composite quest with no children does not auto-complete, to avoid an instant cascade.
- The LLM evaluator never mutates quest state; the test entry applies the verdict via `TryCompleteQuest`.

Not done (out of scope, by design): NPC interaction, DialoguePipeline, TTS playback, full UI.

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
