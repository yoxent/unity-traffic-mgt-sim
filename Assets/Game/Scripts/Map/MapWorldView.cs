using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Fleet;
using TrafficSim.Hubs;
using UnityEngine;

namespace TrafficSim.Map
{
    /// <summary>
    /// Top-down (XZ) placeholder world: ground grid, blocked tiles, roads, hubs, vehicles.
    /// Integer X/Z coordinates are cell centers; see <see cref="MapGridSpec"/>.
    /// </summary>
    public sealed class MapWorldView : MonoBehaviour
    {
        [SerializeField] float _gridLineWidth = 0.04f;
        [SerializeField] Color _groundColor = new(0.16f, 0.18f, 0.16f, 1f);
        [SerializeField] Color _gridColor = new(0.32f, 0.36f, 0.34f, 0.9f);
        [SerializeField] Color _blockedColor = new(0.12f, 0.28f, 0.38f, 0.95f);
        [SerializeField] Color _roadColor = new(0.62f, 0.64f, 0.68f, 1f);
        [SerializeField] Color _emptySlotColor = new(0.4f, 0.42f, 0.44f, 0.55f);
        [SerializeField] Color _houseColor = new(0.78f, 0.72f, 0.62f, 1f);
        [SerializeField] GameObject _roadStraightPrefab;
        [SerializeField] GameObject _roadCornerPrefab;
        [SerializeField] GameObject _roadTJunctionPrefab;
        [SerializeField] GameObject _roadCrossPrefab;
        [SerializeField] float _roadHeight = 0.04f;

        static readonly Quaternion FlatOnXz = Quaternion.Euler(90f, 0f, 0f);

        SimSession _session;
        MapSkeleton _skeleton;
        Transform _gridRoot;
        Transform _roadsRoot;
        Transform _hubsRoot;
        Transform _housesRoot;
        Transform _vehiclesRoot;
        Sprite _tileSprite;
        Sprite _vehicleSprite;
        Material _lineMaterial;
        readonly Dictionary<int, SpriteRenderer> _hubVisuals = new();
        readonly Dictionary<int, SpriteRenderer> _vehicleVisuals = new();
        readonly HashSet<int> _seenVehicleIds = new();
        readonly HashSet<int> _seenHubIds = new();
        readonly List<int> _pruneBuffer = new();
        readonly HashSet<Vector2Int> _roadCells = new();

        public void Bind(SimSession session, MapSkeleton skeleton)
        {
            _session = session;
            _skeleton = skeleton;
            EnsureRoots();
            RebuildStaticGeometry();
            var nodeCount = skeleton?.roadNodePositions?.Length ?? 0;
            var slotCount = skeleton?.hubSlotPositions?.Length ?? 0;
            var houseCount = skeleton?.houseLots?.Length ?? 0;
            SimLog.MapInfo($"MapWorldView bound nodes={nodeCount} hubSlots={slotCount} houses={houseCount}");
        }

        void LateUpdate()
        {
            if (_session == null)
                return;

            RefreshHubs();
            RefreshVehicles();
        }

