using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMcpPro
{
    public class ParticleCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_particle_system", CreateParticleSystem);
            router.Register("set_particle_module", SetParticleModule);
            router.Register("get_particle_info", GetParticleInfo);
            router.Register("add_particle_sub_emitter", AddParticleSubEmitter);
            router.Register("set_particle_renderer", SetParticleRenderer);
        }

        private static object CreateParticleSystem(Dictionary<string, object> p)
        {
            string name = GetStringParam(p, "name", "Particle System");
            string preset = GetStringParam(p, "preset");
            string posStr = GetStringParam(p, "position");
            string parentPath = GetStringParam(p, "parent");

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            Undo.RegisterCreatedObjectUndo(go, "MCP: Create Particle System");

            if (!string.IsNullOrEmpty(posStr))
                go.transform.position = TypeParser.ParseVector3(posStr);

            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = FindGameObject(parentPath);
                go.transform.SetParent(parent.transform, false);
            }

            if (!string.IsNullOrEmpty(preset))
                ApplyPreset(ps, preset);

            // Auto-assign URP particle material if available
            ApplyUrpParticleMaterial(go.GetComponent<ParticleSystemRenderer>());

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "preset", preset ?? "default" },
                { "path", GetGameObjectPath(go) }
            };
        }

        private static object SetParticleModule(Dictionary<string, object> p)
        {
            string goPath = GetStringParam(p, "game_object_path");
            string moduleName = GetStringParam(p, "module");

            if (string.IsNullOrEmpty(goPath))
                throw new ArgumentException("game_object_path is required");
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentException("module is required");

            var go = FindGameObject(goPath);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                throw new ArgumentException($"No ParticleSystem on {go.name}");

            RecordUndo(ps, "Set Particle Module");
            var props = GetDictParam(p, "properties") ?? new Dictionary<string, object>();

            switch (moduleName.ToLower())
            {
                case "main":
                    var main = ps.main;
                    if (props.ContainsKey("duration"))
                        main.duration = GetFloatParam(props, "duration", 5f);
                    if (props.ContainsKey("loop"))
                        main.loop = GetBoolParam(props, "loop", true);
                    if (props.ContainsKey("startLifetime"))
                        main.startLifetime = GetFloatParam(props, "startLifetime", 5f);
                    if (props.ContainsKey("startSpeed"))
                        main.startSpeed = GetFloatParam(props, "startSpeed", 5f);
                    if (props.ContainsKey("startSize"))
                        main.startSize = GetFloatParam(props, "startSize", 1f);
                    if (props.ContainsKey("maxParticles"))
                        main.maxParticles = GetIntParam(props, "maxParticles", 1000);
                    if (props.ContainsKey("gravityModifier"))
                        main.gravityModifier = GetFloatParam(props, "gravityModifier");
                    if (props.ContainsKey("startColor"))
                        main.startColor = TypeParser.ParseColor(GetStringParam(props, "startColor"));
                    break;
                case "emission":
                    var emission = ps.emission;
                    if (props.ContainsKey("enabled"))
                        emission.enabled = GetBoolParam(props, "enabled", true);
                    if (props.ContainsKey("rateOverTime"))
                        emission.rateOverTime = GetFloatParam(props, "rateOverTime", 10f);
                    break;
                case "shape":
                    var shape = ps.shape;
                    if (props.ContainsKey("enabled"))
                        shape.enabled = GetBoolParam(props, "enabled", true);
                    string shapeType = GetStringParam(props, "shapeType");
                    if (!string.IsNullOrEmpty(shapeType))
                    {
                        if (Enum.TryParse<ParticleSystemShapeType>(shapeType, true, out var st))
                            shape.shapeType = st;
                    }
                    if (props.ContainsKey("radius"))
                        shape.radius = GetFloatParam(props, "radius", 1f);
                    if (props.ContainsKey("angle"))
                        shape.angle = GetFloatParam(props, "angle", 25f);
                    string scaleStr = GetStringParam(props, "scale");
                    if (!string.IsNullOrEmpty(scaleStr))
                        shape.scale = TypeParser.ParseVector3(scaleStr);
                    break;
                case "coloroverlifetime":
                    var col = ps.colorOverLifetime;
                    col.enabled = GetBoolParam(props, "enabled", true);
                    break;
                case "sizeoverlifetime":
                    var sol = ps.sizeOverLifetime;
                    sol.enabled = GetBoolParam(props, "enabled", true);
                    break;
                case "velocityoverlifetime":
                    var vol = ps.velocityOverLifetime;
                    vol.enabled = GetBoolParam(props, "enabled", true);
                    break;
                default:
                    throw new ArgumentException($"Unknown module: {moduleName}");
            }

            EditorUtility.SetDirty(ps);
            return Success($"Updated {moduleName} module on {go.name}");
        }

        private static object GetParticleInfo(Dictionary<string, object> p)
        {
            string goPath = GetStringParam(p, "game_object_path");
            if (string.IsNullOrEmpty(goPath))
                throw new ArgumentException("game_object_path is required");

            var go = FindGameObject(goPath);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                throw new ArgumentException($"No ParticleSystem on {go.name}");

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            return new Dictionary<string, object>
            {
                { "name", go.name },
                { "isPlaying", ps.isPlaying },
                { "particleCount", ps.particleCount },
                { "main", new Dictionary<string, object>
                    {
                        { "duration", main.duration },
                        { "loop", main.loop },
                        { "maxParticles", main.maxParticles },
                        { "gravityModifier", main.gravityModifier.constant }
                    }
                },
                { "emission", new Dictionary<string, object>
                    {
                        { "enabled", emission.enabled },
                        { "rateOverTime", emission.rateOverTime.constant }
                    }
                },
                { "shape", new Dictionary<string, object>
                    {
                        { "enabled", shape.enabled },
                        { "shapeType", shape.shapeType.ToString() },
                        { "radius", shape.radius }
                    }
                }
            };
        }

        private static object AddParticleSubEmitter(Dictionary<string, object> p)
        {
            string goPath = GetStringParam(p, "game_object_path");
            string subEmitterPath = GetStringParam(p, "sub_emitter_path");
            string typeStr = GetStringParam(p, "type", "Birth");

            if (string.IsNullOrEmpty(goPath))
                throw new ArgumentException("game_object_path is required");
            if (string.IsNullOrEmpty(subEmitterPath))
                throw new ArgumentException("sub_emitter_path is required");

            var go = FindGameObject(goPath);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
                throw new ArgumentException($"No ParticleSystem on {go.name}");

            var subGo = FindGameObject(subEmitterPath);
            var subPs = subGo.GetComponent<ParticleSystem>();
            if (subPs == null)
                throw new ArgumentException($"No ParticleSystem on {subGo.name}");

            RecordUndo(ps, "Add Sub Emitter");
            var subEmitters = ps.subEmitters;
            subEmitters.enabled = true;

            ParticleSystemSubEmitterType subType = ParticleSystemSubEmitterType.Birth;
            if (Enum.TryParse<ParticleSystemSubEmitterType>(typeStr, true, out var parsed))
                subType = parsed;

            subEmitters.AddSubEmitter(subPs, subType, ParticleSystemSubEmitterProperties.InheritNothing);
            return Success($"Added sub-emitter {subGo.name} to {go.name} ({subType})");
        }

        private static object SetParticleRenderer(Dictionary<string, object> p)
        {
            string goPath = GetStringParam(p, "game_object_path");
            string modeStr = GetStringParam(p, "render_mode");
            string materialPath = GetStringParam(p, "material_path");

            if (string.IsNullOrEmpty(goPath))
                throw new ArgumentException("game_object_path is required");

            var go = FindGameObject(goPath);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                throw new ArgumentException($"No ParticleSystemRenderer on {go.name}");

            RecordUndo(renderer, "Set Particle Renderer");

            if (!string.IsNullOrEmpty(modeStr))
            {
                if (Enum.TryParse<ParticleSystemRenderMode>(modeStr, true, out var mode))
                    renderer.renderMode = mode;
            }

            if (!string.IsNullOrEmpty(materialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat != null)
                    renderer.sharedMaterial = mat;
            }

            string meshPath = GetStringParam(p, "mesh_path");
            if (!string.IsNullOrEmpty(meshPath))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh != null)
                    renderer.mesh = mesh;
            }

            EditorUtility.SetDirty(renderer);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "renderMode", renderer.renderMode.ToString() }
            };
        }

        private static void ApplyUrpParticleMaterial(ParticleSystemRenderer renderer)
        {
            if (renderer == null) return;

            // Check if project uses URP
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return; // Built-in RP — default material is fine

            // Try URP particle shaders in preference order
            string[] shaderNames = new[]
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Universal Render Pipeline/Particles/Lit",
                "Particles/Standard Unlit"
            };

            Shader shader = null;
            foreach (var name in shaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null) break;
            }

            if (shader != null)
            {
                var mat = new Material(shader);
                mat.name = "MCP_ParticleMat";
                renderer.sharedMaterial = mat;
            }
        }

        private static void ApplyPreset(ParticleSystem ps, string preset)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            switch (preset.ToLower())
            {
                case "fire":
                    main.startLifetime = 1.5f;
                    main.startSpeed = 2f;
                    main.startSize = 0.5f;
                    main.startColor = new Color(1f, 0.5f, 0.1f, 1f);
                    main.gravityModifier = -0.2f;
                    main.maxParticles = 200;
                    emission.rateOverTime = 50;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15f;
                    shape.radius = 0.3f;
                    break;
                case "smoke":
                    main.startLifetime = 4f;
                    main.startSpeed = 0.5f;
                    main.startSize = 1f;
                    main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                    main.gravityModifier = -0.05f;
                    main.maxParticles = 100;
                    emission.rateOverTime = 15;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25f;
                    break;
                case "sparkle":
                    main.startLifetime = 0.5f;
                    main.startSpeed = 5f;
                    main.startSize = 0.1f;
                    main.startColor = new Color(1f, 0.95f, 0.6f, 1f);
                    main.gravityModifier = 0.5f;
                    main.maxParticles = 300;
                    emission.rateOverTime = 100;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    break;
                case "rain":
                    main.startLifetime = 1.5f;
                    main.startSpeed = 15f;
                    main.startSize = 0.05f;
                    main.startColor = new Color(0.7f, 0.8f, 1f, 0.6f);
                    main.gravityModifier = 1f;
                    main.maxParticles = 1000;
                    emission.rateOverTime = 300;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(20, 0, 20);
                    shape.position = new Vector3(0, 10, 0);
                    break;
                case "explosion":
                    main.startLifetime = 1f;
                    main.startSpeed = 10f;
                    main.startSize = 0.3f;
                    main.startColor = new Color(1f, 0.6f, 0.2f, 1f);
                    main.gravityModifier = 0.5f;
                    main.maxParticles = 500;
                    main.loop = false;
                    main.duration = 0.5f;
                    emission.rateOverTime = 0;
                    var burst = new ParticleSystem.Burst(0f, 100);
                    emission.SetBursts(new[] { burst });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    break;
            }
        }
    }
}
