# Traffic Management Sim — Design Spec

**Date:** 2026-07-21  
**Status:** Approved  
**Platform:** Windows (Steam, buy-to-play, single-player)  
**Engine:** Unity 6000.3.18f1, URP, 2D top-down

---

## 1. Vision

A minimalist, bright, bouncy **Grab-style logistics management sim**. The player runs a multi-service platform (Car, Food, Delivery) in a growing 2D city. Roads are mostly fixed; the player expands districts, places hubs, buys color-coded fleets, and responds to rising demand. Dispatch is automatic. Success is measured by surviving overload pressure while maintaining star rating and profit.

**Visual target:** Mini Motorways / Fly Corp — clean, colorful, readable at a glance.

**Not in scope for MVP:** multiplayer, IAP, free-to-play, food spoilage, road-drawing as primary verb, congestion/jam simulation, catering module.

---

## 2. Design pillars

1. **Modular services** — Car, Food, Delivery share city/road graph and core systems but have separate fleets and demand profiles.
2. **Auto-dispatch** — player manages supply (fleet, hubs, network), not individual trip assignment.
3. **Pressure through supply/demand** — time-based order waves continue even when the player lacks the right fleet; unserved demand drives overload and low stars.
4. **Meaningful EOD decisions** — strategic purchases and upgrades happen at day boundaries; daytime is execution and reaction.
5. **Hybrid content** — authored map skeletons with deterministic scenarios and procedural endless variety.

---

## 3. Core gameplay loop

### During the day (continuous)

1. Demand spawns on a **time-based schedule** (checkpoints preview upcoming waves: module, size band, approximate count).
2. Auto-dispatch assigns eligible idle vehicles to jobs.
3. Vehicles pathfind on the road graph; one job at a time per vehicle.
4. Job completes → fare income, durability consumed, optional tip (if stars allow), rating delta from remaining patience.
5. Player may: buy vehicles, repair, buy modules (when gate allows), place hubs (on unlocked slots), pause/speed (1x/2x/3x), start hub close (relocate queued for EOD), camera pan/zoom.
6. Day/night shifts demand weights per module.

### End of day (EOD phase)

1. Resolve day-end bookkeeping.
2. **Fail checks** (overload already ends run immediately; at EOD: 1★ streak, scenario fail).
3. **Scenario win check** (if applicable).
4. **Apply EOD queue:** fleet upgrades, scrap, network unlock, start relocations (pay fee).
5. **EOD intervention UI** (unless Skip enabled): review stats, confirm queued actions, plan next day.
6. Advance to next calendar day; newly unlocked districts reveal hub slots.

### Hard fail conditions

| Condition | When |
|-----------|------|
| **Overload** | Unanswered demand at hub/customer/city queue exceeds capacity meter (immediate) |
| **1★ streak** | End-of-day star rating is 1★ for **N consecutive days** (N tunable via SO; any day ending ≥2★ resets streak) |

### Soft pressure (no instant fail)

- Low star rating → Endless demand heat (spawn rate ↑, patience windows ↓).
- No tips at 1–2★ (evaluated on job complete).
- Maintenance: durability loss per job, hub upkeep, repair costs.
- Money constraints on buying/repairing (bankruptcy is soft — cannot afford actions).

---

## 4. Services & modules

### Service modules

| Module | Job type | Default vehicle eligibility |
|--------|----------|----------------------------|
| **Car** | Passenger rides (1–6 pax) | Motorbike (1), 4-seater (1–4), 6-seater (4–6) |
| **Food** | Restaurant → customer | Bicycle, Motorbike |
| **Delivery** | Pickup → dropoff (size tiers) | Bicycle, Motorbike (small), 4-seater (medium), 6-seater (large) |

- Each module has **separate color-coded fleets** per vehicle type (e.g. Food-Bicycle fleet, Food-Motorbike fleet).
- **Strict size bands:** vehicles only take jobs within their band. No downsizing (6-seater cannot take 1-pax job).
- **One job at a time** per vehicle; multi-stop trips (pickup → dropoff) are a single job with one patience meter.

### Module unlock — Tutorial

Scripted first session on a small district:

1. Start with **Car** only.
2. Checkpoint → tooltip pause → **Buy Food Module** (includes starter fleet + brief demand ramp).
3. Checkpoint → **Buy Delivery Module**.
4. Tooltip-style pauses throughout; simulation paused during tips.

**Replay:** Settings → Replay Tutorial (sandbox; no meta/leaderboard writes).

### Module unlock — Endless

- Player **chooses** which module is **free on day 1**.
- Remaining modules: **1 paid purchase per 15 calendar days** (whole-day availability, e.g. day 15+).
- Cooldown starts on **paid** unlock only; free first module does not start the timer.
- All three modules are buyable from day 1 subject to budget and 15-day gate.
- Starting **budget**; player chooses expansion path.

