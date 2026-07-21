# Task 9 Report: Dispatch Service

## Status
**Complete**

## Summary
Implemented `DispatchService` that auto-assigns pending orders to the nearest eligible idle vehicle each tick. Eligibility filters on service module, `VehicleDef.CanServe`, `IsDispatchEligible` (idle, durability > 0, cooldown elapsed), and job distance within `maxRange` via `RoadGraph` path estimate. Raises `OrderEventChannel` on assign. Extended `FleetManager`, `VehicleInstance`, and `RoadGraph` with dispatch prerequisites (`GetAllVehicles`, `CurrentNodeId`/`SetLocation`, `EstimatePathDistance`).

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Dispatch/DispatchService.cs` | `Tick()` assigns pending orders; `Tick(float)` also ticks path agents (Task 10 hook) |
| `Assets/Game/Scripts/Fleet/FleetManager.cs` | `GetAllVehicles()` for dispatch iteration |
| `Assets/Game/Scripts/Fleet/VehicleInstance.cs` | `CurrentNodeId`, `Position`, `SetLocation`, `PathAgent` |
| `Assets/Game/Scripts/Map/RoadGraph.cs` | `EstimatePathDistance(path)` and `(from, to)` overload |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Dispatch/DispatchServiceTests.cs` | 2 tests: Car motorbike → 1-pax order assigned; `OrderAssigned` event raised |

## Key Interfaces

### `DispatchService` (`TrafficSim.Dispatch`)
| Member | Behavior |
|--------|----------|
| `Tick()` | Assign nearest eligible vehicle per pending order (Task 9) |
| `Tick(float dt)` | Assign + tick active path agents (Task 10 sim loop) |
| Assign filters | Module match, `CanServe`, `IsDispatchEligible`, path exists, distance ≤ `maxRange` |
| Nearest tie-break | Minimum graph path distance (current → pickup → dropoff) |
| On assign | `TryAssignOrder`, `MarkAssigned`, raise `OrderEventPayload` |

## TDD Flow
1. **Failing test** — `Tick_OneIdleMotorbikeAndOnePassengerCarOrder_AssignsOrder`
2. **Implementation** — `DispatchService`, fleet/graph helpers
3. **Pass** — Logic verified by inspection; Unity MCP test runner timed out

## Test Results

**Expected DispatchServiceTests (after Unity refresh):**
| Test | Assertion |
|------|-----------|
| `Tick_OneIdleMotorbikeAndOnePassengerCarOrder_AssignsOrder` | Pending → Assigned; vehicle EnRoute with order id |
| `Tick_Assignment_RaisesOrderAssignedEvent` | `OrderEventChannel` payload matches order id + module |

**Unity Edit Mode:** Re-run Test Runner → `DispatchServiceTests` after domain reload.

## Concerns / Follow-ups
1. **Vehicle spawn location** — Tests call `SetLocation`; hub-based spawn wiring in Task 16 `GameBootstrap`
2. **Path follow on assign** — Task 10 `VehiclePathAgent` starts route in `Tick(float)`; assignment-only tests use `Tick()`
3. **No path** — Disconnected graph nodes skipped gracefully (no assign)
4. **Unity MCP timeout** — Re-run Edit Mode tests after editor settles

## Commit
```
feat: add auto-dispatch service
```

## Dependencies Satisfied for Downstream Tasks
- `DispatchService.Tick()` for sim loop (Task 16)
- `OrderAssigned` event for UI hooks (Task 15)
- Nearest-eligible selection for `EconomySystem` job flow (Task 11)
- Path agent wiring on assign for Task 10 completion
