# Sprint 5 Plan — Base UI, Subtitle UI, Quest UI & Debug Panel

> Status: Plan approved (architecture decision: `QuestRuntimeHost` owns the live `QuestManager`).
> Scope guardrails: no DialoguePipeline, no LLM/TTS/STT calls, no refactor of `QuestManager`/`NPCManager`.
> Business modules update UI only via existing events, `DebugStateStore` setters, or public UI methods — never by touching `Text`/TMP components directly.

## 1. Goal

Establish observability early. Every later module can surface state through the on-screen UI and the
Debug panel: current NPC, current quest, recent input, raw LLM output, JSON parse result, quest
verdict, TTS queue length, and the last error.

## 2. Context gathered from the codebase

- Events already exist:
  - `ActiveNPCService.ActiveNPCChanged` / `ActiveNPCCleared` — owned by the `NPCManager` scene
    singleton, reachable via `NPCManager.Instance.ActiveService` (and re-exported events).
  - `QuestManager.QuestStateChanged` (payload `QuestStateChangedEventArgs`).
- `ActiveNPCChangedEventArgs` already carries `NpcName` and `HintText` (defaults to `按住空格说话`).
- Gap: there is **no production scene owner of a live `QuestManager`** (only the debug-only
  `QuestSystemTester` builds one). Resolved additively via `QuestRuntimeHost` (see §B item 8).
- UI stack: `com.unity.ugui 2.0.0` (TextMeshPro included). No `.asmdef`, so all scripts compile into
  `Assembly-CSharp` and can reference `UnityEngine.UI` / TMP freely.
- Input: `activeInputHandler: 2` (Both); existing `SimpleFirstPersonController` uses legacy `Input`.
  F1 toggle and Enter-to-send will use legacy `Input` for consistency.
- Conventions: namespaces `MultiAgentNPC.*`; XML-documented classes; events use `EventArgs` payloads;
  plain-C# managers with thin MonoBehaviour owners.

Text rendering: **TextMeshPro** (`TMP_Text` / `TMP_InputField`) for all labels (handles long
multiline debug dumps well). One-time "Import TMP Essentials" required (see §E).

## A. Architecture overview

```
Business modules (later sprints)        UI layer (this sprint)
─────────────────────────────────       ─────────────────────────────
ActiveNPCService.ActiveNPCChanged ─────► InteractionHintUI
QuestManager.QuestStateChanged ────────► QuestUI
DialoguePipeline/STT/LLM/TTS ──set()──► DebugStateStore ──Changed──► DebugPanelUI
DialoguePipeline (subtitle pushes) ────► SubtitleUI (public methods)
DebugInputBox ──OnDebugTextSubmitted──► (later) DialoguePipeline
```

## B. Scripts to create

UI components live in `Assets/Scripts/UI/` (`MultiAgentNPC.UI`); the state hub lives in
`Assets/Scripts/Diagnostics/` (`MultiAgentNPC.Diagnostics`).

1. **`DebugStateStore.cs`** (`MultiAgentNPC.Diagnostics`) — plain C# singleton (`Instance`). Central
   record of system state with typed setters and a single `event Action<DebugStateStore> Changed`.
   No Unity/UI dependency.
2. **`DebugEvents.cs`** (`MultiAgentNPC.Diagnostics`) — static class with
   `static event Action<string> DebugTextSubmitted` (the `OnDebugTextSubmitted` broadcast) plus a
   `Raise(text)` helper. Decouples `DebugInputBox` from the future `DialoguePipeline`.
3. **`InteractionHintUI.cs`** (`MultiAgentNPC.UI`) — subscribes to `NPCManager.Instance.ActiveService`
   (`ActiveNPCChanged` / `ActiveNPCCleared`); shows `"<NpcName>\n<HintText>"` when active, hides on clear.
4. **`SubtitleUI.cs`** (`MultiAgentNPC.UI`) — public methods only: `ShowPlayerText(string)`,
   `ShowNpcSentence(string)`, `ShowError(string)`, `Clear()`. Optional auto-hide timer.