        void OnDestroy()
        {
            if (_tileSprite != null)
                Destroy(_tileSprite.texture);

            if (_vehicleSprite != null && _vehicleSprite.texture != _tileSprite?.texture)
                Destroy(_vehicleSprite.texture);

            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        void EnsureRoots()
        {
            if (_gridRoot == null)
                _gridRoot = CreateChildRoot("Grid");
            if (_roadsRoot == null)
                _roadsRoot = CreateChildRoot("Roads");
            if (_hubsRoot == null)
                _hubsRoot = CreateChildRoot("Hubs");
            if (_housesRoot == null)
                _housesRoot = CreateChildRoot("Houses");
            if (_vehiclesRoot == null)
                _vehiclesRoot = CreateChildRoot("Vehicles");
            if (_tileSprite == null)
                _tileSprite = CreateSquareSprite("MapWorldViewTile");
            if (_vehicleSprite == null)
                _vehicleSprite = CreateCircleSprite("MapWorldViewVehicle");
            if (_lineMaterial == null)
                _lineMaterial = CreateLineMaterial();
        }

        Transform CreateChildRoot(string name)
        {
            var existing = transform.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void RebuildStaticGeometry()
        {
            ClearChildren(_gridRoot);
            ClearChildren(_roadsRoot);
            ClearChildren(_housesRoot);
            RebuildGrid();
            RebuildBlockedCells();
            RebuildRoads();
            RebuildHouses();
        }

        void RebuildGrid()
        {
            var bounds = ResolveDistrictBounds();
            var width = bounds.size.x;
            var depth = bounds.size.y;
            if (width <= 0 || depth <= 0)
                return;

            var centerX = bounds.xMin + width * 0.5f - 0.5f;
            var centerZ = bounds.yMin + depth * 0.5f - 0.5f;

            var ground = CreateTileObject("Ground", _gridRoot, _groundColor, width, depth, -2);
            ground.transform.position = new Vector3(centerX, -0.02f, centerZ);

            var lineMinX = bounds.xMin - 0.5f;
            var lineMaxX = bounds.xMin + width - 0.5f;
            var lineMinZ = bounds.yMin - 0.5f;
            var lineMaxZ = bounds.yMin + depth - 0.5f;

            for (var x = bounds.xMin; x <= bounds.xMin + width; x++)
            {
                var lineX = x - 0.5f;
                CreateGridLine(
                    $"GridX_{x}",
                    new Vector3(lineX, 0.01f, lineMinZ),
                    new Vector3(lineX, 0.01f, lineMaxZ));
            }

            for (var z = bounds.yMin; z <= bounds.yMin + depth; z++)
            {
                var lineZ = z - 0.5f;
                CreateGridLine(
                    $"GridZ_{z}",
                    new Vector3(lineMinX, 0.01f, lineZ),
                    new Vector3(lineMaxX, 0.01f, lineZ));
            }
        }

        void RebuildBlockedCells()
        {
            var blocked = _skeleton?.blockedCells;
            if (blocked == null || blocked.Length == 0)
                return;

            var tileScale = MapGridSpec.TileScale(MapGridSpec.RoadFill);
            for (var i = 0; i < blocked.Length; i++)
            {
                var cell = blocked[i];
                var tile = CreateTileObject($"Blocked_{cell.x}_{cell.y}", _gridRoot, _blockedColor, tileScale, tileScale, 0);
                tile.transform.position = Lift(MapGridSpec.CellCenter(cell), 0.02f);
            }
        }

        void RebuildRoads()
        {
            var graph = _session?.Graph;
            if (graph == null || graph.NodeCount == 0)
                return;

            _roadCells.Clear();
            graph.ForEachUndirectedEdge((from, to) =>
                MapGridSpec.CollectLineCells(graph.GetNodePosition(from), graph.GetNodePosition(to), _roadCells));

            for (var i = 0; i < graph.NodeCount; i++)
                _roadCells.Add(MapGridSpec.WorldToCell(graph.GetNodePosition(i)));

            var usePrefabs = _roadStraightPrefab != null || _roadCornerPrefab != null ||
                             _roadTJunctionPrefab != null || _roadCrossPrefab != null;
            foreach (var cell in _roadCells)
            {
                if (usePrefabs && TrySpawnRoadPrefab(cell))
                    continue;

                var tileScale = MapGridSpec.TileScale(MapGridSpec.RoadFill);
                var tile = CreateTileObject($"Road_{cell.x}_{cell.y}", _roadsRoot, _roadColor, tileScale, tileScale, 1);
                tile.transform.position = Lift(MapGridSpec.CellCenter(cell), _roadHeight);
            }
        }

        bool TrySpawnRoadPrefab(Vector2Int cell)
        {
            var placement = RoadTileResolver.Resolve(cell, _roadCells);
            var prefab = ResolveRoadPrefab(placement.Kind);
            if (prefab == null)
                return false;

            var instance = Instantiate(prefab, _roadsRoot);
            instance.name = $"Road_{cell.x}_{cell.y}";
            var center = MapGridSpec.CellCenter(cell);
            instance.transform.SetPositionAndRotation(
                new Vector3(center.x, _roadHeight, center.z),
                Quaternion.Euler(90f, placement.YRotation, 0f));
            return true;
        }

        void RebuildHouses()
        {
            var lots = _skeleton?.houseLots;
            if (lots == null || lots.Length == 0)
                return;

            for (var i = 0; i < lots.Length; i++)
            {
                var lot = lots[i];
                if (lot == null || !MapGridSpec.IsValidHouseFootprint(lot.footprint))
                    continue;

                var scale = MapGridSpec.FootprintScaleXY(lot.footprint, MapGridSpec.HouseFill);
                var center = MapGridSpec.FootprintCenter(lot.origin, lot.footprint);
                var house = CreateTileObject($"House_{i}", _housesRoot, _houseColor, scale.x, scale.y, 2);
                house.transform.position = Lift(center, 0.045f);
            }
        }

        GameObject ResolveRoadPrefab(RoadTileKind kind) =>
            kind switch
            {
                RoadTileKind.Straight => _roadStraightPrefab,
                RoadTileKind.Corner => _roadCornerPrefab,
                RoadTileKind.TJunction => _roadTJunctionPrefab ?? _roadCrossPrefab ?? _roadCornerPrefab ?? _roadStraightPrefab,
                RoadTileKind.Cross => _roadCrossPrefab ?? _roadTJunctionPrefab ?? _roadCornerPrefab ?? _roadStraightPrefab,
                _ => _roadStraightPrefab
            };

        void CreateGridLine(string name, Vector3 a, Vector3 b)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_gridRoot, false);
            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line, _gridColor, _gridLineWidth, sortingOrder: -1);
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        BoundsInt ResolveDistrictBounds()
        {
            if (_skeleton != null)
            {
                var bounds = _skeleton.blockedGridBounds;
                if (bounds.size.x > 0 && bounds.size.y > 0)
                    return bounds;
            }

            return new BoundsInt(new Vector3Int(-10, -10, 0), new Vector3Int(30, 30, 1));
        }