### Module unlock — Scenarios

Defined per `ScenarioDef` (which modules available, fixed seed, win conditions).

---

## 5. Vehicles & fleets

### Vehicle types

| Type | Modules | Passenger | Delivery size |
|------|---------|-----------|---------------|
| Bicycle | Food, Delivery | — | Small |
| Motorbike | Food, Delivery, Car | 1 | Small |
| 4-seater | Car, Delivery | 1–4 | Medium |
| 6-seater | Car, Delivery | 4–6 | Large |

Per-type stats (SO-configured): speed, range/distance capability, durability max, durability loss per job, maintenance/repair cost, next-job cooldown, purchase cost, color.

### Fleet model

- A **fleet** = one module × one vehicle type (e.g. Car-4-seater fleet).
- Buying vehicles adds to that type fleet (continuous, any time).
- **Fleet upgrade** (EOD only): raises stats for **all vehicles of that type fleet**. Cost scales with **vehicle count**. Does **not** refresh durability.
- **Durability:** consumed on job **complete** (never mid-job breakdown). At 0, vehicle excluded from dispatch until repaired.
- **Repair:** continuous, any time.
- **Scrap:** EOD only, idle/offline vehicles only, no refund.
- **Next-job cooldown:** per vehicle type; starts after job complete (cooldown persists through EOD upgrade).

### Dispatch eligibility

1. Correct module fleet.
2. Vehicle idle, durability > 0, cooldown elapsed.
3. Job size within vehicle band.
4. Vehicle within range of job (distance capability).
5. Nearest/suitable assignment (tie-break rule tunable; default: nearest idle).

---

## 6. Jobs, patience & rating

### Job spawn

- **Time-based waves** spawn regardless of fleet availability (intentional pressure).
- Spawn controller respects scenario/endless curves and **can** filter by owned modules; size bands may spawn before player owns matching vehicles.
- **Checkpoints UI** shows upcoming waves: time, module, size band, count estimate.

### Patience

- Patience meter starts at spawn.
- **Grace period** = extended patience (same meter, longer initial budget); after grace, normal decay continues.
- Late/expired jobs hurt rating; expired jobs fail (strong negative rating delta).

### Rating (5★ system)

- Platform star rating derived from rolling job outcomes.
- Each successful job contributes based on **remaining patience fraction** at completion:

| Remaining patience | Rating impact |
|--------------------|---------------|
| 80–100% | Strong positive |
| 60–80% | Positive |
| 40–60% | Neutral / tiny positive |
| 20–40% | Small negative |
| 0–20% | Larger negative |
| Expired / failed | Strong negative |

- **End-of-day snapshot** used for 1★ streak fail and scenario rating objectives.
- Mid-day flicker does not affect streak; only EOD count matters.

### Tips

- Awarded on job complete when **current platform stars ≥ 3★** (no tips at 1–2★).
- Tip amount scales with stars (tunable SO).
- High volume + tips must not outpace durability/upkeep sinks (balance).

### Endless low-star pressure (≤2★)

- Demand spawn rate increases.
- Patience windows shrink.
- No tips (already enforced at 1–2★).
- Light bonus durability loss at 1★ optional for post-MVP tuning.

---

## 7. Hubs & network

### Hubs

- Types: Car depot, food kitchen, parcel warehouse (per module).
- Buffer capacity contributes to overload calculation.
- **Daily upkeep** per hub (money sink).
- **Placement:** continuous, but only on **unlocked hub slots** (revealed when district unlocks at EOD).

### Relocation

- **Start relocate:** EOD only; pay fee (money cost, not framed as tax).
- Allowed with **pending jobs** (close-wait loop).
- **Only one hub globally** may be in closing state at a time.
- While closing: hub stops accepting new jobs; pending jobs finish; new demand **transfers to other hubs** or enters **city queue** (patience ticks).
- **Sole hub:** city queue holds demand during close/move (option C).
- **Complete:** instant when pending clear (may finish mid-day).
- **Cancel close:** anytime; hub reopens; city queue returns to hub; **no fee refund**.

### Network unlock

- **EOD only:** unlock road segments / districts.
- New slots available next morning.
- Demand checkpoints should preview post-unlock pressure.

---

## 8. Day/night & time controls

- In-game day/night cycle affects per-module demand weights (SO curves).
- Example defaults (tunable): Car peaks commute + nightlife; Food lunch/dinner; Delivery business hours.
- **Pause** freezes all sim clocks.
- **Speed:** 1x / 2x / 3x scales sim time (patience, wear, day clock, demand).
- **Visual:** palette shift bright → night (minimalist).