5. **`QuestUI.cs`** (`MultiAgentNPC.UI`) — renders the current main quest + subtasks with completion
   state. Binds to a `QuestManager` via `Bind(QuestManager)`, subscribes to `QuestStateChanged`, and
   re-renders on every transition. Filters by `QuestConfig.ShowInUI`, orders by `SortOrder`, and shows
   composite children with `[ ]` / `[x]` markers derived from `QuestState`.
6. **`DebugPanelUI.cs`** (`MultiAgentNPC.UI`) — F1 toggles a root panel; subscribes to
   `DebugStateStore.Changed` and rebuilds one multiline `TMP_Text` (throttled once per frame via a
   dirty flag). Sections listed in §D.
7. **`DebugInputBox.cs`** (`MultiAgentNPC.UI`) — wraps a `TMP_InputField`; on submit (Enter) calls
   `DebugEvents.Raise(text)`, optionally echoes into `DebugStateStore`/`SubtitleUI`, then clears and
   re-focuses. Lives inside the Debug panel.
8. **`QuestRuntimeHost.cs`** (`MultiAgentNPC.UI`) — small additive owner (not a refactor). Creates a
   `ConfigManager`, loads quest configs, constructs + `Initialize()`s a `QuestManager`, and exposes it.
   `QuestUI` auto-binds to it if no manager is assigned. Fills the "who owns the live QuestManager" gap
   for the demo and is trivially replaceable by a future game-flow sprint (`QuestUI.Bind(...)` still works).
9. **`HudBinder.cs`** (optional, `MultiAgentNPC.UI`) — one scene component that wires references at
   startup (finds `NPCManager`, binds `QuestUI` to `QuestRuntimeHost`). UI components also self-subscribe
   defensively, so this is convenience, not required.

> `NPCNameplate` (existing tiny world-space label) is left untouched; `InteractionHintUI` is the
> screen-space HUD version requested here.

## C. Inspector fields per script

