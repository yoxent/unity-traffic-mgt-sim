### Task 4: RunState & rating math

Create RunState.cs, RatingSystem.cs, RatingSystemTests.cs per plan.
RunState: DayIndex, CurrentStars=3f, ConsecutiveOneStarDays, Money, CumulativeProfit, PeakProfit, SuccessfulJobs, Phase, UnlockedModules HashSet
RatingSystem: ApplyJobOutcome(remainingFraction), SnapshotEndOfDay(), ShouldFailStreak(RatingDef)
Test: ApplyJobOutcome_80PercentRemaining_IncreasesStars
Commit: feat: add RunState and rating band system