### EOD intervention

| Action | Timing |
|--------|--------|
| Fleet upgrade | EOD only |
| Scrap | EOD only |
| Network unlock | EOD only |
| Start relocation | EOD only |
| Buy vehicles | Continuous |
| Repair | Continuous |
| Buy module | Continuous (when gate + budget allow) |
| Place hub | Continuous (unlocked slots) |

- **Skip EOD UI:** available after tutorial; queued EOD actions still apply silently.
- First run forces EOD UI exposure.

### EOD order of operations

1. End active day simulation.
2. Compute EOD star snapshot → update 1★ streak → **fail if N consecutive 1★ days**.
3. Check overload (if not already failed).
4. Scenario fail conditions.
5. Scenario win conditions (finish-at-rating, survive X days, etc.).
6. Apply EOD queue (upgrade, scrap, network unlock, start relocations).
7. Show EOD intervention UI (unless skip).
8. Advance calendar day; apply day/night transition.

---

## 9. Map generation (hybrid)

### Map skeleton (authored, Addressable)

- Road graph (splines/grid), district bounds, blocked cells (water/parks).
- Hub slot markers, lot markers for building spawn.
- Network unlock gates.

### Mode behavior

| Mode | Generation |
|------|------------|
| Tutorial | Fully deterministic; fixed layout |
| Scenarios | Fixed seed; same layout every play |
| Endless | Seeded procedural fill on skeleton: building lots, demand spawn points, decor, minor side-street variation |

### Pipeline

1. Load `MapSkeleton` asset.
2. Apply deterministic bake or seeded proc fill.
3. Build pathfinding graph.
4. Spawn systems read graph + lot markers.

**MVP:** one authored tutorial district + one scenario skeleton. Endless procgen after vertical slice.

---

## 10. Game modes

### Tutorial

- One session; Car → Food → Delivery unlock sequence.
- Tooltip pauses; sandbox replay from Settings.

### Scenarios

- Fixed goals: survive X days, reach profit target, **finish at defined star rating**, unlock N districts.
- Deterministic maps/seeds.
- Fail-before-win at EOD.

### Endless

- Starting budget; player-chosen free module; hybrid proc map per run.
- **Leaderboards (tabbed, submit on run end only):**
  - Days survived
  - Peak cumulative profit (best peak during run; no min-days qualifier for MVP)
  - Successful jobs completed
- Steam integration deferred; architecture supports it.

---

## 11. Controls & camera

- **View:** 2D orthographic top-down (not isometric).
- **RMB + drag:** pan camera.
- **Mouse wheel:** zoom (clamped min/max; LitMotion `OutQuad` settle — no bounce/overshoot).
- **Keyboard + mouse** via Input System.
- Service colors on **fleet vehicles only**; destinations use icons/shapes (not destination color-coding).

---

## 12. Data architecture

### ScriptableObjects (immutable config)

| Asset | Purpose |
|-------|---------|
| `ServiceModuleDef` | Module id, color, unlock cost, day/night demand curves |
| `VehicleDef` | Type stats, eligibility, visuals |
| `HubDef` | Capacity, upkeep, module binding |
| `DistrictDef` / `MapSkeleton` | Roads, slots, unlock gates |
| `ScenarioDef` | Goals, seed, starting modules, day length |
| `EconomyDef` | Costs, fares, tip curves |
| `RatingDef` | Star bands, 1★ streak N, patience→rating table |
| `MaintenanceDef` | Durability, repair, hub upkeep |
| `DemandWaveDef` | Spawn schedules, checkpoint definitions |
| `TutorialStepDef` | Pauses, tips, checkpoint triggers |
| `FleetUpgradeDef` | Per-tier stat deltas, cost-per-vehicle multiplier |

### Event channels (SO)

Typed channels for cross-system decoupling: `OrderSpawned`, `OrderCompleted`, `RatingChanged`, `DayAdvanced`, `DayNightChanged`, `ModuleUnlocked`, `HubClosing`, `RunFailed`, `EODPhaseStarted`, etc.

**Do not** store mutable run state on SO assets (money, active orders, fleet instances, rating, day index).

### Runtime state

Plain C# / MonoBehaviour-owned state objects: `RunState`, `FleetInstance`, `OrderInstance`, `HubInstance`, `DayClock`, `RatingTracker`.

### ECS (future)

SO defs remain source of truth. If entity counts require it, bake SO data into ECS components/BlobAssets at play start. Systems handle sim; UI bridges via event channels. **Not required for MVP.**

---

## 13. Tech stack

### In project now

- Unity 6000.3.18f1, URP
- Input System
- LitMotion (zero-alloc tween)
- Unity MCP + SCE MCP (workflow)

