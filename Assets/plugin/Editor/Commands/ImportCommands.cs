using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class ImportCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("get_import_settings", GetImportSettings);
            router.Register("set_texture_import", SetTextureImport);
            router.Register("set_model_import", SetModelImport);
            router.Register("set_audio_import", SetAudioImport);
            router.Register("apply_import_preset", ApplyImportPreset);
        }

        private static object GetImportSettings(Dictionary<string, object> p)
        {
            string path = GetStringParam(p, "path");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new ArgumentException($"No asset importer found at: {path}");

            var result = new Dictionary<string, object>
            {
                { "path", path },
                { "importerType", importer.GetType().Name }
            };

            if (importer is TextureImporter texImporter)
            {
                result["textureType"] = texImporter.textureType.ToString();
                result["maxTextureSize"] = texImporter.maxTextureSize;
                result["textureCompression"] = texImporter.textureCompression.ToString();
                result["mipmapEnabled"] = texImporter.mipmapEnabled;
                result["isReadable"] = texImporter.isReadable;
                result["sRGBTexture"] = texImporter.sRGBTexture;
                result["filterMode"] = texImporter.filterMode.ToString();
                result["wrapMode"] = texImporter.wrapMode.ToString();
                result["spriteImportMode"] = texImporter.spriteImportMode.ToString();
                result["spritePixelsPerUnit"] = texImporter.spritePixelsPerUnit;
                result["alphaIsTransparency"] = texImporter.alphaIsTransparency;
                result["npotScale"] = texImporter.npotScale.ToString();
                result["anisoLevel"] = texImporter.anisoLevel;
            }
            else if (importer is ModelImporter modelImporter)
            {
                result["globalScale"] = modelImporter.globalScale;
                result["importMaterials"] = modelImporter.materialImportMode != ModelImporterMaterialImportMode.None;
                result["importAnimation"] = modelImporter.importAnimation;
                result["animationType"] = modelImporter.animationType.ToString();
                result["meshCompression"] = modelImporter.meshCompression.ToString();
                result["isReadable"] = modelImporter.isReadable;
                result["addCollider"] = modelImporter.addCollider;
                result["importNormals"] = modelImporter.importNormals.ToString();
                result["importBlendShapes"] = modelImporter.importBlendShapes;
                result["importVisibility"] = modelImporter.importVisibility;
                result["importCameras"] = modelImporter.importCameras;
                result["importLights"] = modelImporter.importLights;
                result["generateSecondaryUV"] = modelImporter.generateSecondaryUV;
            }
            else if (importer is AudioImporter audioImporter)
            {
                result["forceToMono"] = audioImporter.forceToMono;
                result["loadInBackground"] = audioImporter.loadInBackground;
                var defaultSettings = audioImporter.defaultSampleSettings;
                result["loadType"] = defaultSettings.loadType.ToString();
                result["compressionFormat"] = defaultSettings.compressionFormat.ToString();
                result["quality"] = defaultSettings.quality;
                result["sampleRateSetting"] = defaultSettings.sampleRateSetting.ToString();
            }

            return result;
        }

        private static object SetTextureImport(Dictionary<string, object> p)
        {
            string path = GetStringParam(p, "path");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new ArgumentException($"No TextureImporter found at: {path}");

            string textureType = GetStringParam(p, "texture_type");
            if (!string.IsNullOrEmpty(textureType))
            {
                if (Enum.TryParse<TextureImporterType>(textureType, true, out var tt))
                    importer.textureType = tt;
                else
                    throw new ArgumentException($"Invalid texture_type: {textureType}");
            }

            int maxSize = GetIntParam(p, "max_size", -1);
            if (maxSize > 0)
                importer.maxTextureSize = maxSize;

            string compression = GetStringParam(p, "compression");
            if (!string.IsNullOrEmpty(compression))
            {
                switch (compression.ToLower())
                {
                    case "none": importer.textureCompression = TextureImporterCompression.Uncompressed; break;
                    case "lowquality": importer.textureCompression = TextureImporterCompression.CompressedLQ; break;
                    case "normalquality": importer.textureCompression = TextureImporterCompression.Compressed; break;
                    case "highquality": importer.textureCompression = TextureImporterCompression.CompressedHQ; break;
                    default: throw new ArgumentException($"Invalid compression: {compression}");
                }
            }

            if (p.ContainsKey("generate_mipmaps"))
                importer.mipmapEnabled = GetBoolParam(p, "generate_mipmaps");

            if (p.ContainsKey("read_write"))
                importer.isReadable = GetBoolParam(p, "read_write");

            if (p.ContainsKey("srgb"))
                importer.sRGBTexture = GetBoolParam(p, "srgb");

            string filterMode = GetStringParam(p, "filter_mode");
            if (!string.IsNullOrEmpty(filterMode))
            {
                if (Enum.TryParse<FilterMode>(filterMode, true, out var fm))
                    importer.filterMode = fm;
                else
                    throw new ArgumentException($"Invalid filter_mode: {filterMode}");
            }

            string spriteMode = GetStringParam(p, "sprite_mode");
            if (!string.IsNullOrEmpty(spriteMode))
            {
                switch (spriteMode.ToLower())
                {
                    case "single": importer.spriteImportMode = SpriteImportMode.Single; break;
                    case "multiple": importer.spriteImportMode = SpriteImportMode.Multiple; break;
                    case "polygon": importer.spriteImportMode = SpriteImportMode.Polygon; break;
                    default: throw new ArgumentException($"Invalid sprite_mode: {spriteMode}");
                }
            }

            float ppu = GetFloatParam(p, "pixels_per_unit", -1f);
            if (ppu > 0)
                importer.spritePixelsPerUnit = ppu;

            importer.SaveAndReimport();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "message", $"Texture import settings updated and reimported: {path}" }
            };
        }

        private static object SetModelImport(Dictionary<string, object> p)
        {
            string path = GetStringParam(p, "path");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new ArgumentException($"No ModelImporter found at: {path}");

            float scaleFactor = GetFloatParam(p, "scale_factor", -1f);
            if (scaleFactor > 0)
                importer.globalScale = scaleFactor;

            if (p.ContainsKey("import_materials"))
                importer.materialImportMode = GetBoolParam(p, "import_materials")
                    ? ModelImporterMaterialImportMode.ImportViaMaterialDescription
                    : ModelImporterMaterialImportMode.None;

            if (p.ContainsKey("import_animation"))
                importer.importAnimation = GetBoolParam(p, "import_animation");

            string animType = GetStringParam(p, "animation_type");
            if (!string.IsNullOrEmpty(animType))
            {
                switch (animType.ToLower())
                {
                    case "none": importer.animationType = ModelImporterAnimationType.None; break;
                    case "legacy": importer.animationType = ModelImporterAnimationType.Legacy; break;
                    case "generic": importer.animationType = ModelImporterAnimationType.Generic; break;
                    case "humanoid": importer.animationType = ModelImporterAnimationType.Human; break;
                    default: throw new ArgumentException($"Invalid animation_type: {animType}");
                }
            }

            string meshComp = GetStringParam(p, "mesh_compression");
            if (!string.IsNullOrEmpty(meshComp))
            {
                if (Enum.TryParse<ModelImporterMeshCompression>(meshComp, true, out var mc))
                    importer.meshCompression = mc;
                else
                    throw new ArgumentException($"Invalid mesh_compression: {meshComp}");
            }

            if (p.ContainsKey("read_write"))
                importer.isReadable = GetBoolParam(p, "read_write");

            if (p.ContainsKey("generate_colliders"))
                importer.addCollider = GetBoolParam(p, "generate_colliders");

            string normals = GetStringParam(p, "normals");
            if (!string.IsNullOrEmpty(normals))
            {
                switch (normals.ToLower())
                {
                    case "import": importer.importNormals = ModelImporterNormals.Import; break;
                    case "calculate": importer.importNormals = ModelImporterNormals.Calculate; break;
                    case "none": importer.importNormals = ModelImporterNormals.None; break;
                    default: throw new ArgumentException($"Invalid normals: {normals}");
                }
            }

            if (p.ContainsKey("blend_shapes"))
                importer.importBlendShapes = GetBoolParam(p, "blend_shapes");

            importer.SaveAndReimport();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "message", $"Model import settings updated and reimported: {path}" }
            };
        }

        private static object SetAudioImport(Dictionary<string, object> p)
        {
            string path = GetStringParam(p, "path");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new ArgumentException($"No AudioImporter found at: {path}");

            if (p.ContainsKey("force_mono"))
                importer.forceToMono = GetBoolParam(p, "force_mono");

            var settings = importer.defaultSampleSettings;

            string loadType = GetStringParam(p, "load_type");
            if (!string.IsNullOrEmpty(loadType))
            {
                switch (loadType.ToLower())
                {
                    case "decompressonload": settings.loadType = AudioClipLoadType.DecompressOnLoad; break;
                    case "compressedinmemory": settings.loadType = AudioClipLoadType.CompressedInMemory; break;
                    case "streaming": settings.loadType = AudioClipLoadType.Streaming; break;
                    default: throw new ArgumentException($"Invalid load_type: {loadType}");
                }
            }

            string compFormat = GetStringParam(p, "compression_format");
            if (!string.IsNullOrEmpty(compFormat))
            {
                switch (compFormat.ToUpper())
                {
                    case "PCM": settings.compressionFormat = AudioCompressionFormat.PCM; break;
                    case "VORBIS": settings.compressionFormat = AudioCompressionFormat.Vorbis; break;
                    case "ADPCM": settings.compressionFormat = AudioCompressionFormat.ADPCM; break;
                    default: throw new ArgumentException($"Invalid compression_format: {compFormat}");
                }
            }

            float quality = GetFloatParam(p, "quality", -1f);
            if (quality >= 0f)
                settings.quality = Mathf.Clamp01(quality);

            string sampleRate = GetStringParam(p, "sample_rate");
            if (!string.IsNullOrEmpty(sampleRate))
            {
                switch (sampleRate.ToLower())
                {
                    case "preservesamplerate": settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; break;
                    case "optimizesamplerate": settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate; break;
                    case "overridesamplerate": settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; break;
                    default: throw new ArgumentException($"Invalid sample_rate: {sampleRate}");
                }
            }

            importer.defaultSampleSettings = settings;

            if (p.ContainsKey("normalize"))
            {
                // Normalize is set via SerializedObject
                var so = new SerializedObject(importer);
                var normProp = so.FindProperty("m_Normalize");
                if (normProp != null)
                {
                    normProp.boolValue = GetBoolParam(p, "normalize");
                    so.ApplyModifiedProperties();
                }
            }

            importer.SaveAndReimport();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "message", $"Audio import settings updated and reimported: {path}" }
            };
        }

        private static object ApplyImportPreset(Dictionary<string, object> p)
        {
            var paths = GetStringListParam(p, "paths");
            string preset = GetStringParam(p, "preset");
            string platform = GetStringParam(p, "platform");

            if (paths == null || paths.Length == 0)
                throw new ArgumentException("paths is required and must not be empty");
            if (string.IsNullOrEmpty(preset))
                throw new ArgumentException("preset is required");

            var results = new List<object>();
            int successCount = 0;
            int failCount = 0;

            foreach (var path in paths)
            {
                try
                {
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer == null)
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            { "path", path },
                            { "success", false },
                            { "error", "No importer found" }
                        });
                        failCount++;
                        continue;
                    }

                    ApplyPresetToImporter(importer, preset, platform);
                    importer.SaveAndReimport();

                    results.Add(new Dictionary<string, object>
                    {
                        { "path", path },
                        { "success", true }
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "path", path },
                        { "success", false },
                        { "error", ex.Message }
                    });
                    failCount++;
                }
            }

            return new Dictionary<string, object>
            {
                { "success", failCount == 0 },
                { "preset", preset },
                { "successCount", successCount },
                { "failCount", failCount },
                { "results", results }
            };
        }

        private static void ApplyPresetToImporter(AssetImporter importer, string preset, string platform)
        {
            switch (preset.ToLower())
            {
                case "mobile_texture":
                    if (importer is TextureImporter mobileTex)
                    {
                        mobileTex.maxTextureSize = 512;
                        mobileTex.textureCompression = TextureImporterCompression.Compressed;
                        mobileTex.mipmapEnabled = false;
                        mobileTex.isReadable = false;

                        if (!string.IsNullOrEmpty(platform))
                        {
                            var platformSettings = mobileTex.GetPlatformTextureSettings(platform);
                            platformSettings.overridden = true;
                            platformSettings.maxTextureSize = 512;
                            platformSettings.format = TextureImporterFormat.ASTC_6x6;
                            mobileTex.SetPlatformTextureSettings(platformSettings);
                        }
                    }
                    else
                        throw new ArgumentException($"Preset 'mobile_texture' requires a texture asset, got {importer.GetType().Name}");
                    break;

                case "hd_texture":
                    if (importer is TextureImporter hdTex)
                    {
                        hdTex.maxTextureSize = 4096;
                        hdTex.textureCompression = TextureImporterCompression.CompressedHQ;
                        hdTex.mipmapEnabled = true;
                        hdTex.isReadable = false;
                        hdTex.anisoLevel = 4;
                        hdTex.filterMode = FilterMode.Trilinear;

                        if (!string.IsNullOrEmpty(platform))
                        {
                            var platformSettings = hdTex.GetPlatformTextureSettings(platform);
                            platformSettings.overridden = true;
                            platformSettings.maxTextureSize = 4096;
                            platformSettings.format = TextureImporterFormat.BC7;
                            hdTex.SetPlatformTextureSettings(platformSettings);
                        }
                    }
                    else
                        throw new ArgumentException($"Preset 'hd_texture' requires a texture asset, got {importer.GetType().Name}");
                    break;

                case "web_audio":
                    if (importer is AudioImporter webAudio)
                    {
                        webAudio.forceToMono = false;
                        var webSettings = webAudio.defaultSampleSettings;
                        webSettings.loadType = AudioClipLoadType.CompressedInMemory;
                        webSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        webSettings.quality = 0.5f;
                        webAudio.defaultSampleSettings = webSettings;

                        if (!string.IsNullOrEmpty(platform) && platform.ToLower() == "webgl")
                        {
                            var platSettings = webAudio.GetOverrideSampleSettings("WebGL");
                            platSettings.loadType = AudioClipLoadType.CompressedInMemory;
                            platSettings.compressionFormat = AudioCompressionFormat.AAC;
                            platSettings.quality = 0.5f;
                            webAudio.SetOverrideSampleSettings("WebGL", platSettings);
                        }
                    }
                    else
                        throw new ArgumentException($"Preset 'web_audio' requires an audio asset, got {importer.GetType().Name}");
                    break;

                case "mobile_audio":
                    if (importer is AudioImporter mobileAudio)
                    {
                        mobileAudio.forceToMono = true;
                        var mobileSettings = mobileAudio.defaultSampleSettings;
                        mobileSettings.loadType = AudioClipLoadType.CompressedInMemory;
                        mobileSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        mobileSettings.quality = 0.35f;
                        mobileAudio.defaultSampleSettings = mobileSettings;

                        if (!string.IsNullOrEmpty(platform))
                        {
                            var platSettings = mobileAudio.GetOverrideSampleSettings(platform);
                            platSettings.loadType = AudioClipLoadType.CompressedInMemory;
                            platSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                            platSettings.quality = 0.35f;
                            mobileAudio.SetOverrideSampleSettings(platform, platSettings);
                        }
                    }
                    else
                        throw new ArgumentException($"Preset 'mobile_audio' requires an audio asset, got {importer.GetType().Name}");
                    break;

                case "humanoid_model":
                    if (importer is ModelImporter humanoidModel)
                    {
                        humanoidModel.animationType = ModelImporterAnimationType.Human;
                        humanoidModel.importAnimation = true;
                        humanoidModel.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        humanoidModel.isReadable = false;
                        humanoidModel.meshCompression = ModelImporterMeshCompression.Medium;
                        humanoidModel.importBlendShapes = true;
                    }
                    else
                        throw new ArgumentException($"Preset 'humanoid_model' requires a model asset, got {importer.GetType().Name}");
                    break;

                case "static_mesh":
                    if (importer is ModelImporter staticMesh)
                    {
                        staticMesh.animationType = ModelImporterAnimationType.None;
                        staticMesh.importAnimation = false;
                        staticMesh.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        staticMesh.isReadable = false;
                        staticMesh.meshCompression = ModelImporterMeshCompression.Medium;
                        staticMesh.addCollider = true;
                        staticMesh.generateSecondaryUV = true;
                        staticMesh.importBlendShapes = false;
                    }
                    else
                        throw new ArgumentException($"Preset 'static_mesh' requires a model asset, got {importer.GetType().Name}");
                    break;

                default:
                    throw new ArgumentException($"Unknown preset: {preset}. Valid presets: mobile_texture, hd_texture, web_audio, mobile_audio, humanoid_model, static_mesh");
            }
        }
    }
}
