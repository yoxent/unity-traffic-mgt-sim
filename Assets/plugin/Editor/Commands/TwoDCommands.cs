using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor.U2D;
using UnityEngine.U2D;

namespace UnityMcpPro
{
    public class TwoDCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_sprite_renderer", CreateSpriteRenderer);
            router.Register("create_tilemap", CreateTilemap);
            router.Register("set_tilemap_tile", SetTilemapTile);
            router.Register("create_sprite_atlas", CreateSpriteAtlas);
            router.Register("add_2d_collider", Add2DCollider);
            router.Register("setup_2d_physics", Setup2DPhysics);
        }

        private static object CreateSpriteRenderer(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_sprite_renderer");

            string spritePath = GetStringParam(p, "sprite_path");
            if (string.IsNullOrEmpty(spritePath))
                throw new ArgumentException("sprite_path is required");

            string name = GetStringParam(p, "name");
            string posStr = GetStringParam(p, "position");
            string sortingLayer = GetStringParam(p, "sorting_layer");
            int orderInLayer = GetIntParam(p, "order_in_layer", 0);
            string colorStr = GetStringParam(p, "color");

            // Load the sprite from asset path
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                // If the asset is a Texture2D, try to get the sprite sub-asset
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
                if (texture != null)
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                    foreach (var asset in allAssets)
                    {
                        if (asset is Sprite s)
                        {
                            sprite = s;
                            break;
                        }
                    }

                    if (sprite == null)
                        throw new ArgumentException(
                            $"Texture at '{spritePath}' has no Sprite sub-asset. Set Texture Type to 'Sprite (2D and UI)' in the import settings.");
                }
                else
                {
                    throw new ArgumentException($"No Sprite or Texture2D found at '{spritePath}'");
                }
            }

            // Derive name from sprite if not provided
            if (string.IsNullOrEmpty(name))
                name = sprite.name;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Sprite Renderer");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            if (!string.IsNullOrEmpty(posStr))
                go.transform.position = TypeParser.ParseVector3(posStr);

            if (!string.IsNullOrEmpty(sortingLayer))
                sr.sortingLayerName = sortingLayer;

            sr.sortingOrder = orderInLayer;

            if (!string.IsNullOrEmpty(colorStr))
                sr.color = TypeParser.ParseColor(colorStr);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "sprite", sprite.name },
                { "sortingLayer", sr.sortingLayerName },
                { "orderInLayer", sr.sortingOrder }
            };
        }

        private static object CreateTilemap(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_tilemap");

            string name = GetStringParam(p, "name", "Tilemap");
            string cellSizeStr = GetStringParam(p, "cell_size", "1,1,0");
            int sortingOrder = GetIntParam(p, "sorting_order", 0);
            string tileAnchorStr = GetStringParam(p, "tile_anchor");

            // Create Grid parent
            var gridGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gridGo, "Create Tilemap");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = TypeParser.ParseVector3(cellSizeStr);

            // Create Tilemap child
            var tilemapGo = new GameObject("Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform, false);
            Undo.RegisterCreatedObjectUndo(tilemapGo, "Create Tilemap Child");

            var tilemap = tilemapGo.AddComponent<Tilemap>();
            var renderer = tilemapGo.AddComponent<TilemapRenderer>();
            tilemapGo.AddComponent<TilemapCollider2D>();

            renderer.sortingOrder = sortingOrder;

            if (!string.IsNullOrEmpty(tileAnchorStr))
                tilemap.tileAnchor = TypeParser.ParseVector3(tileAnchorStr);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gridObject", gridGo.name },
                { "gridPath", GetGameObjectPath(gridGo) },
                { "tilemapPath", GetGameObjectPath(tilemapGo) },
                { "cellSize", cellSizeStr },
                { "sortingOrder", sortingOrder }
            };
        }

        private static object SetTilemapTile(Dictionary<string, object> p)
        {
            ThrowIfPlaying("set_tilemap_tile");

            string targetPath = GetStringParam(p, "target");
            string tilePath = GetStringParam(p, "tile_path");
            string posStr = GetStringParam(p, "position");
            string mode = GetStringParam(p, "mode", "place");
            string endPosStr = GetStringParam(p, "end_position");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(posStr))
                throw new ArgumentException("position is required");

            var go = FindGameObject(targetPath);
            var tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null)
                throw new ArgumentException($"No Tilemap component found on '{targetPath}'");

            RecordUndo(tilemap, "Set Tilemap Tile");

            // Parse position (x,y)
            var posParts = posStr.Split(',');
            if (posParts.Length < 2)
                throw new ArgumentException("position must be 'x,y' integer coordinates");
            var pos = new Vector3Int(int.Parse(posParts[0].Trim()), int.Parse(posParts[1].Trim()), 0);

            switch (mode.ToLower())
            {
                case "place":
                {
                    if (string.IsNullOrEmpty(tilePath))
                        throw new ArgumentException("tile_path is required for place mode");

                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                    if (tile == null)
                        throw new ArgumentException($"No TileBase asset found at '{tilePath}'");

                    tilemap.SetTile(pos, tile);

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "mode", "place" },
                        { "position", posStr },
                        { "tile", tile.name }
                    };
                }

                case "remove":
                {
                    tilemap.SetTile(pos, null);

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "mode", "remove" },
                        { "position", posStr }
                    };
                }

                case "box":
                {
                    if (string.IsNullOrEmpty(tilePath))
                        throw new ArgumentException("tile_path is required for box mode");
                    if (string.IsNullOrEmpty(endPosStr))
                        throw new ArgumentException("end_position is required for box mode");

                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
                    if (tile == null)
                        throw new ArgumentException($"No TileBase asset found at '{tilePath}'");

                    var endParts = endPosStr.Split(',');
                    if (endParts.Length < 2)
                        throw new ArgumentException("end_position must be 'x,y' integer coordinates");
                    var endPos = new Vector3Int(int.Parse(endParts[0].Trim()), int.Parse(endParts[1].Trim()), 0);

                    // Fill the rectangular region
                    int minX = Mathf.Min(pos.x, endPos.x);
                    int maxX = Mathf.Max(pos.x, endPos.x);
                    int minY = Mathf.Min(pos.y, endPos.y);
                    int maxY = Mathf.Max(pos.y, endPos.y);
                    int count = 0;

                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                            count++;
                        }
                    }

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "mode", "box" },
                        { "from", posStr },
                        { "to", endPosStr },
                        { "tilesPlaced", count },
                        { "tile", tile.name }
                    };
                }

                default:
                    throw new ArgumentException($"Unknown mode: {mode}. Use 'place', 'remove', or 'box'.");
            }
        }

        private static object CreateSpriteAtlas(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_sprite_atlas");

            string path = GetStringParam(p, "path");
            string[] sources = GetStringListParam(p, "sources");
            bool includeInBuild = GetBoolParam(p, "include_in_build", true);

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("sources array is required and must not be empty");

            if (!path.EndsWith(".spriteatlas"))
                path += ".spriteatlas";

            // Create the SpriteAtlas asset
            var atlas = new SpriteAtlas();

            // Configure packing settings
            var packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                enableTightPacking = true,
                padding = 4
            };
            atlas.SetPackingSettings(packingSettings);

            // Configure texture settings
            var textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                filterMode = FilterMode.Bilinear,
                sRGB = true
            };
            atlas.SetTextureSettings(textureSettings);

            atlas.SetIncludeInBuild(includeInBuild);

            // Add source objects (folders or sprites)
            var packables = new List<UnityEngine.Object>();
            foreach (var source in sources)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(source);
                if (obj != null)
                    packables.Add(obj);
                else
                    Debug.LogWarning($"[MCP] SpriteAtlas source not found: {source}");
            }

            if (packables.Count > 0)
                atlas.Add(packables.ToArray());

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] folders = dir.Replace("\\", "/").Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string nextPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    currentPath = nextPath;
                }
            }

            AssetDatabase.CreateAsset(atlas, path);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "sourcesAdded", packables.Count },
                { "includeInBuild", includeInBuild }
            };
        }

        private static object Add2DCollider(Dictionary<string, object> p)
        {
            string targetPath = GetStringParam(p, "target");
            string type = GetStringParam(p, "type");
            bool isTrigger = GetBoolParam(p, "is_trigger");
            string sizeStr = GetStringParam(p, "size");
            float radius = GetFloatParam(p, "radius", -1f);
            bool usedByComposite = GetBoolParam(p, "used_by_composite");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("type is required");

            var go = FindGameObject(targetPath);
            Collider2D collider;

            switch (type.ToLower())
            {
                case "box2d":
                    var box = Undo.AddComponent<BoxCollider2D>(go);
                    if (!string.IsNullOrEmpty(sizeStr))
                    {
                        var parts = sizeStr.Split(',');
                        if (parts.Length >= 2)
                            box.size = new Vector2(
                                float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture));
                    }
                    collider = box;
                    break;

                case "circle2d":
                    var circle = Undo.AddComponent<CircleCollider2D>(go);
                    if (radius >= 0) circle.radius = radius;
                    collider = circle;
                    break;

                case "polygon2d":
                    collider = Undo.AddComponent<PolygonCollider2D>(go);
                    break;

                case "edge2d":
                    collider = Undo.AddComponent<EdgeCollider2D>(go);
                    break;

                case "capsule2d":
                    var capsule = Undo.AddComponent<CapsuleCollider2D>(go);
                    if (!string.IsNullOrEmpty(sizeStr))
                    {
                        var parts = sizeStr.Split(',');
                        if (parts.Length >= 2)
                            capsule.size = new Vector2(
                                float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture));
                    }
                    collider = capsule;
                    break;

                case "composite2d":
                    collider = Undo.AddComponent<CompositeCollider2D>(go);
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown 2D collider type: {type}. Use: Box2D, Circle2D, Polygon2D, Edge2D, Capsule2D, Composite2D");
            }

            collider.isTrigger = isTrigger;

            if (usedByComposite)
            {
#if UNITY_6000_0_OR_NEWER
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
                collider.usedByComposite = true;
#endif
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "colliderType", collider.GetType().Name },
                { "isTrigger", isTrigger },
