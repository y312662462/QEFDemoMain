# Sprint 10 Plan: ActionID Animation System and Expression Placeholder

## 1. Goal

Map the LLM JSON `actionId` to NPC Animator actions. `expressionId` is reserved (placeholder
only) and must NOT raise errors when expression resources are missing.

Do NOT change the Services layer (LLM/STT/TTS), the `DialoguePipeline` business flow, the
Quest system, prompt files, or UI layout.

## 2. Current state (verified)

- LLM JSON already parses `actionId` / `expressionId`: `NPCResponseJsonParser` lines 57-59 store
  them into `NPCSentence.ActionId` / `NPCSentence.ExpressionId`. Sprint 6 deliberately deferred
  driving the Animator.
- Playback is plain C# (not a MonoBehaviour): `DialoguePipeline` -> `TtsQueuePlayer.PlayAsync`
  plays sentences in order. Each sentence starts with `_presenter.ShowNpcSentence(sentence.Text)`
  (`TtsQueuePlayer.cs` line 84) - the natural place to trigger an action.
- The bridge between plain-C# pipeline and the Unity main thread is `IDialoguePresenter`,
  implemented by `DialogueManager` (MonoBehaviour) which owns main-thread UI/audio.
- NPC side: `NPCController` is the identity/activation hub; `NPCFacingController` is the template
  for "sibling component that reacts to activation". `NPCAnimationController` follows that pattern.
- NPC prefabs (e.g. `NPC_HatGril.prefab`) already have an `Animator` using
  `AnimationController_Demo.controller` (guid `9ba084a6...`).
- Animation assets exist: `Anim@Emoji_Hi / Emoji_Smile1 / Emoji_Showmanship / Interaction_Item_Put
  / Dance_1 ...`; the matching Animator STATE names are `Emoji_Hi`, `Interaction_Item_Put`,
  `Dance_1`, etc.
- IMPORTANT: the existing `AnimationController_Demo` is a locomotion blend-tree controller (params
  only `Forward/Turn/Jump/JumpLeg/OnGround/Crouch`); it has NO triggers for the emoji/interaction
  states. So the default implementation uses StateName (`Animator.CrossFade`/`Play`); Trigger mode
  is kept as an option.

## 3. ActionID default table (requirements doc 9.2)

| actionId | meaning | Animator state name |
| --- | --- | --- |
| 1001 | default Idle | `Idle` |
| 1201 | greet | `Emoji_Hi` |
| 1202 | nice | `Emoji_Nice` |
| 1203 | smile1 (default when unsure, rule 7) | `Emoji_Smile1` |
| 1204 | smile2 | `Emoji_Smile2` |
| 1205 | cheer | `Emoji_Cheer` |
| 1206 | applaud | `Emoji_Applaud` |
| 1213 | idle small action | `Emoji_Putter_Around` |
| 1214 | showmanship | `Emoji_Showmanship` |
| 1215 | side to side | `Emoji_SideToSide` |
| 1216 | sigh | `Emoji_Sigh` |
| 1301 | put item | `Interaction_Item_Put` |
| 1302 | pickup | `Interaction_Pickup` |
| 1501 | celebrate | `Dance_1` |

Unknown actionId -> fall back to `1001 / Idle`, log a single Warning, never error.

## 4. New / modified files

New (under `Assets/Scripts/Animation/`, per `Docs/Architecture.md` module map):

- `ActionDefinition.cs` - serializable mapping row: `int actionId`, `ActionDriveMode mode
  {StateName, Trigger}`, `string stateName`, `string triggerName`, `int layer`, `float
  crossFadeSeconds`.
- `ActionMappingTable.cs` - `ScriptableObject` holding `List<ActionDefinition>` for Inspector
  editing / sharing across NPCs.
- `ActionIdMapper.cs` - plain C# resolver: dictionary from the table (or built-in default table
  when none); `Resolve(actionId)` returns the hit or the default Idle + Warning.
- `NPCAnimationController.cs` - MonoBehaviour on the NPC prefab: `PlayAction(int)`,
  `ReturnToIdle()`, references `Animator` + optional `ActionMappingTable`.
- `ExpressionController.cs` - placeholder MonoBehaviour: `ApplyExpression(int)` normalizes unknown
  to 2000, logs and safely ignores, never errors.

Modified (minimal; Services untouched):

- `IDialoguePresenter.cs` - add `void PlaySentenceVisuals(int actionId, int expressionId);`.
- `TtsQueuePlayer.cs` - call `PlaySentenceVisuals` alongside `ShowNpcSentence` (no mapping here).
- `DialogueManager.cs` - implement `PlaySentenceVisuals`, capture the active NPC's
  `NPCAnimationController`/`ExpressionController` per turn, and call `ReturnToIdle()` on turn
  completion and on cancel (`StopAudioAndClear`).

Not changed: `DialoguePipeline.cs` flow, Services, Quest, prompts, UI layout.

## 5. Timing design

- Per-sentence: `TtsQueuePlayer` -> `presenter.PlaySentenceVisuals(actionId, expressionId)` ->
  `DialogueManager` routes to this turn's `NPCAnimationController.PlayAction` +
  `ExpressionController.ApplyExpression`. All Unity calls run on the main thread.
- Target NPC binding: captured in `DialogueManager.BeginTurn` from `NPCManager.Instance.ActiveNpc`
  via `GetComponentInChildren`, cached for the session.
- Return to Idle (req 9.1.5): in `BeginTurn` finally (only when `IsCurrentSession`) and in
  `StopAudioAndClear` (cancel/leave-range path). `DialoguePipeline` needs no change.

## 6. Animator parameter / state design

- StateName mode (default): `animator.CrossFade(stateName, crossFadeSeconds, layer)`. No new
  Animator params needed. States inside sub-state-machines need the full path in the Inspector
  (e.g. `Emoji.Emoji_Hi`).
- Trigger mode (optional): `animator.SetTrigger(triggerName)` for a future dedicated NPC controller.
- Idle: `idleActionId = 1001` -> a resident `Idle` state; `ReturnToIdle()` cross-fades to it.

## 7. Inspector configuration

- Add `NPCAnimationController` + `ExpressionController` to the NPC prefab.
- `NPCAnimationController` exposes `Animator`, `ActionMappingTable` (empty -> built-in default + a
  clear Warning), `idleActionId`, `logActions`.
- `Create > MultiAgentNPC > Action Mapping Table` builds a shareable asset; edit rows visually.

## 8. Self-test steps

1. Action plays: in range, LLM returns `1201` -> NPC plays `Emoji_Hi` synced with subtitle/voice.
2. Multi-sentence order: 1201/1203/1214 play in order.
3. Unknown actionId (e.g. 9999): fall back to Idle, one Warning, no exception, dialogue continues.
4. Return to Idle after finishing a turn.
5. Cancel: leave range mid-speech -> animation stops, returns to Idle.
6. expressionId placeholder: any id (incl. unknown) only logs, normalizes to 2000, no error.
7. Missing table: empty `ActionMappingTable` -> built-in default + clear message.
8. Inspector config: change a row (e.g. 1501 -> `Dance_2`) without code change.
9. Sub-state-machine path: `Emoji.Emoji_Hi` style full path resolves.

Editor note: playmode self-tests must run in Unity `6000.3.15f1`.