- **`DebugStateStore`** — none (pure C#).
- **`DebugEvents`** — none (static).
- **`InteractionHintUI`** — `TMP_Text hintLabel`, `GameObject root`, `bool logOnly`.
- **`SubtitleUI`** — `TMP_Text playerLine`, `TMP_Text npcLine`, `TMP_Text errorLine`,
  `GameObject root`, `float autoHideSeconds` (0 = stay), `Color errorColor`.
- **`QuestUI`** — `TMP_Text questText`, `GameObject root`, `QuestRuntimeHost questHost` (optional;
  auto-found if null), `bool showCompletedQuests`.
- **`DebugPanelUI`** — `GameObject panelRoot`, `TMP_Text contentText`, `KeyCode toggleKey = F1`,
  `bool startVisible`, `DebugInputBox debugInputBox` (optional, for focus management).
- **`DebugInputBox`** — `TMP_InputField inputField`, `bool clearOnSubmit = true`,
  `bool echoToDebugState = true`.
- **`QuestRuntimeHost`** — `int startQuestIdOverride = 0`, `string configFolderOverride` (empty =
  default), `bool initializeOnAwake = true`.
- **`HudBinder`** (if used) — references to each UI component above + `QuestRuntimeHost`.

## D. How `DebugStateStore` is updated by later modules

`DebugStateStore.Instance` exposes read-only properties + typed setters. Each setter updates the
field, stamps `LastUpdatedUtc`, and raises `Changed`. Later sprints call these — **no UI reference
needed**:

| Field (read-only prop)            | Setter (called by)                                              |
| --------------------------------- | --------------------------------------------------------------- |
| `CurrentNpcName` / `CurrentNpcId` | `SetCurrentNpc(id, name)` — Dialogue/NPC layer (or `ActiveNPCChanged`) |
| `CurrentQuestName` / `CurrentQuestId` | `SetCurrentQuest(id, name)` — quest layer                   |
| `DialogueState` (placeholder)     | `SetDialogueState(string)` — future DialoguePipeline            |
| `LastSttText`                     | `SetLastSttText(string)` — STT service                          |
| `LastLlmRaw`                      | `SetLastLlmRaw(string)` — LLM service                           |
| `LastJsonParse`                   | `SetLastJsonParse(string)` — response parser                    |
| `LastQuestVerdict`                | `SetLastQuestVerdict(string)` — quest evaluator (`QuestEvalResult.ToString()`) |
| `TtsQueueLength`                  | `SetTtsQueueLength(int)` — TTS service                          |
| `LastError`                       | `SetLastError(string)` — any module's catch block               |

`DebugPanelUI` is the only subscriber; it formats all fields into one labeled multiline block. Sprint
6+ wiring is one line per event (e.g. `DebugStateStore.Instance.SetLastSttText(result)`) with zero
coupling to Text components.

## E. Unity Canvas manual configuration steps

1. **Import TMP Essentials**: `Window ▸ TextMeshPro ▸ Import TMP Essential Resources` (one-time).
2. **Create HUD Canvas**: in `DemoMain.unity`, `GameObject ▸ UI ▸ Canvas` → name `HUDCanvas`
   (Render Mode: Screen Space – Overlay; Canvas Scaler: Scale With Screen Size, 1920×1080). This also
   creates an `EventSystem` (needed for the input field).
3. **Interaction hint** (bottom-center): empty child `InteractionHint` + a `TMP_Text` child; add
   `InteractionHintUI`, assign `hintLabel` and `root`.
4. **Subtitle** (bottom area, above hint): `SubtitlePanel` with three `TMP_Text` children
   (`PlayerLine`, `NpcLine`, `ErrorLine`); add `SubtitleUI`, assign the three labels + `root`.
5. **Quest UI** (top-left): `QuestPanel` + `TMP_Text questText`; add `QuestUI`, assign `questText` + `root`.
6. **Debug panel** (overlay, hidden by default): `DebugPanel` with a semi-transparent `Image`
   background, a large/scrollable `TMP_Text` `contentText`, and a `TMP_InputField` at the bottom; add
   `DebugPanelUI` (assign `panelRoot`, `contentText`) and `DebugInputBox` (assign `inputField`). Set
   `panelRoot` inactive so it starts hidden.
7. **Managers/hosts**: ensure an `NPCManager` exists. Add a `QuestRuntimeHost` to a `GameSystems`
   object. Optionally add `HudBinder` to `HUDCanvas` and assign all UI components + host (otherwise
   components self-resolve at runtime).
8. **Sorting**: keep `DebugPanel` last in the hierarchy so it draws on top.

## F. Self-test steps

1. **Compile** — confirm 0 errors / 0 new warnings after adding scripts.
2. **Interaction hint** — walk Player into an NPC trigger → hint shows `"<name> / 按住空格说话"`; leave
   range → hides. Multiple NPCs respect the single-active rule.
3. **Quest UI** — on Play, `QuestRuntimeHost` initializes and the start quest appears top-left. Use
   `QuestSystemTester`'s context-menu `Complete Quest` to drive a transition → `QuestUI` updates subtask
   `[ ]`→`[x]` and advances to the next quest live.
4. **Subtitle** — call `SubtitleUI.ShowPlayerText/ShowNpcSentence/ShowError` (context-menu tester or
   Debug input echo) → lines render; error uses error color.
5. **Debug panel** — press **F1** to toggle. Confirm sections render: current NPC, current quest,
   dialogue state placeholder, last STT / LLM raw / JSON / quest verdict, TTS queue length, last error.
   Drive a few `DebugStateStore.Instance.Set*` calls and confirm the panel refreshes.
6. **Debug input** — focus the input field, type text, press **Enter** → a temporary
   `DebugEvents.DebugTextSubmitted` handler logs the text; field clears. Confirms the hook for
   `DialoguePipeline`.
7. **Constraint check** — confirm no DialoguePipeline/LLM/TTS/STT calls were added and
   `QuestManager`/`NPCManager` were not modified.
