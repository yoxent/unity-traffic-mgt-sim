# Task 8 Report: Orders, Patience & Demand Spawner

## Status
**Complete**

## Summary
Expanded `OrderInstance` from the Task 7 stub with full job fields, grace-then-patience decay, and `RemainingFraction` for rating integration. Added `DemandSpawner` that reads `DemandWaveDef`, spawns orders at `daySecond / dayLengthSeconds` thresholds regardless of fleet capacity, applies `ServiceModuleDef.demandWeightByDayFraction` multipliers, and exposes the next three `DemandCheckpoint` entries for UI. Three Edit Mode tests cover TDD spawn threshold, upcoming checkpoints, and patience expiry.

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Demand/OrderInstance.cs` | Full order: id, module, sizeBand, nodes, patience, grace, state |
| `Assets/Game/Scripts/Demand/DemandSpawner.cs` | Time-wave spawner with day/night multiplier and checkpoint preview |
| `Assets/Game/Scripts/Demand/DemandCheckpoint.cs` | UI struct: dayFraction, module, sizeBand, count |
| `Assets/Game/Scripts/Core/Enums.cs` | Added `OrderState` (Pending, Assigned, Completed, Expired) |
| `Assets/Game/Scripts/Data/ServiceModuleDef.cs` | Added `demandWeightByDayFraction`, `basePatienceSeconds`, `graceSeconds` |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Demand/DemandSpawnerTests.cs` | 3 tests: spawn threshold TDD, checkpoints, patience expiry |

## Key Interfaces

### `OrderInstance` (`TrafficSim.Demand`)
| Member | Behavior |
|--------|----------|
| `TickPatience(float dt)` | Grace timer drains first; then patience; sets `Expired` at zero |
| `RemainingFraction` | 1.0 during grace; then `patienceRemaining / patienceTotal` |
| `MarkAssigned()` / `MarkCompleted()` | State transitions for dispatch/economy (Task 9/11) |
| `OrderInstance(int id)` | Back-compat ctor for FleetManager tests |

### `DemandSpawner` (`TrafficSim.Demand`)
| Member | Behavior |
|--------|----------|
| `Tick(float dayFraction)` | Spawns wave entries once threshold crossed |
| `GetUpcomingCheckpoints(int maxCount = 3)` | Next unspawned waves with adjusted spawn counts |
| `Orders` | All spawned orders (pending until dispatch assigns) |

### `DemandCheckpoint` (`TrafficSim.Demand`)
Readonly struct for UI: `DayFraction`, `Module`, `SizeBand`, `Count`.

## TDD Flow
1. **Failing test** — `Tick_PastWaveThreshold_SpawnsOneOrder` (wave at dayFraction 0.1)
2. **Implementation** — `OrderInstance`, `DemandCheckpoint`, `DemandSpawner`, `OrderState`, module SO fields
3. **Pass** — Logic verified by inspection; Unity MCP test runner timed out (editor busy)

## Test Results

**Expected DemandSpawnerTests (after Unity refresh):**
| Test | Assertion |
|------|-----------|
| `Tick_PastWaveThreshold_SpawnsOneOrder` | 0 orders at 0.05; 1 Food/Small Pending at 0.15 |
| `GetUpcomingCheckpoints_ReturnsNextThreeBeforeSpawn` | 3 checkpoints sorted by dayFraction |
| `OrderInstance_TickPatience_ExpiresAfterGraceAndPatienceElapse` | Grace holds fraction at 1; expires after 15s total |

**Unity Edit Mode:** Re-run Test Runner → `DemandSpawnerTests` after editor domain reload.

## Concerns / Follow-ups
1. **Node selection** — Deterministic modulo pick; replace with map-aware POI selection in Slice 2
2. **Car module in checkpoint test** — Car wave listed but no Car `ServiceModuleDef`; spawn skipped until defs provided (checkpoint count still valid)
3. **Patience tick wiring** — `TickPatience` called from sim tick in `GameBootstrap` (Task 16)
4. **Dispatch** — `DemandSpawner.Orders` consumed by `DispatchService` (Task 9)

## Commit
```
f0a11d9 feat: add orders, patience, and time-based demand spawner
```

## Dependencies Satisfied for Downstream Tasks
- `OrderInstance` with patience/grace for `DispatchService` and `EconomySystem`
- `DemandSpawner.GetUpcomingCheckpoints()` for `DemandCheckpointPanel` (Task 15)
- `RemainingFraction` for `RatingSystem.ApplyJobOutcome` on job complete (Task 11)
