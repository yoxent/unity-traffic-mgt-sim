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

Implement SO defs per plan:
- VehicleDef: type, color, speed, maxRange, maxDurability, durabilityLossPerJob, repairCost, purchaseCost, cooldownSeconds, allowedModules[], allowedSizeBands[]; CanServe(module, band)
- ServiceModuleDef: module id, displayName, color, unlockCost, starterVehicleCount, starterVehicleType
- RatingDef: streakFailDays (default 3), band thresholds 0.2/0.4/0.6/0.8, deltas per band, minStarsForTips (3), GetRatingDelta(float remainingFraction)
- HubDef: module, capacity, dailyUpkeep, relocateCost
- DemandWaveDef: serializable list of daySecond, module, sizeBand, count
- MapSkeleton: district unlock ids, hub slot positions, road spline container refs or node positions, blocked grid bounds

Create MVP assets under Assets/Game/Data/: Modules/CarModule, FoodModule; Vehicles/Bicycle, Motorbike, FourSeater; Rating/DefaultRating; Hubs/CarHub, FoodHub; Maps/TutorialDistrict

TDD: VehicleEligibilityTests.Bicycle_ServesFoodSmallOnly per plan.

Commit: feat: add core ScriptableObject definitions and MVP data assets
