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
