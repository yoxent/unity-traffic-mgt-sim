using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcpPro
{
    public class MultiSceneCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("load_scene_additive", LoadSceneAdditive);
            router.Register("unload_scene", UnloadScene);
            router.Register("get_loaded_scenes", GetLoadedScenes);
            router.Register("set_active_scene", SetActiveScene);
        }

        private static object LoadSceneAdditive(Dictionary<string, object> p)
        {
            ThrowIfPlaying("load_scene_additive");

            string scenePath = GetStringParam(p, "scene_path");
            bool setActive = GetBoolParam(p, "set_active");

            if (string.IsNullOrEmpty(scenePath))
                throw new ArgumentException("scene_path is required");

            // Verify the scene asset exists
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
                throw new ArgumentException($"Scene not found at: {scenePath}");

            // Check if scene is already loaded
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath && loadedScene.isLoaded)
                {
                    if (setActive)
                        SceneManager.SetActiveScene(loadedScene);

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "sceneName", loadedScene.name },
                        { "scenePath", scenePath },
                        { "alreadyLoaded", true },
                        { "isActive", setActive }
                    };
                }
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            if (setActive)
                SceneManager.SetActiveScene(scene);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "sceneName", scene.name },
                { "scenePath", scene.path },
                { "alreadyLoaded", false },
                { "isActive", setActive },
                { "rootObjectCount", scene.rootCount },
                { "totalLoadedScenes", SceneManager.sceneCount }
            };
        }

        private static object UnloadScene(Dictionary<string, object> p)
        {
            ThrowIfPlaying("unload_scene");

            string sceneName = GetStringParam(p, "scene_name");
            bool save = GetBoolParam(p, "save", true);

            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("scene_name is required");

            if (SceneManager.sceneCount <= 1)
                throw new InvalidOperationException("Cannot unload the last remaining scene. At least one scene must be loaded.");

            Scene targetScene = default;
            bool found = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName || scene.path == sceneName)
                {
                    targetScene = scene;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new ArgumentException($"Scene not found among loaded scenes: {sceneName}");

            string unloadedName = targetScene.name;
            string unloadedPath = targetScene.path;
            bool wasDirty = targetScene.isDirty;

            if (save && targetScene.isDirty)
                EditorSceneManager.SaveScene(targetScene);

            EditorSceneManager.CloseScene(targetScene, true);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "sceneName", unloadedName },
                { "scenePath", unloadedPath },
                { "wasDirty", wasDirty },
                { "saved", save && wasDirty },
                { "remainingScenes", SceneManager.sceneCount }
            };
        }

        private static object GetLoadedScenes(Dictionary<string, object> p)
        {
            var activeScene = SceneManager.GetActiveScene();
            var scenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                scenes.Add(new Dictionary<string, object>
                {
                    { "name", scene.name },
                    { "path", scene.path },
                    { "isLoaded", scene.isLoaded },
                    { "isDirty", scene.isDirty },
                    { "rootCount", scene.rootCount },
                    { "buildIndex", scene.buildIndex },
                    { "isActive", scene == activeScene }
                });
            }

            return new Dictionary<string, object>
            {
                { "sceneCount", SceneManager.sceneCount },
                { "activeScene", activeScene.name },
                { "scenes", scenes }
            };
        }

        private static object SetActiveScene(Dictionary<string, object> p)
        {
            string sceneName = GetStringParam(p, "scene_name");
            if (string.IsNullOrEmpty(sceneName))
                throw new ArgumentException("scene_name is required");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName || scene.path == sceneName)
                {
                    if (!scene.isLoaded)
                        throw new InvalidOperationException($"Scene '{sceneName}' is not loaded. Load it first before setting as active.");

                    SceneManager.SetActiveScene(scene);

                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "activeScene", scene.name },
                        { "activePath", scene.path },
                        { "message", $"Active scene set to: {scene.name}" }
                    };
                }
            }

            throw new ArgumentException($"Scene not found among loaded scenes: {sceneName}. Use get_loaded_scenes to see available scenes.");
        }
    }
}
