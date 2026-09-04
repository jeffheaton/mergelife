// Fallback for a missing main scene, and the one place that asserts the build
// scene list. tools/bootstrap.sh copies the real HeatonCAMainScene.unity from
// heaton-life-unity (its App object binds to the reserved AppController GUID),
// so in the normal case this only reasserts EditorBuildSettings. When the
// scene is gone it is rebuilt from nothing as an empty scene: everything the
// app shows is constructed at runtime by AppController, so an empty scene is
// the correct scene, not a placeholder. Adapted from heaton-voxel's
// SceneBootstrap, minus the objects that project builds by hand.
//
//   Unity -batchmode -quit -projectPath unity/heaton-ca \
//         -executeMethod HeatonCA.Editor.SceneBootstrap.Run

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatonCA.Editor
{
    /// <summary>
    /// Creates <c>Assets/Scenes/HeatonCAMainScene.unity</c> when it is missing
    /// and makes it the only (enabled) scene in the build settings.
    /// </summary>
    public static class SceneBootstrap
    {
        /// <summary>Folder that holds the app's single scene.</summary>
        public const string ScenesFolder = "Assets/Scenes";

        /// <summary>Project-relative path of the app's single scene.</summary>
        public const string ScenePath = ScenesFolder + "/HeatonCAMainScene.unity";

        private const string Tag = "[SceneBootstrap]";

        /// <summary>
        /// Ensures the main scene exists (creating an empty one if not) and sets
        /// <see cref="EditorBuildSettings.scenes"/> to exactly that scene, enabled.
        /// Idempotent: when the scene exists and the list already matches, nothing
        /// is written. A recreated scene gets a fresh GUID from Unity; the Build
        /// Profiles under Assets/Settings use the global scene list
        /// (m_OverrideGlobalSceneList 0), so the list set here is what builds use.
        /// </summary>
        public static void Run()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absoluteScenePath = Path.Combine(projectRoot, ScenePath);
            bool created = false;

            if (!File.Exists(absoluteScenePath))
            {
                if (!AssetDatabase.IsValidFolder(ScenesFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Scenes");
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError($"{Tag} could not save {ScenePath}");
                    EditorApplication.Exit(1);
                    return;
                }

                created = true;
                Debug.Log($"{Tag} created empty scene {ScenePath}");
            }

            bool listChanged = false;
            if (!BuildListIsExactly(ScenePath))
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(ScenePath, true),
                };
                listChanged = true;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} scene={ScenePath} created={created} buildListChanged={listChanged} "
                      + $"buildScenes={EditorBuildSettings.scenes.Length}");
        }

        /// <summary>True when the build list holds exactly one enabled entry for <paramref name="path"/>.</summary>
        private static bool BuildListIsExactly(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            return scenes != null
                && scenes.Length == 1
                && scenes[0].enabled
                && scenes[0].path == path;
        }
    }
}
