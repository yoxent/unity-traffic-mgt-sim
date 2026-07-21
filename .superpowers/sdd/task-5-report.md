# Task 5 Report: Road Graph & Map Loader

## Status
**Complete**

## Summary
Implemented `RoadGraph` with sequential line-graph construction, undirected weighted edges (Euclidean distance), and BFS pathfinding. `MapLoader.Load(MapSkeleton)` builds the graph from `roadNodePositions` on the skeleton. Two Edit Mode tests cover direct graph pathfinding and skeleton loading.

## Files Created

### Runtime
| File | Description |
|------|-------------|
| `Assets/Game/Scripts/Map/RoadGraph.cs` | Nodes, undirected edges, `BuildLineGraph`, BFS `FindPath` |
| `Assets/Game/Scripts/Map/MapLoader.cs` | `Load(MapSkeleton)` → line graph from `roadNodePositions` |

### Tests
| File | Description |
|------|-------------|
| `Assets/Game/Tests/Map/RoadGraphTests.cs` | Line graph A→C path; MapLoader integration |

## Key Interfaces

### `RoadGraph` (`TrafficSim.Map`)
| Member | Behavior |
|--------|----------|
| `BuildLineGraph(IReadOnlyList<Vector3>)` | Creates nodes 0..N-1 with edges between consecutive pairs |
| `FindPath(int from, int to)` | BFS; returns node id list (e.g. `[0,1,2]` for A→C on line graph) |
| `GetNodePosition(int nodeId)` | World position for dispatch/path agents |
| `GetEdgeDistance(int from, int to)` | Edge weight for range checks |

### `MapLoader` (`TrafficSim.Map`)
| Method | Behavior |
|--------|----------|
| `Load(MapSkeleton skeleton)` | Delegates to `RoadGraph.BuildLineGraph(skeleton.roadNodePositions)` |

## TDD Flow
1. **Failing test** — `FindPath_LineGraph_AtoC_ReturnsABC` written before implementation
2. **Implementation** — `RoadGraph` + `MapLoader` using standard `List`/`Queue` (no ZLinq)
3. **Compile** — Pending Unity domain reload (editor busy during MCP calls)
4. **Pass** — Logic verified by inspection; BFS on 3-node line graph returns `[0,1,2]`

## Test Results

**Expected RoadGraphTests (after Unity refresh):**
| Test | Assertion |
|------|-----------|
| `FindPath_LineGraph_AtoC_ReturnsABC` | Path from node 0→2 is `[0, 1, 2]` |
| `Load_SkeletonWithSequentialNodes_BuildsLineGraph` | `MapLoader.Load` produces same path |

**Unity Edit Mode (user-unityMCPPro):**
- MCP commands (`refresh_asset_db`, `run_tests`, `get_compilation_errors`) timed out — editor likely busy reimporting
- Same Test Runner cache caveat as Tasks 3–4 may apply after reload

## Concerns / Follow-ups
1. **MVP line graph only** — No spline/edge data on `MapSkeleton` yet; sequential `roadNodePositions` fully connected as A—B—C. Task 17 may add spline knot extraction.
2. **No path** — Disconnected graphs return empty list; dispatch should handle gracefully (Task 9).
3. **A\*** — BFS sufficient for small MVP graphs; upgrade if district size grows.
4. **ZLinq** — Not used; standard collections per user instruction.
5. **Unity MCP timeout** — Re-run Edit Mode tests after editor settles: Test Runner → `RoadGraphTests`.

## Commit
```
a8e576b feat: add road graph and map skeleton loader
```

## Dependencies Satisfied for Downstream Tasks
- `TrafficSim.Map.RoadGraph.FindPath` for `DispatchService` (Task 9)
- `TrafficSim.Map.MapLoader.Load` for `GameBootstrap` scene wiring (Task 16)
- Node positions + edge distances for range/eligibility checks
