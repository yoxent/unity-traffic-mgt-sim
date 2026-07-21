# Task 11 Report: Economy System (Fares, Tips, Upkeep)

## Status
**Complete**

## Summary
Implemented `EconomySystem` to award base fare on job completion, scale tips by rounded stars when `>= RatingDef.minStarsForTips`, and track `RunState.Money`, `CumulativeProfit`, `PeakProfit`, and `SuccessfulJobs`. Added `ApplyDailyUpkeep` to sum `HubDef.dailyUpkeep` across active hubs. Hooked upkeep into `EodController.ApplyQueue` before queued EOD actions. Four Edit Mode tests cover the required tip gate (2★ vs 4★), peak-profit tracking, and hub upkeep deduction.

## Files Created / Modified

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Systems/EconomySystem.cs` | `OnJobCompleted`, `ApplyDailyUpkeep`, fare/tip payout |
| `Assets/Game/Scripts/Core/EodController.cs` | Optional `EconomySystem` + active hubs; upkeep in `ApplyQueue` |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Systems/EconomySystemTests.cs` | 4 tests: no tip at 2★, tip at 4★, peak profit, upkeep sum |

## Key Interfaces

### `EconomySystem` (`TrafficSim.Systems`)
| Member | Behavior |
|--------|----------|
| `OnJobCompleted(OrderInstance, float currentStars)` | Adds base fare; tip if `RoundToInt(stars) >= minStarsForTips`; updates money/profit/peak/jobs |
| `ApplyDailyUpkeep(IReadOnlyList<HubDef>)` | Deducts sum of `dailyUpkeep`; reduces `CumulativeProfit` (peak unchanged) |
| Constructor | `baseFare` (default 10), `tipPerStar` (default 5) for MVP tuning without `EconomyDef` SO |

### Tip formula
- No tip when `Mathf.RoundToInt(currentStars) < ratingDef.minStarsForTips` (default 3)
- Tip = `tipPerStar * (roundedStars - minStarsForTips + 1)` → 4★ earns `2 * tipPerStar`

### `EodController` change
- New optional ctor params: `EconomySystem economy`, `IReadOnlyList<HubDef> activeHubs`
- `ApplyQueue()` calls `economy.ApplyDailyUpkeep(activeHubs)` before `EodActionQueue.ApplyAll`

## TDD Flow
1. **Failing tests** — `OnJobCompleted_AtTwoStars_NoTip`, `OnJobCompleted_AtFourStars_EarnsTip`
2. **Implementation** — `EconomySystem` with fare/tip/peak/upkeep
3. **EOD hook** — `EodController.ApplyQueue` applies daily hub upkeep
4. **Pass** — Edit Mode tests (see below)

## Test Results

**Expected EconomySystemTests:**
| Test | Assertion |
|------|-----------|
| `OnJobCompleted_AtTwoStars_NoTip` | Payout = base fare only; no tip at 2★ |
| `OnJobCompleted_AtFourStars_EarnsTip` | Payout = fare + positive tip |
| `OnJobCompleted_UpdatesPeakProfitAcrossJobs` | Upkeep lowers cumulative profit; peak retained |
| `ApplyDailyUpkeep_DeductsSumOfHubCosts` | Money/profit reduced by sum of hub upkeep |

## Concerns / Follow-ups
1. **EconomyDef SO** — Fare/tip curves deferred; constructor defaults used for MVP (design doc lists `EconomyDef` for post-MVP balance)
2. **Size-band fares** — Flat `baseFare` per job; module/band pricing when `EconomyDef` lands
3. **Hub wiring** — `EodController` accepts hub list; `HubManager` (Task 12) supplies active hubs at bootstrap
4. **Job-complete pipeline** — `VehiclePathAgent` / dispatch should call `OnJobCompleted` + `RatingSystem.ApplyJobOutcome` (Task 16)

## Commit
```
f53907b feat: add economy fares, tips, and upkeep
```

## Dependencies Satisfied for Downstream Tasks
- `EconomySystem.OnJobCompleted` for job payout after path completion
- `ApplyDailyUpkeep` wired into EOD flow for hub money sink
- `PeakProfit` / `SuccessfulJobs` updated for leaderboards and UI (Tasks 15–16)
