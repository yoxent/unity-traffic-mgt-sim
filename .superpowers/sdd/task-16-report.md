# Task 16 Report: Game Bootstrap & MVP Scene

## Status
**Complete**

## Summary
Implemented `GameBootstrap` MonoBehaviour that wires the full MVP sim stack (`DayClock`, `EodController`, `FleetManager`, `DemandSpawner`, `DispatchService`, `HubManager`, `RatingSystem`, `EconomySystem`, `OverloadSystem`, `MapLoader`, `RunState`) and ticks them in the specified order. Created `MVP_District.unity` with bootstrap, orthographic camera + input, placeholder ground/canvas, six event-channel SO assets, tutorial demand/overload data, and added the scene to `EditorBuildSettings`.

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Core/GameBootstrap.cs` | Scene wiring, sim tick loop, EOD + economy/rating hooks |
| `Assets/Game/Scripts/Dispatch/DispatchService.cs` | Added public `TickPathAgents(float)` for post-overload path tick |

### Data
| File | Description |
|------|-------------|
| `Assets/Game/Data/Demand/TutorialDemand.asset` | Car + Food demand waves for tutorial day |
| `Assets/Game/Data/Overload/DefaultOverload.asset` | Default capacity multiplier |
| `Assets/Game/Data/Events/DayEnded.asset` | `GameEventChannel` |
| `Assets/Game/Data/Events/EodStarted.asset` | `GameEventChannel` |
| `Assets/Game/Data/Events/DayAdvanced.asset` | `GameEventChannel` |
| `Assets/Game/Data/Events/RunFailed.asset` | `GameEventChannel` |
| `Assets/Game/Data/Events/OrderAssigned.asset` | `OrderEventChannel` |
| `Assets/Game/Data/Events/OrderCompleted.asset` | `OrderEventChannel` |

### Scene / Settings
| File | Description |
|------|-------------|
| `Assets/Game/Scenes/MVP_District.unity` | Bootstrap, ortho camera, input, ground, canvas placeholder |
| `ProjectSettings/EditorBuildSettings.asset` | Added MVP scene to build list |

## Sim Tick Order

When `RunPhase.Playing`:

1. **DayClock** — `Advance(deltaTime)`
2. **Demand** — `DemandSpawner.Tick(dayFraction)`; route new orders via `HubManager.TryAcceptOrder`
3. **Patience + cooldowns** — hub/city-queue patience; fleet cooldown ticks
4. **Hub** — `HubManager.Tick(deltaTime)`
5. **Dispatch assign** — refresh pending list from hubs + city queue; `DispatchService.Tick()`
6. **Overload** — `OverloadSystem.Tick()`
7. **Path agents** — `DispatchService.TickPathAgents(deltaTime)`

Post-dispatch: job completion/expiry applies `RatingSystem` + `EconomySystem` and raises `OrderCompleted`.

## EOD Wiring

- `DayClock.DayEnded` → raises `DayEnded` channel → `EodController.BeginEod(skipIntervention: TutorialSaveStub.ShouldSkipEodUi)`
- `EodController.EodStarted` / `DayAdvanced` → respective SO channels
- Shared `EodActionQueue` across `FleetManager`, `HubManager`, and `EodController`
- `EconomySystem.ApplyDailyUpkeep` runs on active hub defs during EOD apply

## Module / Hub Bootstrap

- On unlock (or saved free-module choice), places module hub on next slot, unlocks slot 1 for second hub, buys starter fleet, positions vehicles at nearest road node to hub slot
- MVP scene defaults to **Car** module when none unlocked (UI panels optional/null-safe)

## Scene Contents

| Object | Components |
|--------|------------|
| `Bootstrap` | `GameBootstrap` with all SO refs wired |
| `Main Camera` | Orthographic top-down, `OrthoPanZoomCamera`, `GameInputReader`, URP camera data |
| `Ground` | Scaled plane placeholder |
| `Canvas` | Screen-space overlay + placeholder label |
| `EventSystem` | Input System UI module |
| `Directional Light` | Basic scene lighting |

## Play Mode Checklist (manual)

- [ ] Car orders spawn and auto-dispatch after Play
- [ ] Buy Food module (when UI wired) → food hub + orders
- [ ] Patience/rating/tips on job complete
- [ ] Overload or 1★ streak → `RunFailed` + phase Failed
- [ ] Day end → EOD panel (when wired) or skip via tutorial flag
- [ ] RMB pan, scroll zoom, Space pause, Tab speed

## Concerns / Follow-ups

1. **UI wiring in scene** — Canvas placeholder only; assign `GameHud`, `DemandCheckpointPanel`, `EodPanel`, `ModulePurchasePanel` in Inspector for full HUD (Task 15 components exist)
2. **Unity MCP timeout** — Editor compile/play verification timed out; re-open scene in Unity to refresh script meta if needed
3. **Addressables** — Map still loaded synchronously via `MapLoader.Load` (Task 17)
4. **Food demand before unlock** — Food waves spawn but fail routing until Food module purchased

## Commit
```
feat: integrate MVP vertical slice scene
```

## Dependencies Satisfied for Downstream Tasks
- Full vertical slice scene for manual acceptance testing
- Event channel assets for UI subscription (Task 15)
- `TickPathAgents` split for correct overload ordering
- Build settings entry for MVP district scene
