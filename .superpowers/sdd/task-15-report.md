# Task 15 Report: UI — HUD, Checkpoints, EOD Panel

## Status
**Complete**

## Summary
Added minimal uGUI MonoBehaviour panels for the MVP HUD and intervention flows: `GameHud` (money, stars, day, speed, time-of-day), `DemandCheckpointPanel` (next three demand waves with ETA), `EodPanel` (day summary, queued EOD actions, Continue, Skip toggle stub gated by tutorial PlayerPrefs), and `ModulePurchasePanel` with `ModulePurchaseGate` (first module free, then 15-day paid unlock gate + budget check). Shared `UiTextRef` sets text on TextMeshPro when the package is present, otherwise legacy `UnityEngine.UI.Text`.

## Files Created

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/UI/UiTextRef.cs` | TMP-or-legacy text binding via reflection |
| `Assets/Game/Scripts/UI/TutorialSaveStub.cs` | PlayerPrefs stubs for tutorial complete, free module choice, skip EOD UI |
| `Assets/Game/Scripts/UI/ModulePurchaseGate.cs` | Endless module unlock rules (free first, 15-day paid cooldown) |
| `Assets/Game/Scripts/UI/GameHud.cs` | Top-bar HUD bound to `RunState` + `DayClock` |
| `Assets/Game/Scripts/UI/DemandCheckpointPanel.cs` | Next 3 waves from `DemandSpawner.GetUpcomingCheckpoints()` |
| `Assets/Game/Scripts/UI/EodPanel.cs` | EOD summary, queue list, Continue, Skip toggle |
| `Assets/Game/Scripts/UI/ModulePurchasePanel.cs` | Per-module buy buttons when gate allows |

### Modified
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Game.Runtime.asmdef` | Added `UnityEngine.UI` reference |

## Key Interfaces

### `GameHud` (`TrafficSim.UI`)
| Member | Behavior |
|--------|----------|
| `Bind(RunState, DayClock)` | Injects sim state for `LateUpdate` refresh |
| Displays | Money, stars (1 decimal), day index (+1), speed/paused, Morning/Day/Evening/Night |

### `DemandCheckpointPanel` (`TrafficSim.UI`)
| Member | Behavior |
|--------|----------|
| `Bind(DemandSpawner, DayClock, dayLengthSeconds)` | Reads upcoming checkpoints each frame |
| Line format | `{Module} {SizeBand} x{Count} — {ETA}` (seconds or minutes) |

### `EodPanel` (`TrafficSim.UI`)
| Member | Behavior |
|--------|----------|
| `Bind(RunState, EodController)` | Subscribes to `EodStarted` |
| Continue | Calls `EodController.AdvanceDay()` during `EodIntervention` |
| Skip toggle | Disabled until `TutorialSaveStub.CanSkipEodUi`; persists preference for `BeginEod(skipIntervention: true)` in bootstrap |
| Summary | Stars, money, jobs, cumulative profit; lists queued EOD action costs |

### `ModulePurchasePanel` + `ModulePurchaseGate`
| Rule | Behavior |
|------|----------|
| First unlock | Free when `UnlockedModules` empty; records `TutorialSaveStub.SetFreeModuleChoice` |
| Paid unlock | One purchase per 15 calendar days after last paid unlock |
| Budget | Blocks purchase when `Money < unlockCost` |
| UI | Button labels show owned/free/cost; status line explains block reason |

### `UiTextRef`
| Behavior |
|----------|
| Uses `TMPro.TMP_Text` via reflection when `Unity.TextMeshPro` is loaded; falls back to `UnityEngine.UI.Text` |

## Scene Wiring (Task 16)
1. Create Canvas with uGUI text/button/toggle elements (TMP if installed, else legacy Text).
2. Add `GameHud`, `DemandCheckpointPanel`, `EodPanel`, `ModulePurchasePanel` to canvas child objects.
3. Wire serialized `UiTextRef` targets, EOD Continue button, Skip toggle, module buy buttons + `ServiceModuleDef` assets.
4. From `GameBootstrap`, call:
   - `gameHud.Bind(runState, dayClock)`
   - `demandCheckpointPanel.Bind(demandSpawner, dayClock, dayLengthSeconds)`
   - `eodPanel.Bind(runState, eodController)`
   - `modulePurchasePanel.Bind(runState)`
5. On `DayClock.DayEnded`, call `eodController.BeginEod(TutorialSaveStub.ShouldSkipEodUi)`.

## Test Results
- No automated tests (UI presentation layer; plan Step 5 is commit only).
- Compile verified via asmdef `UnityEngine.UI` reference; TMP optional at runtime.

## Concerns / Follow-ups
1. **Scene prefabs** — Scripts only until Task 16 builds `MVP_District` canvas wiring.
2. **TextMeshPro package** — Not in `manifest.json`; `UiTextRef` supports TMP when added manually.
3. **Module purchase side effects** — Unlock adds to `RunState.UnlockedModules` only; starter fleet/hub spawn deferred to bootstrap (Task 16).
4. **EOD action buttons** — Upgrade/scrap/network buttons are summarized via queue text; dedicated controls can be added later.
5. **Skip toggle during failed run** — Continue disabled on `RunPhase.Failed`; panel still shows failure summary.

## Commit
```
feat: add HUD, demand checkpoints, and EOD UI
```

## Dependencies Satisfied for Downstream Tasks
- HUD and checkpoint panels ready for `GameBootstrap` binding (Task 16)
- `EodPanel` + `TutorialSaveStub.ShouldSkipEodUi` for EOD skip flow
- `ModulePurchaseGate` for endless module expansion UI
- `DemandSpawner.GetUpcomingCheckpoints()` consumed by checkpoint HUD