### Add for MVP

| Package / pattern | Use |
|-------------------|-----|
| Unity Splines | Road paths, vehicle following |
| ZLinq | Zero-alloc queries on spans/lists |
| Addressables | Maps, vehicles, VFX, audio, tutorial content |
| SO data + SO event channels | Config + decoupling |
| UnityEngine.Pool | Vehicles, order markers, VFX |
| Unity Awaitable | Async loads, waits |

### Deferred

- Steamworks (achievements, cloud, leaderboards)
- Unity Localization
- ECS/DOTS
- Cinemachine (custom ortho pan/zoom sufficient)
- NavMesh (custom road graph)
- Catering module
- Congestion simulation

### Addressables groups (initial)

- `Maps/Skeletons`
- `Vehicles`
- `UI`
- `Audio`
- `Tutorial`
- Remote catalog for post-ship content updates (maps, scenarios)

---

## 14. Architecture (high level)

```
┌─────────────────────────────────────────────────────────┐
│  Presentation (UI, Camera, Tutorial, Checkpoints HUD)   │
├─────────────────────────────────────────────────────────┤
│  Event Channels (SO)                                    │
├──────────┬──────────┬──────────┬──────────┬─────────────┤
│ Dispatch │ Demand   │ Rating   │ Economy  │ Day/EOD     │
│ System   │ Spawn    │ System   │ System   │ Controller  │
├──────────┴──────────┴──────────┴──────────┴─────────────┤
│  Simulation (Fleet, Orders, Hubs, Pathfinding, Map)     │
├─────────────────────────────────────────────────────────┤
│  Data (SO defs) │ Addressables │ Map Skeleton + ProcGen │
└─────────────────────────────────────────────────────────┘
```

### Suggested folder layout

```
Assets/
  Game/
    Data/           # SO assets
    Scripts/
      Core/         # RunState, DayClock, EODController
      Modules/      # Car, Food, Delivery
      Fleet/        # VehicleInstance, FleetManager, Upgrade
      Demand/       # Spawn, Orders, Patience
      Dispatch/     # Auto-assign, path follow
      Map/          # Skeleton load, proc fill, graph
      Hubs/         # Placement, relocate, queue
      UI/
      Events/       # SO channel definitions
    Scenes/
    Addressables/
  Art/              # User-provided
  Settings/
```

---

## 15. MVP vertical slice

**Goal:** One small district, playable tutorial + one endless-style run.

### Includes

- Car + Food modules (Delivery in slice 2 or stubbed)
- Bicycle + Motorbike + one car type
- Authored skeleton, no endless procgen yet
- Auto-dispatch, patience, 5★ rating, overload fail, 1★ streak
- Day/night + EOD phase (upgrade, scrap, network unlock stub)
- Hub place + relocate flow
- Time-based demand + checkpoint UI
- Camera pan/zoom
- SO-driven vehicle/module defs

### Slice 2 (post-validate)

- Delivery module + size tiers
- Full vehicle roster (4-seater, 6-seater)
- Endless procgen + leaderboards (local)
- Scenario mode
- Steam hooks

---

## 16. Balance parameters (tunable via SO)

| Parameter | Notes |
|-----------|-------|
| 1★ streak days (N) | Default suggest: 3 |
| Day length (real seconds at 1x) | TBD in playtest |
| Starter budget (Endless) | TBD |
| Module costs + starter fleet bundles | TBD |
| Hub upkeep / repair costs | TBD |
| Tip % by star band | TBD |
| Low-star demand multiplier | TBD |
| Overload capacity thresholds | TBD |

---

## 17. Open items (post-MVP)

- Catering (Food, large orders → 4/6-seater eligibility extension)
- Congestion on shared roads
- Dispatch tie-break tuning
- Profit leaderboard min-days qualifier if padding exploited
- Bonus durability loss at 1★
- Steam achievements/cloud
- ECS migration trigger: profile when fleet+order count exceeds threshold

---

## 18. Competitor reference (research summary)

**Mini Motorways:** ~25–26 city maps, draw-road loop, trip-milestone unlocks, 165 achievements, no paid DLC, daily/weekly challenges, Endless/Creative/Expert modes.

**Fly Corp:** Single world map (~200 territories), scenario/daily/UGC modes, overload fail, 72 achievements, paid DLC (American Dream, Delivery).

**Our differentiation:** Grab-style multi-service (Car/Food/Delivery), fleet management + hub/network expansion (not road drawing), EOD strategic layer, star rating + tips economy, hybrid map pipeline.

---

## Revision history

| Date | Change |
|------|--------|
| 2026-07-21 | Initial spec from design sessions (Sections 1–3 frozen) |
