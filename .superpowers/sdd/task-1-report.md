# Task 1 Report: Project foundation & packages

**Status:** DONE_WITH_CONCERNS  
**Date:** 2026-07-21  
**Commit:** chore: add Game foundation, packages, and shared enums

---

## Summary

Implemented Task 1 per brief: added Splines, Addressables, and ZLinq packages; created `Game.Runtime` and `Game.Tests` assemblies; added shared domain enums under `TrafficSim.Core`.

---

## Files Created / Modified

| Action | Path |
|--------|------|
| Modified | `Packages/manifest.json` |
| Created | `Assets/Game/Scripts/Game.Runtime.asmdef` |
| Created | `Assets/Game/Tests/Game.Tests.asmdef` |
| Created | `Assets/Game/Scripts/Core/Enums.cs` |

### Packages added to manifest

- `com.unity.splines`: 2.8.2
- `com.unity.addressables`: 2.7.4
- `com.cysharp.zlinq`: Git URL (ZLinq.Unity)

Pre-existing packages confirmed: LitMotion, Input System, URP, test-framework.

---

## Shared Enums (`TrafficSim.Core`)

| Enum | Values |
|------|--------|
| `ServiceModule` | Car, Food, Delivery |
| `VehicleType` | Bicycle, Motorbike, FourSeater, SixSeater |
| `JobSizeBand` | Small, OnePassenger, OneToFourPassengers, FourToSixPassengers, MediumDelivery, LargeDelivery |
| `VehicleState` | Idle, EnRoute, Cooldown, Offline |
| `HubState` | Active, Closing, Relocating |
| `RunPhase` | Playing, EodIntervention, Failed, Won |

---

## Assembly Definitions

### Game.Runtime

- Namespace: `TrafficSim`
- References: Unity.InputSystem, Unity.Splines, Unity.Mathematics, LitMotion, ZLinq.Unity

### Game.Tests

- Namespace: `TrafficSim.Tests`
- Editor-only test assembly with TestAssemblies optional reference
- References: Game.Runtime, UnityEngine.TestRunner, UnityEditor.TestRunner

---

## Verification

| Check | Result |
|-------|--------|
| Unity Editor connected (MCP) | Yes |
| AssetDatabase refresh | Success |
| `Game.Runtime` C# compile errors | 0 |
| `Game.Runtime` warnings | 0 |
| Console errors | 1 (Game.Tests asmdef duplicate refs) |

Unity reported:

> Assembly has duplicate references: UnityEngine.TestRunner, UnityEditor.TestRunner (Assets/Game/Tests/Game.Tests.asmdef)

This is expected when both `optionalUnityReferences: ["TestAssemblies"]` and explicit TestRunner references are present. Brief Step 5 criterion ("0 errors in Game.Runtime") is met. `Game.Tests` will not compile until duplicate refs are resolved (remove explicit TestRunner refs or drop `TestAssemblies`).

---

## Self-Review

### Matches brief

- [x] manifest.json updated with exact three package entries
- [x] Game.Runtime.asmdef content matches brief verbatim
- [x] Game.Tests.asmdef content matches brief verbatim
- [x] Enums.cs content matches brief verbatim
- [x] Commit message matches brief

### Notes

- `Packages/packages-lock.json` updated by Unity package resolution but not included in commit (brief `git add` scope was `manifest.json` + `Assets/Game/` only). Consider staging lock file in a follow-up for reproducible resolves.
- Unity auto-generated `.meta` files for `Assets/Game/` (not committed per brief scope).
- No runtime scripts beyond enums; no SO assets touched.

---

## Concerns

1. **Game.Tests asmdef duplicate references** — Console error blocks test assembly compilation. Fix: remove `UnityEngine.TestRunner` and `UnityEditor.TestRunner` from `references` (TestAssemblies injects them), or remove `optionalUnityReferences`. Deferred to avoid deviating from brief's exact asmdef content.
2. **packages-lock.json unstaged** — May cause package version drift across clones until committed.

---

## Next Task Dependencies Satisfied

Later tasks can import:

- `TrafficSim.Core` enums
- `Game.Runtime` assembly with Splines, Input System, LitMotion, ZLinq references
- Addressables package available project-wide
