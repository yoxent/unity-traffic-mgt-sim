# Traffic Management Sim — MVP Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable MVP vertical slice — one small authored district, Car + Food modules, auto-dispatch, patience/rating/overload/EOD loop, hub placement, camera pan/zoom — driven by ScriptableObjects and ready for Slice 2 (Delivery, endless procgen, Steam).

**Architecture:** MonoBehaviour + ScriptableObject data/event channels (no ECS in MVP). Simulation systems communicate via typed SO event channels and a central `RunState`. Map loads an authored `MapSkeleton`, builds a road graph for pathfinding, spawns demand on time-based waves. EOD controller gates strategic actions.

**Tech Stack:** Unity 6000.3.18f1, URP 2D orthographic, Input System, LitMotion, Unity Splines, ZLinq, Addressables, Unity Test Framework, SO event channels.

## Global Constraints

- **Platform:** Windows single-player; Steam deferred to Slice 2.
- **View:** 2D top-down orthographic; RMB-drag pan; mouse wheel zoom.
- **Modules in MVP:** Car + Food only (Delivery stubbed in enums/SO, not playable).
- **Vehicles in MVP:** Bicycle, Motorbike, 4-seater (6-seater Slice 2).
- **Dispatch:** Automatic; one job per vehicle; strict size bands; no downsizing.
- **Fail:** Overload (immediate); 1★ for N consecutive EODs (N=3 default SO).
- **Tips:** None at 1–2★ on job complete; rating from 20% patience bands.
- **EOD-only:** fleet upgrade, scrap, network unlock, start relocation.
- **Continuous:** buy vehicles, repair, buy module, place hub.
- **Map:** Authored skeleton only (no endless procgen in MVP).
- **Assets:** User-provided art; use placeholder sprites/colors until supplied.
- **Do not mutate SO assets at runtime** for run state.

## File Map (MVP)

| Path | Responsibility |
|------|----------------|
| `Assets/Game/Scripts/Game.Runtime.asmdef` | Runtime assembly |
| `Assets/Game/Tests/Game.Tests.asmdef` | Edit Mode tests |
| `Assets/Game/Scripts/Core/Enums.cs` | Shared enums |
| `Assets/Game/Scripts/Core/RunState.cs` | Mutable run snapshot |
| `Assets/Game/Scripts/Core/DayClock.cs` | Day/night + speed scale |
| `Assets/Game/Scripts/Core/EodController.cs` | EOD phase + queue apply |
| `Assets/Game/Scripts/Core/GameBootstrap.cs` | Scene wiring |
| `Assets/Game/Scripts/Events/*.cs` | SO event channel types |
| `Assets/Game/Scripts/Data/*.cs` | SO definition types |
| `Assets/Game/Scripts/Map/RoadGraph.cs` | Nodes/edges from skeleton |
| `Assets/Game/Scripts/Map/MapLoader.cs` | Load skeleton → graph |
| `Assets/Game/Scripts/Fleet/FleetManager.cs` | Buy/repair/scrap/upgrade |
| `Assets/Game/Scripts/Fleet/VehicleInstance.cs` | Runtime vehicle state |
| `Assets/Game/Scripts/Demand/OrderInstance.cs` | Job + patience |
| `Assets/Game/Scripts/Demand/DemandSpawner.cs` | Time waves + checkpoints |
| `Assets/Game/Scripts/Dispatch/DispatchService.cs` | Auto-assign |
| `Assets/Game/Scripts/Dispatch/VehiclePathAgent.cs` | Spline follow |
| `Assets/Game/Scripts/Systems/RatingSystem.cs` | Stars + bands |
| `Assets/Game/Scripts/Systems/EconomySystem.cs` | Money, fares, tips |
| `Assets/Game/Scripts/Systems/OverloadSystem.cs` | Capacity fail |
| `Assets/Game/Scripts/Hubs/HubManager.cs` | Place/close/relocate/queue |
| `Assets/Game/Scripts/Input/GameInputActions.cs` | Input System wrapper |
| `Assets/Game/Scripts/Camera/OrthoPanZoomCamera.cs` | Pan/zoom + LitMotion `OutQuad` (no bounce) |
| `Assets/Game/Scripts/UI/*.cs` | HUD, checkpoints, EOD panel |
| `Assets/Game/Data/` | SO asset instances |
| `Assets/Game/Scenes/MVP_District.unity` | Vertical slice scene |
| `Assets/Game/Addressables/` | Addressables groups (maps) |

