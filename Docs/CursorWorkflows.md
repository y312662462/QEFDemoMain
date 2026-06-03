# Cursor Workflows (Draft)

How we collaborate with Cursor on this project. Keep this short and enforce it every sprint.

## Hard constraints

1. Do NOT implement NPC, Quest, Dialogue, LLM, TTS, or STT business logic outside the sprint scope.
2. Do NOT modify scene assets, prefabs, materials, or existing third-party resources.
3. Do NOT hardcode any API key. Keys are entered via the Inspector (Demo stage) or come from a
   backend later; never commit keys.
4. Do NOT create duplicate Manager classes. Check for an existing manager before adding one.
5. All new C# files use the unified namespace root `MultiAgentNPC` (plus a per-module sub-namespace).

## Working agreement

1. Plan first. For any non-trivial task, Cursor outputs a plan (file list, responsibilities,
   directory structure, Unity verification steps) and waits for confirmation before editing code.
2. One sprint at a time. Stay within the current sprint's scope.
3. Respect the architecture rules in `Architecture.md` (dependency inversion for services,
   input inversion for controls).
4. Update `Docs/ImplementationLog.md` after each sprint with what changed and key decisions.
5. Update `Docs/TestPlan.md` with verification steps for new features.

## Resume-friendly workflow

- Each sprint is self-contained and leaves the project compiling.
- `Docs/ImplementationLog.md` is the source of truth for "what state are we in".
- If a session is interrupted, re-read the latest log entry and the active plan before continuing.
