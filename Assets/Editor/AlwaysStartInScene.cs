#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// When you hit Play in the Editor, this will always switch
/// to your chosen “startup” scene first (saving current scenes if needed).
/// </summary>
[InitializeOnLoad]
public static class AlwaysStartInScene
{
    // Change this to the exact path of your startup scene:
    private const string k_StartupSceneName = "Startup";

    static AlwaysStartInScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Just before entering Play Mode from Edit Mode...
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Find the scene in your Build Settings that has the name “Startup”
            var buildScene = EditorBuildSettings.scenes
                .FirstOrDefault(s => System.IO.Path.GetFileNameWithoutExtension(s.path) == k_StartupSceneName);

            if (buildScene.path == null)
            {
                Debug.LogError($"[AlwaysStartInScene] Could not find a scene named “{k_StartupSceneName}” in Build Settings!");
                return;
            }

            // Prompt to save any unsaved changes in the current scene(s)
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // User canceled save → abort entering Play mode
                EditorApplication.isPlaying = false;
                return;
            }

            // Open the startup scene, single-edit mode
            EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            Debug.Log($"[AlwaysStartInScene] Opening startup scene: {buildScene.path}");
        }
    }
}
#endif