        void RefreshHubs()
        {
            _seenHubIds.Clear();
            var slots = _skeleton?.hubSlotPositions;
            if (slots == null)
                return;

            var hubs = _session.Hubs;
            var slotFootprint = MapGridSpec.HubFootprint;
            var slotScale = MapGridSpec.FootprintScale(slotFootprint, MapGridSpec.HubSlotFill);

            for (var slotId = 0; slotId < slots.Length; slotId++)
            {
                var markerId = slotId + 100000;
                _seenHubIds.Add(markerId);

                if (!_hubVisuals.TryGetValue(markerId, out var slotVisual))
                {
                    slotVisual = CreateTileObject($"HubSlot_{slotId}", _hubsRoot, _emptySlotColor, slotScale, slotScale, 3)
                        .GetComponent<SpriteRenderer>();
                    _hubVisuals[markerId] = slotVisual;
                }

                slotVisual.transform.position = Lift(slots[slotId], 0.05f);
                var unlocked = hubs.IsSlotUnlocked(slotId);
                var occupied = hubs.IsSlotOccupied(slotId);
                slotVisual.enabled = unlocked && !occupied;
                slotVisual.color = _emptySlotColor;
                slotVisual.transform.localScale = new Vector3(slotScale, slotScale, 1f);
            }

            foreach (var hub in hubs.GetHubs())
            {
                _seenHubIds.Add(hub.Id);
                var footprint = hub.Def != null ? hub.Def.footprint : MapGridSpec.HubFootprint;
                var hubScale = MapGridSpec.FootprintScale(footprint, MapGridSpec.HubFill);

                if (!_hubVisuals.TryGetValue(hub.Id, out var visual))
                {
                    visual = CreateTileObject($"Hub_{hub.Id}", _hubsRoot, ResolveHubColor(hub), hubScale, hubScale, 4)
                        .GetComponent<SpriteRenderer>();
                    _hubVisuals[hub.Id] = visual;
                }

                if (hub.SlotId >= 0 && hub.SlotId < slots.Length)
                    visual.transform.position = Lift(slots[hub.SlotId], 0.06f);

                visual.enabled = true;
                visual.color = ResolveHubColor(hub);
                visual.transform.localScale = new Vector3(hubScale, hubScale, 1f);
            }

            RemoveMissing(_hubVisuals, _seenHubIds);
        }