#if UNITY_6000_0_OR_NEWER
                { "usedByComposite", collider.compositeOperation != Collider2D.CompositeOperation.None }
#else
                { "usedByComposite", collider.usedByComposite }
#endif
            };
        }

        private static object Setup2DPhysics(Dictionary<string, object> p)
        {
            string targetPath = GetStringParam(p, "target");
            string bodyTypeStr = GetStringParam(p, "body_type", "Dynamic");
            float gravityScale = GetFloatParam(p, "gravity_scale", 1f);
            float mass = GetFloatParam(p, "mass", 1f);
            float linearDrag = GetFloatParam(p, "linear_drag", 0f);
            float angularDrag = GetFloatParam(p, "angular_drag", 0.05f);
            bool freezeRotation = GetBoolParam(p, "freeze_rotation");
            string collisionDetectionStr = GetStringParam(p, "collision_detection", "Discrete");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody2D>(go);
            else
                RecordUndo(rb, "Setup 2D Physics");

            // Set body type
            switch (bodyTypeStr.ToLower())
            {
                case "dynamic":
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    break;
                case "kinematic":
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    break;
                case "static":
                    rb.bodyType = RigidbodyType2D.Static;
                    break;
                default:
                    throw new ArgumentException($"Unknown body type: {bodyTypeStr}. Use: Dynamic, Kinematic, Static");
            }

            rb.gravityScale = gravityScale;
            rb.mass = mass;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linearDrag;
            rb.angularDamping = angularDrag;
#else
            rb.drag = linearDrag;
            rb.angularDrag = angularDrag;
#endif

            if (freezeRotation)
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            else
                rb.constraints = RigidbodyConstraints2D.None;

            // Collision detection
            switch (collisionDetectionStr.ToLower())
            {
                case "discrete":
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                    break;
                case "continuous":
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown collision detection mode: {collisionDetectionStr}. Use: Discrete, Continuous");
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "bodyType", bodyTypeStr },
                { "mass", rb.mass },
                { "gravityScale", rb.gravityScale },
                { "freezeRotation", freezeRotation },
                { "collisionDetection", collisionDetectionStr }
            };
        }
    }
}
