# Traffic Management Sim — Agent Guide

Unity 6000.3 · 2D top-down · Grab-style traffic management (Car / Food / Delivery).

## Source of truth (read first)

| Doc | Purpose |
|-----|---------|
| `docs/superpowers/specs/2026-07-21-traffic-management-sim-design.md` | Approved design (Status: Approved) |
| `docs/superpowers/plans/2026-07-21-traffic-management-sim-mvp.md` | MVP task plan (17 tasks) |
| `AGENTS.md` (this file) | Architecture + agent conventions |

Use **SCE MCP** (`search_knowledge`) for indexed patterns before inventing APIs. If SCE returns empty, rely on spec/plan and live codebase.

## Architecture

```
GameBootstrap (MonoBehaviour, scene entry)
  → MapLoader (sync fallback / Addressables async)
  → SimComposition.Build() → SimSession
  → SimLoop (Update tick)
  → SimEventBridge (clock/EOD → SO channels, IDisposable)
```

- **Sim systems:** plain C# classes, constructor-injected, no DI container in MVP.
- **Data:** ScriptableObject definitions in `Assets/Game/Data/` — **never mutate SO assets at runtime**; use `RunState`.
- **Events:** typed SO channels in `Assets/Game/Scripts/Events/`.
- **Boundaries:** prefer `IDispatchService`, `IHubManager`, `IDemandSource` at cross-system edges.

## Tech stack (MVP)

| Area | Choice |
|------|--------|
| Render | URP 2D orthographic |
| Input | Unity Input System |
| UI | **uGUI (Canvas)** + **TextMeshPro** — HUD (money/stars/day/clock + day progress), EOD panel, module purchase, demand checkpoints |
| Animation | LitMotion (camera zoom `OutQuad`, no bounce) |
| Paths | Unity Splines (Slice 2); MVP uses node lerp |
| Queries | **ZLinq** via `TrafficSim.Core.Linq.SimLinq` helpers |
| Maps | Addressables group `Maps`, address `Maps/TutorialDistrict` |
| Tests | Unity Test Framework, Edit Mode, `Assets/Game/Tests/` |

**UI Toolkit:** not used in MVP. Panels use uGUI (`Button`, `Toggle`) with **TMP** text via `UiTextRef` (`TMP_Text`). Slice 2 may add Toolkit for complex menus.

## ZLinq

Install (both steps required):

1. NuGetForUnity + `Assets/packages.config` → `ZLinq` 1.5.6
2. Git package `com.cysharp.zlinq` (ZLinq.Unity)
3. `ZLinq.dll` in `Assets/Plugins/ZLinq/`

**Do not** scatter raw `.AsValueEnumerable()` everywhere. Add domain queries to:

`Assets/Game/Scripts/Core/Linq/SimLinq.cs`

Use SimLinq for: fleet/hub iteration, pending-order collection, nearest-node, dispatch nearest-vehicle. Plain `for` on `List<T>` is fine when already zero-alloc.

## Key paths

```
Assets/Game/Scripts/Core/     SimComposition, SimLoop, SimSession, RunState
Assets/Game/Scripts/Contracts/  IDispatchService, IHubManager, IDemandSource
Assets/Game/Scripts/Data/       SO definition types
Assets/Game/Scripts/Events/     SO event channels
Assets/Game/Scenes/             MVP_District.unity
Assets/Game/Editor/             AddressablesMapSetup menu
```

## Editor menus

- **TrafficSim → Setup → Addressables Maps** — registers `Maps/TutorialDistrict` (run once after clone)
- **TrafficSim → Setup → MVP District Scene** — builds Canvas UI + wires `GameBootstrap` refs

## Conventions

- Namespace root: `TrafficSim`
- Runtime asmdef: `Game.Runtime`; tests: `Game.Tests`; editor: `Game.Editor`
- Event subscriptions from MonoBehaviours: unsubscribe in `OnDestroy`; use `EventChannelSubscriptions` or `SimEventBridge` pattern
- Play-mode logs: `SimLog` → Console filter `[TrafficSim`
- Commits: only when user asks; follow existing `feat:` / `fix:` style
- No ECS in MVP; optional later

## MVP scope reminders

- Modules: Car + Food playable; Delivery enum only
- Vehicles: Bicycle, Motorbike, 4-seater
- Auto-dispatch, one job per vehicle
- Fail: overload immediate; 1★ streak N EODs
- EOD-only vs continuous actions per design spec

## Slice 2 (out of scope unless asked)

Delivery module, 6-seater, endless procgen, Steam, leaderboards, UI Toolkit migration, spline agents.
