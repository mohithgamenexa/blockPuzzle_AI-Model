using System.IO;
using System.Linq;
using BlockPuzzle.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockPuzzle.Editor
{
    /// <summary>
    /// Creates Assets/Scenes/Main.unity (camera + GameController) and registers it in
    /// Build Settings. Runs once automatically when the project is first opened, and is
    /// also available from the menu.
    /// </summary>
    [InitializeOnLoad]
    static class SceneSetup
    {
        internal const string ScenePath = "Assets/Scenes/Main.unity";

        static SceneSetup()
        {
            if (!File.Exists(ScenePath)) EditorApplication.delayCall += CreateIfMissing;
        }

        static void CreateIfMissing()
        {
            if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            CreateMainScene();
        }

        [MenuItem("Block Puzzle/Create Main Scene")]
        internal static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cam.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.BackgroundBottom;

            new GameObject("Block Puzzle", typeof(GameController));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"Block Puzzle: created {ScenePath} and added it to Build Settings. Press Play!");
        }
    }
}
