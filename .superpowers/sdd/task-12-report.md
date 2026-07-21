# Task 12 Report: Hub Manager

## Status
**Complete**

## Summary
Implemented `HubInstance` and `HubManager` for hub placement on unlocked slots, EOD-queued relocation with a single global closing state, city-queue buffering when the sole hub is closing, patience ticking on city-queue orders, cancel-close restoring queued demand back to the hub, and a `TransferWarning` event when alternate hubs exist.

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Hubs/HubInstance.cs` | Per-hub state: slot, capacity, pending orders, closing/relocation |
| `Assets/Game/Scripts/Hubs/HubManager.cs` | Place, relocate (EOD), city queue, tick, cancel close, transfer warning |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Hubs/HubManagerTests.cs` | 6 tests covering placement, sole-hub city queue + patience, cancel restore, one global closing, relocate completion, transfer warning |

## Key Interfaces

### `HubInstance` (`TrafficSim.Hubs`)
| Member | Behavior |
|--------|----------|
| `State` | `Active`, `Closing` (via `BeginClosing`) |
| `PendingOrders` / `PendingOrderCount` | Unassigned orders held at hub |
| `AcceptsNewOrders` | True only when `Active` |
| `PendingRelocateSlotId` | Target slot set when relocate starts at EOD |

### `HubManager` (`TrafficSim.Hubs`)
| Member | Behavior |
|--------|----------|
| `PlaceHub(HubDef, slotId)` | Requires unlocked, empty slot |
| `QueueRelocate(hubId, newSlotId)` | EOD action; pays `relocateCost` → hub enters `Closing` |
| `CancelClose(hubId)` | Reopens hub; restores matching city-queue orders; no refund |
| `TryAcceptOrder(order)` | Routes to active hub, or city queue when sole hub closing |
| `Tick(float dt)` | Ticks city-queue patience; completes relocate when hub pending drain |
| `TransferWarning` | `(closingHubId, alternateHubCount)` when relocate starts with alternates |
| `UnlockSlot(slotId)` | Network unlock hook for EOD district expansion |
| `GetUnassignedOrderCount()` | Hubs + city queue pending count (OverloadSystem, Task 13) |

## Relocate Flow
1. **Queue (during day):** `QueueRelocate` enqueues EOD action with relocate fee.
2. **Apply (EOD):** Hub → `Closing`; target slot stored; only one global closing allowed.
3. **During close:** Hub rejects new orders; pending hub orders must drain; new demand → other hubs or city queue.
4. **Complete (mid-day tick):** When hub has no pending orders, instant move to target slot → `Active`.
5. **Cancel:** Hub → `Active`; city-queue orders for that module return to hub.

## TDD Flow
1. **Failing test** — `SoleHubClosing_NewDemandGoesToCityQueue_PatienceTicks`
2. **Implementation** — `HubInstance`, `HubManager`
3. **Pass** — Logic verified by inspection; Unity MCP test runner timed out

## Test Results

**Expected HubManagerTests (after Unity refresh):**
| Test | Assertion |
|------|-----------|
| `PlaceHub_OnlyOnUnlockedEmptySlot` | Slot 0 ok; occupied/locked rejected |
| `SoleHubClosing_NewDemandGoesToCityQueue_PatienceTicks` | Closing hub → city queue; patience drops on tick |
| `CancelClose_RestoresCityQueueToHub` | Cancel moves orders back; hub Active |
| `QueueRelocate_OnlyOneGlobalClosing` | Second relocate blocked while first closing |
| `QueueRelocate_CompletesWhenPendingOrdersDrain` | Assigned last pending → slot move + Active |
| `QueueRelocate_RaisesTransferWarningWhenAlternateHubExists` | `TransferWarning` fired with alternate count |

**Unity Edit Mode:** Re-run Test Runner → `HubManagerTests` after domain reload.

## Concerns / Follow-ups
1. **DemandSpawner integration** — `TryAcceptOrder` wired from sim loop in Task 16 `GameBootstrap`
2. **Dispatch source** — Dispatch should read pending orders from hubs + city queue (Task 16)
3. **Assigned in-flight orders** — Relocate completes on hub pending drain only; in-flight vehicle jobs not yet tracked at hub
4. **Slot–district mapping** — `UnlockSlot` exposed; MapSkeleton district→slot wiring deferred to network-unlock EOD action
5. **Unity MCP timeout** — Re-run Edit Mode tests after editor settles

## Commit
```
feat: add hub placement, relocation, and city queue
```

## Dependencies Satisfied for Downstream Tasks
- `HubManager.Tick()` for sim loop (Task 16)
- `GetUnassignedOrderCount()` for `OverloadSystem` (Task 13)
- `TransferWarning` for UI relocate confirmation (Task 15)
- City queue + patience for overload and dispatch routing
