// Menu item ARAI/Morphology Viewer: opens Dynamic_Scene (no edits), enters Play and spawns the runtime-only MorphologyViewer
// (Assets/Scripts/Diagnostics/MorphologyViewer.cs) through a SessionState flag. Docs: results/014/VIEWER.md.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MorphologyViewerMenu
{
    const string ScenePath = "Assets/Scenes/Dynamic_Scene.unity";

    [MenuItem("ARAI/Morphology Viewer")]
    static void Launch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[MorphViewer] already in Play mode: exit Play, then use the menu item again"); return; }
        if (System.IO.File.Exists("Temp/gates.json")) { Debug.LogError("[MorphViewer] Temp/gates.json present (a harness pass is pending): viewer not launched"); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        SessionState.SetBool(MorphologyViewer.SessionKey, true);
        EditorApplication.EnterPlaymode();
    }
}
