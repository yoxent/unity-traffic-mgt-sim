# Task 13 Report: Overload Fail Condition

## Status
**Complete**

## Summary
Implemented `OverloadSystem.Tick()` to compare unassigned pending orders (hub buffers + city queue via `HubManager.GetUnassignedOrderCount()`) against a capacity threshold derived from placed hub capacities and `OverloadDef.capacityMultiplier`. When unassigned count exceeds the threshold while `RunPhase.Playing`, the run fails immediately.

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Data/OverloadDef.cs` | SO: `capacityMultiplier` (default 1) scales summed hub capacity |
| `Assets/Game/Scripts/Systems/OverloadSystem.cs` | `Tick()` fail check vs hub capacity threshold |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Systems/OverloadSystemTests.cs` | 3 tests: exceed capacity → Failed, at capacity → Playing, city queue counts |

## Key Interfaces

### `OverloadDef` (`TrafficSim.Data`)
| Field | Behavior |
|-------|----------|
| `capacityMultiplier` | Scales summed `HubInstance.Capacity` into overload threshold (default 1) |

### `OverloadSystem` (`TrafficSim.Systems`)
| Member | Behavior |
|--------|----------|
| `Tick()` | If `Playing` and `GetUnassignedOrderCount() > threshold`, set `Phase = Failed` |
| `GetCapacityThreshold()` | Sum of active hub capacities × multiplier (rounded, min 0) |

## Overload Logic
1. Skip when phase is not `Playing`.
2. Count pending unassigned orders across all hubs and city queue (`HubManager.GetUnassignedOrderCount()`).
3. Threshold = Σ hub `Capacity` × `OverloadDef.capacityMultiplier`.
4. If count **>** threshold → `RunPhase.Failed` immediately.

## TDD Flow
1. **Failing test** — `Tick_OrdersExceedCapacity_SetsPhaseFailed`
2. **Implementation** — `OverloadDef`, `OverloadSystem`
3. **Pass** — Logic verified by inspection; Unity MCP test runner timed out

## Test Results

**Expected OverloadSystemTests (after Unity refresh):**
| Test | Assertion |
|------|-----------|
| `Tick_OrdersExceedCapacity_SetsPhaseFailed` | 4 pending on capacity-3 hub → `Phase == Failed` |
| `Tick_OrdersAtCapacity_RemainsPlaying` | 3 pending on capacity-3 hub → `Phase == Playing` |
| `Tick_CityQueueOrders_CountTowardOverload` | 2 hub + 1 city queue on capacity-2 hub → `Phase == Failed` |

**Unity Edit Mode:** Re-run Test Runner → `OverloadSystemTests` after domain reload.

## Concerns / Follow-ups
1. **GameBootstrap wiring** — Call `OverloadSystem.Tick()` after dispatch/path agents (Task 16)
2. **Default SO asset** — Optional `Assets/Game/Data/Overload/DefaultOverload.asset` for editor tuning
3. **Assigned in-flight jobs** — Only unassigned pending orders count; vehicle jobs in progress excluded
4. **Unity MCP timeout** — Re-run Edit Mode tests after editor settles

## Commit
```
c1c7fd5 feat: add overload fail condition
```

## Dependencies Satisfied for Downstream Tasks
- Immediate fail path for sim loop (Task 16 `GameBootstrap`)
- Uses `HubManager.GetUnassignedOrderCount()` from Task 12