---

### Task 1: Project foundation & packages

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Game/Scripts/Game.Runtime.asmdef`
- Create: `Assets/Game/Tests/Game.Tests.asmdef`
- Create: `Assets/Game/Scripts/Core/Enums.cs`

**Interfaces:**
- Produces: `ServiceModule`, `VehicleType`, `JobSizeBand`, `VehicleState`, `HubState`, `RunPhase` enums used by all later tasks.

- [ ] **Step 1: Add packages to manifest**

Modify `Packages/manifest.json` dependencies — add:

```json
"com.unity.splines": "2.8.2",
"com.unity.addressables": "2.7.4",
"com.cysharp.zlinq": "https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity"
```

(LitMotion and Input System already present.)

- [ ] **Step 2: Create runtime asmdef**

Create `Assets/Game/Scripts/Game.Runtime.asmdef`:

```json
{
  "name": "Game.Runtime",
  "rootNamespace": "TrafficSim",
  "references": [
    "Unity.InputSystem",
    "Unity.Splines",
    "Unity.Mathematics",
    "LitMotion",
    "ZLinq.Unity"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false
}
```

- [ ] **Step 3: Create test asmdef**

Create `Assets/Game/Tests/Game.Tests.asmdef`:

```json
{
  "name": "Game.Tests",
  "rootNamespace": "TrafficSim.Tests",
  "references": [
    "Game.Runtime",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 4: Create shared enums**

Create `Assets/Game/Scripts/Core/Enums.cs`:

```csharp
namespace TrafficSim.Core
{
    public enum ServiceModule { Car, Food, Delivery }
    public enum VehicleType { Bicycle, Motorbike, FourSeater, SixSeater }
    public enum JobSizeBand { Small, OnePassenger, OneToFourPassengers, FourToSixPassengers, MediumDelivery, LargeDelivery }
    public enum VehicleState { Idle, EnRoute, Cooldown, Offline }
    public enum HubState { Active, Closing, Relocating }
    public enum RunPhase { Playing, EodIntervention, Failed, Won }
}
```

- [ ] **Step 5: Verify Unity opens project without compile errors**

Open Unity → wait for reimport → Console: 0 errors in Game.Runtime.

- [ ] **Step 6: Commit**

```bash
git add Packages/manifest.json Assets/Game/
git commit -m "chore: add Game foundation, packages, and shared enums"
```

---

### Task 2: ScriptableObject event channels

**Files:**
- Create: `Assets/Game/Scripts/Events/GameEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/IntEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/OrderEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/OrderEventPayload.cs`
- Test: `Assets/Game/Tests/Events/GameEventChannelTests.cs`

**Interfaces:**
- Produces: `GameEventChannel.Raise()`, `GameEventChannel.Register(Action)`, `IntEventChannel.Raise(int)`, `OrderEventChannel.Raise(OrderEventPayload)`.

- [ ] **Step 1: Write failing test**

Create `Assets/Game/Tests/Events/GameEventChannelTests.cs`:

```csharp
using NUnit.Framework;
using TrafficSim.Events;

namespace TrafficSim.Tests.Events
{
    public class GameEventChannelTests
    {
        [Test]
        public void Raise_InvokesRegisteredListener()
        {
            var channel = UnityEngine.ScriptableObject.CreateInstance<GameEventChannel>();
            var count = 0;
            channel.Register(() => count++);
            channel.Raise();
            Assert.AreEqual(1, count);
        }
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Unity: Window → General → Test Runner → Edit Mode → run `Raise_InvokesRegisteredListener` → FAIL (type not found).

- [ ] **Step 3: Implement event channels**

Create `Assets/Game/Scripts/Events/GameEventChannel.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Events
{
    [CreateAssetMenu(menuName = "TrafficSim/Events/Game Event")]
    public class GameEventChannel : ScriptableObject
    {
        readonly List<Action> _listeners = new();

        public void Register(Action listener) => _listeners.Add(listener);
        public void Unregister(Action listener) => _listeners.Remove(listener);

        public void Raise()
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke();
        }
    }
}
```

Create `Assets/Game/Scripts/Events/IntEventChannel.cs` (same pattern with `Action<int>`).

Create `Assets/Game/Scripts/Events/OrderEventPayload.cs`:

```csharp
using TrafficSim.Core;

namespace TrafficSim.Events
{
    public readonly struct OrderEventPayload
    {
        public readonly int OrderId;
        public readonly ServiceModule Module;
        public OrderEventPayload(int orderId, ServiceModule module)
        {
            OrderId = orderId;
            Module = module;
        }
    }
}
```

Create `Assets/Game/Scripts/Events/OrderEventChannel.cs` with `Action<OrderEventPayload>`.

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Events Assets/Game/Tests/Events
git commit -m "feat: add ScriptableObject event channels"
```

---

### Task 3: Core ScriptableObject definitions

**Files:**
- Create: `Assets/Game/Scripts/Data/VehicleDef.cs`
- Create: `Assets/Game/Scripts/Data/ServiceModuleDef.cs`
- Create: `Assets/Game/Scripts/Data/RatingDef.cs`
- Create: `Assets/Game/Scripts/Data/HubDef.cs`
- Create: `Assets/Game/Scripts/Data/DemandWaveDef.cs`
- Create: `Assets/Game/Scripts/Data/MapSkeleton.cs`
- Create: `Assets/Game/Data/` (placeholder SO assets via Unity Editor)
- Test: `Assets/Game/Tests/Data/VehicleEligibilityTests.cs`

**Interfaces:**
- Produces: `VehicleDef.CanServe(ServiceModule, JobSizeBand)`, `RatingDef.GetRatingDelta(float remainingFraction)`.

- [ ] **Step 1: Write failing eligibility test**

```csharp
using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using UnityEngine;

namespace TrafficSim.Tests.Data
{
    public class VehicleEligibilityTests
    {
        [Test]
        public void Bicycle_ServesFoodSmallOnly()
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            def.type = VehicleType.Bicycle;
            def.allowedModules = new[] { ServiceModule.Food, ServiceModule.Delivery };
            def.allowedSizeBands = new[] { JobSizeBand.Small };
            Assert.IsTrue(def.CanServe(ServiceModule.Food, JobSizeBand.Small));
            Assert.IsFalse(def.CanServe(ServiceModule.Car, JobSizeBand.OnePassenger));
        }
    }
}
```

- [ ] **Step 2: Run test — FAIL**

- [ ] **Step 3: Implement SO defs**

`VehicleDef.cs` — fields: type, color, speed, maxRange, maxDurability, durabilityLossPerJob, repairCost, purchaseCost, cooldownSeconds, allowedModules[], allowedSizeBands[]; method `CanServe(module, band)`.

`ServiceModuleDef.cs` — module id, displayName, color, unlockCost, starterVehicleCount, starterVehicleType.

`RatingDef.cs` — streakFailDays (default 3), band thresholds at 0.2/0.4/0.6/0.8, deltas per band, minStarsForTips (3).

`HubDef.cs` — module, capacity, dailyUpkeep, relocateCost.

`DemandWaveDef.cs` — List of `{ daySecond, module, sizeBand, count }` entries.

`MapSkeleton.cs` — district unlock ids, hub slot transforms (Vector3[]), spline references for roads, blocked cells grid bounds.

- [ ] **Step 4: Create MVP SO assets in Editor**

Under `Assets/Game/Data/`:
- `Modules/CarModule.asset`, `Modules/FoodModule.asset`
- `Vehicles/Bicycle.asset`, `Vehicles/Motorbike.asset`, `Vehicles/FourSeater.asset`
- `Rating/DefaultRating.asset`
- `Hubs/CarHub.asset`, `Hubs/FoodHub.asset`
- `Maps/TutorialDistrict.asset` (empty skeleton stub)

- [ ] **Step 5: Run test — PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/Data Assets/Game/Data Assets/Game/Tests/Data
git commit -m "feat: add core ScriptableObject definitions and MVP data assets"
```

---

### Task 4: RunState & rating math

**Files:**
- Create: `Assets/Game/Scripts/Core/RunState.cs`
- Create: `Assets/Game/Scripts/Systems/RatingSystem.cs`
- Test: `Assets/Game/Tests/Systems/RatingSystemTests.cs`

**Interfaces:**
- Produces: `RunState.CurrentStars`, `RunState.ConsecutiveOneStarDays`, `RunState.Money`, `RunState.PeakProfit`.
- Produces: `RatingSystem.ApplyJobOutcome(float remainingPatienceFraction)`, `RatingSystem.SnapshotEndOfDay()`, `RatingSystem.ShouldFailStreak(RatingDef def)`.

- [ ] **Step 1: Write failing rating band test**

```csharp
[Test]
public void ApplyJobOutcome_80PercentRemaining_IncreasesStars()
{
    var def = CreateDefaultRatingDef();
    var state = new RunState { CurrentStars = 3f };
    var system = new RatingSystem(state, def);
    system.ApplyJobOutcome(0.85f);
    Assert.Greater(state.CurrentStars, 3f);
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement RunState + RatingSystem**

`RunState.cs`:

```csharp
public sealed class RunState
{
    public int DayIndex;
    public float CurrentStars = 3f;
    public int ConsecutiveOneStarDays;
    public float Money;
    public float CumulativeProfit;
    public float PeakProfit;
    public int SuccessfulJobs;
    public RunPhase Phase = RunPhase.Playing;
    public HashSet<ServiceModule> UnlockedModules = new();
}
```

`RatingSystem.cs` — map remaining fraction to band delta from `RatingDef`; clamp stars 1–5; `SnapshotEndOfDay()` rounds to int stars, updates streak; `ShouldFailStreak` returns true when `ConsecutiveOneStarDays >= def.streakFailDays`.

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Core/RunState.cs Assets/Game/Scripts/Systems/RatingSystem.cs Assets/Game/Tests/Systems
git commit -m "feat: add RunState and rating band system"
```

---

### Task 5: Road graph & map loader

**Files:**
- Create: `Assets/Game/Scripts/Map/RoadGraph.cs`
- Create: `Assets/Game/Scripts/Map/MapLoader.cs`
- Test: `Assets/Game/Tests/Map/RoadGraphTests.cs`

**Interfaces:**
- Produces: `RoadGraph.FindPath(int fromNodeId, int toNodeId) → IReadOnlyList<int>`
- Produces: `MapLoader.Load(MapSkeleton skeleton) → RoadGraph`

- [ ] **Step 1: Write failing path test**

Build a 3-node line graph A—B—C; assert path A→C is [A,B,C].

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement RoadGraph**

Nodes with Vector3 positions; edges with distance; BFS/A* for MVP (small graphs). Use ZLinq on span-backed neighbor lists where applicable.

`MapLoader` reads spline knots from `MapSkeleton` road splines → graph nodes at intersections/endpoints.

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Map Assets/Game/Tests/Map
git commit -m "feat: add road graph and map skeleton loader"
```

---

### Task 6: Day clock & EOD controller

**Files:**
- Create: `Assets/Game/Scripts/Core/DayClock.cs`
- Create: `Assets/Game/Scripts/Core/EodController.cs`
- Create: `Assets/Game/Scripts/Core/EodActionQueue.cs`
- Test: `Assets/Game/Tests/Core/EodControllerTests.cs`

**Interfaces:**
- Produces: `DayClock.DayFraction`, `DayClock.TimeScale`, `DayClock.IsPaused`, `DayClock.Advance(float deltaTime)`.
- Produces: `EodController.BeginEod()`, `EodController.ApplyQueue()`, `EodController.AdvanceDay()`.
- Consumes: `RunState`, `RatingSystem`, event channels `DayAdvancedEvent`, `EodStartedEvent`.

- [ ] **Step 1: Write failing EOD streak test**

Simulate 3 EOD snapshots at 1★ → `RunState.Phase == Failed`.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement DayClock**

Fields: dayLengthSeconds (SO), dayFraction 0–1, timeScale (1/2/3), isPaused. `Advance` multiplies delta by timeScale unless paused.

At dayFraction >= 1 → raise `DayEndedEvent`.

- [ ] **Step 4: Implement EodController**

Order: snapshot rating → fail check → apply `EodActionQueue` (upgrade/scrap/network/relocate start) → set phase `EodIntervention` unless skip → on confirm `AdvanceDay()`.

`EodActionQueue` holds pending actions added during day; re-validates affordability at apply.

- [ ] **Step 5: Run tests — PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/Core/DayClock.cs Assets/Game/Scripts/Core/EodController.cs Assets/Game/Scripts/Core/EodActionQueue.cs Assets/Game/Tests/Core
git commit -m "feat: add day clock and EOD controller with fail checks"
```

---

### Task 7: Fleet manager & vehicle instances

**Files:**
- Create: `Assets/Game/Scripts/Fleet/VehicleInstance.cs`
- Create: `Assets/Game/Scripts/Fleet/FleetManager.cs`
- Test: `Assets/Game/Tests/Fleet/FleetManagerTests.cs`

**Interfaces:**
- Produces: `FleetManager.BuyVehicle(ServiceModule, VehicleDef)`, `RepairVehicle(int id)`, `QueueScrap(int id)`, `QueueUpgrade(ServiceModule, VehicleType)`.
- Produces: `VehicleInstance.TryAssignOrder(OrderInstance)`, `VehicleInstance.CompleteJob()`, `VehicleInstance.IsDispatchEligible`.

- [ ] **Step 1: Write failing durability test**

Complete job → durability decreases; at 0 → `IsDispatchEligible == false`.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement VehicleInstance**

Fields: id, def ref, module, state, durability, cooldownRemaining, currentOrderId. `CompleteJob` consumes durability, starts cooldown timer.

- [ ] **Step 4: Implement FleetManager**

Dictionary keyed by `(ServiceModule, VehicleType)`. Buy/repair continuous. Scrap/upgrade via EodActionQueue. Upgrade cost = baseCost × fleetCount (no durability refresh).

- [ ] **Step 5: Run tests — PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/Fleet Assets/Game/Tests/Fleet
git commit -m "feat: add fleet manager and vehicle durability"
```

---

### Task 8: Orders, patience & demand spawner

**Files:**
- Create: `Assets/Game/Scripts/Demand/OrderInstance.cs`
- Create: `Assets/Game/Scripts/Demand/DemandSpawner.cs`
- Create: `Assets/Game/Scripts/Demand/DemandCheckpoint.cs`
- Test: `Assets/Game/Tests/Demand/DemandSpawnerTests.cs`

**Interfaces:**
- Produces: `OrderInstance.TickPatience(float dt)`, `OrderInstance.RemainingFraction`.
- Produces: `DemandSpawner.Tick(dayFraction)`, `DemandSpawner.GetUpcomingCheckpoints() → IReadOnlyList<DemandCheckpoint>`.

- [ ] **Step 1: Write failing spawn test**

Given `DemandWaveDef` with entry at dayFraction 0.1, after tick past 0.1 → one order exists.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement OrderInstance**

Fields: id, module, sizeBand, pickupNode, dropoffNode, patienceTotal, patienceRemaining, graceRemaining, state. Patience ticks from spawn; grace extends effective total.

- [ ] **Step 4: Implement DemandSpawner**

Reads `DemandWaveDef`; spawns regardless of fleet (pressure design). Exposes next 3 checkpoints for UI. Applies day/night multiplier from `ServiceModuleDef` curves (simple AnimationCurve on SO).

- [ ] **Step 5: Run tests — PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/Demand Assets/Game/Tests/Demand
git commit -m "feat: add orders, patience, and time-based demand spawner"
```

---

### Task 9: Dispatch service

**Files:**
- Create: `Assets/Game/Scripts/Dispatch/DispatchService.cs`
- Test: `Assets/Game/Tests/Dispatch/DispatchServiceTests.cs`

**Interfaces:**
- Consumes: `FleetManager`, `RoadGraph`, pending orders list.
- Produces: `DispatchService.Tick()` — assigns nearest eligible idle vehicle per unassigned order.

- [ ] **Step 1: Write failing assign test**

One idle motorbike (Car, 1 pax), one 1-pax Car order → assigned after Tick.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement DispatchService**

For each unassigned order: filter vehicles by module, `VehicleDef.CanServe`, durability > 0, cooldown == 0, range check via graph distance estimate; pick nearest idle. Raise `OrderAssignedEvent`.

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Dispatch Assets/Game/Tests/Dispatch
git commit -m "feat: add auto-dispatch service"
```

---

### Task 10: Vehicle path agent (splines)

**Files:**
- Create: `Assets/Game/Scripts/Dispatch/VehiclePathAgent.cs`
- Modify: `Assets/Game/Scripts/Fleet/VehicleInstance.cs` (hook path agent)

**Interfaces:**
- Produces: `VehiclePathAgent.SetPath(IReadOnlyList<int> nodeIds, RoadGraph graph)`, `VehiclePathAgent.Tick(float dt) → bool` (true when arrived).

- [ ] **Step 1: Implement VehiclePathAgent**

Move transform along road spline segments between graph nodes at `VehicleDef.speed`. On arrival at dropoff → call `FleetManager.CompleteOrder`.

Use Unity Splines if skeleton roads are splines; fallback lerp between node positions for MVP.

- [ ] **Step 2: Wire to DispatchService**

On assign: compute path, start agent, set vehicle state EnRoute.

- [ ] **Step 3: Play Mode smoke test**

Scene with 2 nodes, 1 vehicle, 1 order → vehicle reaches destination, order completes, durability drops.

- [ ] **Step 4: Commit**

```bash
git add Assets/Game/Scripts/Dispatch/VehiclePathAgent.cs
git commit -m "feat: add spline path following for vehicles"
```

---

### Task 11: Economy system (fares, tips, upkeep)

**Files:**
- Create: `Assets/Game/Scripts/Systems/EconomySystem.cs`
- Test: `Assets/Game/Tests/Systems/EconomySystemTests.cs`

**Interfaces:**
- Produces: `EconomySystem.OnJobCompleted(OrderInstance, float currentStars)`, `EconomySystem.ApplyDailyUpkeep()`.
- Updates: `RunState.Money`, `RunState.CumulativeProfit`, `RunState.PeakProfit`.

- [ ] **Step 1: Write failing tip test**

At 2★ → no tip; at 4★ → tip > 0.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement EconomySystem**

Fare on complete; tip if `Mathf.RoundToInt(currentStars) >= ratingDef.minStarsForTips`; track peak cumulative profit.

- [ ] **Step 4: Hook daily hub upkeep in EodController.ApplyQueue**

- [ ] **Step 5: Run tests — PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Scripts/Systems/EconomySystem.cs Assets/Game/Tests/Systems
git commit -m "feat: add economy fares, tips, and upkeep"
```

---

### Task 12: Hub manager (place, close, relocate, city queue)

**Files:**
- Create: `Assets/Game/Scripts/Hubs/HubInstance.cs`
- Create: `Assets/Game/Scripts/Hubs/HubManager.cs`
- Test: `Assets/Game/Tests/Hubs/HubManagerTests.cs`

**Interfaces:**
- Produces: `HubManager.PlaceHub(HubDef, slotId)`, `HubManager.QueueRelocate(hubId, newSlotId)` (EOD), `HubManager.Tick()`.
- Only one hub globally in `Closing` state; city queue holds orders with ticking patience.

- [ ] **Step 1: Write failing sole-hub city queue test**

Close only hub → new demand goes to city queue → patience decreases.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement HubManager**

Slots unlocked per district. Relocate: EOD pay fee → Closing → drain pending → instant move → Active. Cancel → refund none, restore queue to hub.

Transfer warning when multiple hubs (log/UI hook).

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Hubs Assets/Game/Tests/Hubs
git commit -m "feat: add hub placement, relocation, and city queue"
```

---

### Task 13: Overload fail condition

**Files:**
- Create: `Assets/Game/Scripts/Systems/OverloadSystem.cs`
- Test: `Assets/Game/Tests/Systems/OverloadSystemTests.cs`

**Interfaces:**
- Produces: `OverloadSystem.Tick()` — sums unassigned orders across hubs + city queue vs capacity SO threshold → sets `RunState.Phase = Failed`.

- [ ] **Step 1: Write failing overload test**

Orders exceed capacity → Phase Failed.

- [ ] **Step 2: Implement OverloadSystem**

- [ ] **Step 3: Run tests — PASS**

- [ ] **Step 4: Commit**

```bash
git add Assets/Game/Scripts/Systems/OverloadSystem.cs Assets/Game/Tests/Systems/OverloadSystemTests.cs
git commit -m "feat: add overload fail condition"
```

---

### Task 14: Camera & input

**Files:**
- Create: `Assets/Game/Scripts/Input/GameInputReader.cs`
- Create: `Assets/Game/Scripts/Camera/OrthoPanZoomCamera.cs`
- Modify: `Assets/InputSystem_Actions.inputactions` (add Pan, Zoom, Speed, Pause actions)

**Interfaces:**
- Produces: `OrthoPanZoomCamera.OnPan(Vector2 delta)`, `OnZoom(float delta)` with LitMotion `OutQuad` damped zoom (no bounce).

- [ ] **Step 1: Add Input actions**

Pan = Mouse Right Button + delta; Zoom = Mouse Scroll; Pause = Space; SpeedCycle = Tab.

- [ ] **Step 2: Implement OrthoPanZoomCamera**

Orthographic; clamp zoom min/max; RMB drag pans in XZ (top-down).

- [ ] **Step 3: Wire GameInputReader to DayClock timeScale + pause**

- [ ] **Step 4: Play Mode verify pan/zoom/pause/speed**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Input Assets/Game/Scripts/Camera Assets/InputSystem_Actions.inputactions
git commit -m "feat: add orthographic pan/zoom camera and game input"
```

---

### Task 15: UI — HUD, checkpoints, EOD panel

**Files:**
- Create: `Assets/Game/Scripts/UI/GameHud.cs`
- Create: `Assets/Game/Scripts/UI/DemandCheckpointPanel.cs`
- Create: `Assets/Game/Scripts/UI/EodPanel.cs`
- Create: `Assets/Game/Scripts/UI/ModulePurchasePanel.cs`

**Interfaces:**
- Consumes: `RunState`, `DemandSpawner.GetUpcomingCheckpoints()`, `EodController`.

- [ ] **Step 1: GameHud**

Display money, stars, day #, clock (`HH:MM` from day fraction, day starts 06:00) with period label (Morning/Day/Evening/Night), day-progress fill bar, speed indicator.

- [ ] **Step 2: DemandCheckpointPanel**

List next 3 waves: module, size band, ETA.

- [ ] **Step 3: EodPanel**

Show day summary; buttons for queued upgrades/scrap/network; Continue + Skip toggle (disabled until tutorial complete flag in save stub).

- [ ] **Step 4: ModulePurchasePanel**

Buy module when gate + budget allow; first module free choice in Endless (PlayerPrefs stub for MVP).

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/UI
git commit -m "feat: add HUD, demand checkpoints, and EOD UI"
```

---

### Task 16: Game bootstrap & MVP scene integration

**Files:**
- Create: `Assets/Game/Scripts/Core/GameBootstrap.cs`
- Create: `Assets/Game/Scenes/MVP_District.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset` (add scene)

**Interfaces:**
- Produces: wired scene with all systems ticking in Play Mode.

- [ ] **Step 1: Implement GameBootstrap**

MonoBehaviour `[SerializeField]` refs to all systems; `Update()` calls DayClock → Demand → Dispatch → PathAgents → Overload in order; listens to DayEnded → EodController.

- [ ] **Step 2: Build MVP_District scene**

Placeholder ground plane, 1 district skeleton, 2 hub slots, simple spline roads, bootstrap object, UI canvas, ortho camera.

- [ ] **Step 3: Create event channel assets**

`Assets/Game/Data/Events/` — DayEnded, OrderCompleted, RunFailed, etc.

- [ ] **Step 4: Play Mode acceptance test (manual checklist)**

- [ ] Car module spawns rides; buy Food module; food orders spawn
- [ ] Auto-dispatch assigns vehicles
- [ ] Patience/rating/tips behave
- [ ] Overload or 1★ streak ends run
- [ ] EOD pauses; upgrade/scrap apply; next day starts
- [ ] Pan/zoom/pause/speed work

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scenes Assets/Game/Scripts/Core/GameBootstrap.cs ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: integrate MVP vertical slice scene"
```

---

### Task 17: Addressables setup for maps

**Files:**
- Create: `Assets/AddressableAssetsData/` (via Unity Addressables window)
- Modify: `Assets/Game/Scripts/Map/MapLoader.cs` (Addressables load path)

- [ ] **Step 1: Install Addressables groups**

Group `Maps` → mark `TutorialDistrict` MapSkeleton addressable.

- [ ] **Step 2: MapLoader async load**

```csharp
public async Awaitable<RoadGraph> LoadAsync(string address)
{
    var handle = Addressables.LoadAssetAsync<MapSkeleton>(address);
    var skeleton = await handle.Task;
    return BuildGraph(skeleton);
}
```

- [ ] **Step 3: GameBootstrap uses `LoadAsync("Maps/TutorialDistrict")`**

- [ ] **Step 4: Commit**

```bash
git add Assets/AddressableAssetsData Assets/Game/Scripts/Map
git commit -m "feat: load map skeletons via Addressables"
```

---

## Slice 2 backlog (separate plan after MVP validates)

| Item | Notes |
|------|-------|
| Delivery module + size tiers | Full vehicle matrix |
| 6-seater + dispatch for 4–6 band | |
| Endless procgen on skeleton | Seeded lot fill |
| Local leaderboards (3 tabs) | Submit on run end |
| Scenario mode + tutorial script | Tooltip pauses |
| Steamworks integration | |
| Low-star demand heat | Endless only |
| Save system | Module unlocks, settings, skip EOD flag |

---

## Spec coverage self-review

| Spec section | Task |
|--------------|------|
| Modular Car/Food services | 3, 7, 8, 9 |
| Auto-dispatch | 9, 10 |
| Patience + 20% rating bands | 4, 8 |
| Tips at 3★+ only | 11 |
| Overload + 1★ streak | 4, 6, 13 |
| EOD vs continuous actions | 6, 7, 12, 15 |
| Fleet upgrade no dur refresh | 7 |
| Hub relocate + city queue | 12 |
| Day/night + speed | 6, 8, 14 |
| Hybrid map (authored MVP) | 5, 17 |
| SO data + event channels | 2, 3 |
| Camera pan/zoom | 14 |
| Delivery / endless / Steam | Slice 2 backlog |

**Placeholder scan:** Balance numbers live in SO assets (Editor-tuned), not hardcoded TBDs in code tasks.

---

## Execution options

**Plan saved to:** `docs/superpowers/plans/2026-07-21-traffic-management-sim-mvp.md`

**1. Subagent-Driven (recommended)** — Fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Implement tasks in this session with checkpoints for your review.

Which approach do you want?