        void RefreshVehicles()
        {
            _seenVehicleIds.Clear();

            foreach (var vehicle in _session.Fleet.GetAllVehicles())
            {
                _seenVehicleIds.Add(vehicle.Id);
                var scale = ResolveVehicleScale(vehicle);

                if (!_vehicleVisuals.TryGetValue(vehicle.Id, out var visual))
                {
                    visual = CreateVehicleObject(
                            $"Vehicle_{vehicle.Id}",
                            _vehiclesRoot,
                            ResolveVehicleColor(vehicle),
                            scale,
                            5)
                        .GetComponent<SpriteRenderer>();
                    _vehicleVisuals[vehicle.Id] = visual;
                }

                visual.transform.position = Lift(GetVehicleWorldPosition(vehicle), 0.08f);
                visual.color = ResolveVehicleColor(vehicle);
                visual.transform.localScale = Vector3.one * scale;
                visual.enabled = vehicle.State != VehicleState.Offline;
            }

            RemoveMissing(_vehicleVisuals, _seenVehicleIds);
        }

        static Vector3 GetVehicleWorldPosition(VehicleInstance vehicle)
        {
            if (vehicle.State == VehicleState.EnRoute && vehicle.PathAgent.IsActive)
                return vehicle.PathAgent.Position;

            return vehicle.Position;
        }

        static Vector3 Lift(Vector3 position, float height) =>
            new(position.x, height, position.z);

        Color ResolveHubColor(HubInstance hub)
        {
            if (_session.ModuleDefLookup != null &&
                _session.ModuleDefLookup.TryGetValue(hub.Def.module, out var moduleDef))
            {
                return moduleDef.color;
            }

            return Color.white;
        }

        static Color ResolveVehicleColor(VehicleInstance vehicle)
        {
            var color = vehicle.Def != null ? vehicle.Def.color : Color.white;
            return vehicle.State switch
            {
                VehicleState.EnRoute => color,
                VehicleState.Cooldown => Color.Lerp(color, Color.gray, 0.35f),
                VehicleState.Offline => Color.gray,
                _ => color
            };
        }

        float ResolveVehicleScale(VehicleInstance vehicle)
        {
            var baseScale = MapGridSpec.FootprintScale(MapGridSpec.VehicleFootprint, MapGridSpec.VehicleSpriteFill);
            if (vehicle.Def == null)
                return baseScale;

            return vehicle.Def.type switch
            {
                VehicleType.Bicycle => baseScale * 0.75f,
                VehicleType.Motorbike => baseScale * 0.9f,
                VehicleType.FourSeater => baseScale,
                VehicleType.SixSeater => baseScale * 1.15f,
                _ => baseScale
            };
        }

        GameObject CreateTileObject(
            string name,
            Transform parent,
            Color color,
            float scaleX,
            float scaleZ,
            int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = FlatOnXz;
            go.transform.localScale = new Vector3(scaleX, scaleZ, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _tileSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        GameObject CreateVehicleObject(string name, Transform parent, Color color, float scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = FlatOnXz;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _vehicleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        void ConfigureLine(LineRenderer line, Color color, float width, int sortingOrder)
        {
            line.material = _lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.useWorldSpace = true;
            line.sortingOrder = sortingOrder;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        static Material CreateLineMaterial()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        static Sprite CreateSquareSprite(string textureName)
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = textureName
            };

            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite CreateCircleSprite(string textureName)
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = textureName
            };

            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.45f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius * radius
                        ? Color.white
                        : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static void ClearChildren(Transform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        void RemoveMissing(Dictionary<int, SpriteRenderer> visuals, HashSet<int> seen)
        {
            _pruneBuffer.Clear();
            foreach (var pair in visuals)
            {
                if (!seen.Contains(pair.Key))
                    _pruneBuffer.Add(pair.Key);
            }

            for (var i = 0; i < _pruneBuffer.Count; i++)
            {
                var id = _pruneBuffer[i];
                if (visuals.TryGetValue(id, out var renderer) && renderer != null)
                    Destroy(renderer.gameObject);
                visuals.Remove(id);
            }
        }
    }
}
