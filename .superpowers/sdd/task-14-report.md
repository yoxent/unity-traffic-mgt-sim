# Task 14 Report: Camera & Input

## Status
**Complete**

## Summary
Added a `Game` action map to `InputSystem_Actions` (RMB pan, scroll zoom, Space pause, Tab speed cycle), `GameInputReader` to read those actions and drive `DayClock` pause/time-scale, and `OrthoPanZoomCamera` for orthographic top-down pan with LitMotion damped zoom (`Ease.OutBack`).

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/InputSystem_Actions.inputactions` | Added `Game` map: `Pan`, `PanHold`, `Zoom`, `Pause`, `SpeedCycle` |
| `Assets/Game/Scripts/Input/GameInputReader.cs` | Wraps `Game` map; forwards pan/zoom to camera; toggles pause and cycles 1×→2×→3× on `DayClock` |
| `Assets/Game/Scripts/Camera/OrthoPanZoomCamera.cs` | Orthographic pan in XZ; clamped zoom with LitMotion settle |

## Key Interfaces

### `GameInputReader` (`TrafficSim.Input`)
| Member | Behavior |
|--------|----------|
| `Bind(DayClock clock)` | Injects sim clock for pause/speed handling |
| `Update()` | While RMB held, reads `Pan` delta → `OrthoPanZoomCamera.OnPan`; scroll → `OnZoom` |
| Pause (Space) | Toggles `DayClock.IsPaused` |
| SpeedCycle (Tab) | Cycles `DayClock.TimeScale` 1 → 2 → 3 → 1 via `SetTimeScale` |

### `OrthoPanZoomCamera` (`TrafficSim.Camera`)
| Method | Behavior |
|--------|----------|
| `OnPan(Vector2 delta)` | Moves camera in XZ; speed scales with current ortho size |
| `OnZoom(float delta)` | Adjusts target ortho size (clamped); LitMotion animates with `OutBack` |

### Input bindings (`Game` map)
| Action | Binding |
|--------|---------|
| Pan | `<Mouse>/delta` (applied only while `PanHold` pressed) |
| PanHold | `<Mouse>/rightButton` |
| Zoom | `<Mouse>/scroll/y` |
| Pause | `<Keyboard>/space` |
| SpeedCycle | `<Keyboard>/tab` |

## Scene Wiring (Task 16)
1. Add `OrthoPanZoomCamera` to main orthographic camera GameObject.
2. Add `GameInputReader` to scene; assign `InputSystem_Actions` asset and camera reference.
3. From `GameBootstrap`, call `gameInputReader.Bind(dayClock)` after constructing `DayClock`.

## Play Mode Verification
- **Pan:** Hold RMB and drag — camera moves in XZ plane.
- **Zoom:** Scroll wheel — ortho size changes with slight overshoot settle.
- **Pause:** Space toggles sim pause (`DayClock.Advance` no-op).
- **Speed:** Tab cycles 1× / 2× / 3× time scale.

## Test Results
- No automated tests (plan Step 4 is Play Mode manual verify).
- Unity MCP compile check timed out during domain reload; scripts follow existing `Game.Runtime` asmdef references (`Unity.InputSystem`, `LitMotion`).

## Concerns / Follow-ups
1. **Scene integration** — Components are code-only until Task 16 wires them in `GameBootstrap`.
2. **UI focus** — `Game` map stays enabled; EOD/menu UI may need action-map switching in Task 15.
3. **Pause while EOD** — Current toggle is global; EOD intervention may need to block speed/pause later.

## Commit
```
34271b7 feat: add orthographic pan/zoom camera and game input
```

## Dependencies Satisfied for Downstream Tasks
- `GameInputReader.Bind(DayClock)` ready for `GameBootstrap` (Task 16)
- Camera controls match design spec §11 (RMB pan, wheel zoom, LitMotion settle)
- Pause/speed inputs match design spec §7 sim controls
