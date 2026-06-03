# Architecture (Draft)

Multi-agent NPC English dialogue demo. Unity 6 (`6000.3.15f1`), URP, C#.
This document is a Sprint 0 skeleton and will be expanded in later sprints.

## Principles (requirements doc section 18.1)

1. Modular.
2. Low coupling.
3. High readability.
4. High extensibility.
5. Friendly for Cursor-assisted, module-by-module code generation.
6. Supports multi-person collaboration.
7. Extensible toward an XR version.
8. Extensible XR input.

## Namespace

All new C# uses the unified root namespace `MultiAgentNPC`, with sub-namespaces per module
(for example `MultiAgentNPC.Core`, `MultiAgentNPC.Dialogue`).

## Assembly

Sprint 0 uses the default `Assembly-CSharp` (no `.asmdef`). Per-module assembly definitions
may be introduced later if module boundaries need enforcing.

## Module map (requirements doc section 18.2)

| Module | Folder | Responsibility |
| --- | --- | --- |
| Core | `Assets/Scripts/Core` | Global state, events, lifecycle, startup entry (`GameBootstrap`), shared `RuntimeContext` |
| Config | `Assets/Scripts/Config` | CSV loading, config data structures |
| NPC | `Assets/Scripts/NPC` | NPC binding, NPC state, NPC prompt selection |
| Dialogue | `Assets/Scripts/Dialogue` | Dialogue pipeline, state machine, history, turn commit |
| Services | `Assets/Scripts/Services` | LLM / STT / TTS interfaces and provider implementations |
| Quest | `Assets/Scripts/Quest` | Quest system, evaluators, quest events |
| UI | `Assets/Scripts/UI` | Quest UI, subtitle UI, prompts/hints |
| Audio | `Assets/Scripts/Audio` | Recording, audio playback, TTS playback |
| Animation | `Assets/Scripts/Animation` | ActionID mapping, Animator control |
| Input | `Assets/Scripts/Input` | Unity Input System wrapper (Talk action) |
| DebugTools | `Assets/Scripts/DebugTools` | Debug panel, text input mode |
| Utils | `Assets/Scripts/Utils` | JSON, paths, logging, common helpers |

> Note: the brief uses `DebugTools` (instead of `Debug`) to avoid clashing with the C# `Debug` keyword.

## Dependency-inversion rule (requirements doc section 18.3)

Business modules must not call concrete vendor SDKs/APIs directly. They depend on interfaces:

- `ILLMService` (implementations: OpenAI, DeepSeek)
- `ISTTService` (implementations: OpenAI Whisper, Azure Speech-to-Text)
- `ITTSService` (implementation: Azure TTS)

The dialogue system only references interfaces, never concrete providers.

## Input-inversion rule (requirements doc section 18.4)

Business modules must not read `Input.GetKey(...)` directly. Input flows through an input
abstraction (for example `PlayerInputController` / `TalkAction`), so PC can bind Space and a
future XR build can bind a controller button.

## Config & Prompt locations (Sprint 0 decision)

Sprint 0 places runtime config and prompts under `StreamingAssets/` so they remain loadable and
reloadable in builds:

```
Assets/StreamingAssets/Config/        (NPCConfig.csv, QuestConfig.csv, ActionConfig.csv, ExpressionConfig.csv)
Assets/StreamingAssets/Prompts/System/
Assets/StreamingAssets/Prompts/NPC/
Assets/StreamingAssets/Prompts/QuestEval/
Assets/StreamingAssets/Prompts/Shared/
```

DEVIATION: the requirements doc (sections 12.2 / 12.5) lists these under `Assets/Config/` and
`Assets/Prompts/`. We use `StreamingAssets/` because Unity only guarantees runtime file access
(needed for the "Reload Config" feature, doc section 12.8) from `StreamingAssets`. Loader code in
later sprints must resolve paths via `Application.streamingAssetsPath`.
