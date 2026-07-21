# Task 2 Report: ScriptableObject Event Channels

## Status
**Complete**

## Summary
Implemented ScriptableObject-based event channels for cross-system decoupling in the `TrafficSim.Events` namespace. All files created per task brief; TDD flow followed (failing test first, then implementation).

## Files Created

### Runtime (`Assets/Game/Scripts/Events/`)
| File | Description |
|------|-------------|
| `GameEventChannel.cs` | Parameterless event channel with `Register`/`Unregister`/`Raise()` |
| `IntEventChannel.cs` | Typed channel with `Action<int>` listeners |
| `OrderEventPayload.cs` | Readonly struct carrying `OrderId` + `ServiceModule` |
| `OrderEventChannel.cs` | Typed channel with `Action<OrderEventPayload>` listeners |

### Tests (`Assets/Game/Tests/Events/`)
| File | Description |
|------|-------------|
| `GameEventChannelTests.cs` | Edit Mode test verifying `Raise_InvokesRegisteredListener` |

## Design Notes
- All channel types use `[CreateAssetMenu]` under `TrafficSim/Events/…`
- Listeners stored in runtime-only `List<Action>` fields (not serialized) — no mutable run state on SO assets
- `Raise()` iterates listeners in reverse order (safe if a listener unregisters during invoke)
- `IntEventChannel` and `OrderEventChannel` mirror `GameEventChannel` pattern with typed payloads

## TDD Flow
1. **Step 1 — Failing test:** Created `GameEventChannelTests.Raise_InvokesRegisteredListener` (type not found)
2. **Step 2 — Implementation:** Added all four runtime event files
3. **Step 3 — Pass:** Unity Edit Mode test run confirmed pass

## Test Results

**Unity Edit Mode (via user-unityMCPPro `run_tests`):**

| Assembly | Test | Result |
|----------|------|--------|
| `Game.Tests.dll` | `TrafficSim.Tests.Events.GameEventChannelTests.Raise_InvokesRegisteredListener` | **Passed** |

Full Edit Mode run also included unrelated plugin tests (63 total in MCP plugin assembly, 2 pre-existing failures unrelated to this task).

## Concerns / Follow-ups
1. **Listener lifetime:** Channels hold strong references to registered delegates. Subscribers should `Unregister` on disable/destroy to avoid leaks.
2. **No tests yet for `IntEventChannel` / `OrderEventChannel`:** Brief only specified `GameEventChannel` test; typed channels follow identical pattern — add tests in a future task if desired.
3. **Shared SO assets across scenes:** Multiple systems registering on the same channel asset will all receive raises; document ownership when creating channel assets.

## Commit
```
a82b2dfc1c59f0722ea79e754d12cf2a2e641243 feat: add ScriptableObject event channels
```
