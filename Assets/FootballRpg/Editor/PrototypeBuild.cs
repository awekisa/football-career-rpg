using System;
using System.IO;
using FootballRpg.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRpg.Editor
{
    public static class PrototypeBuild
    {
        public const string LaunchScene = "Assets/FootballRpg/Scenes/Launch.unity";

        [MenuItem("Football RPG/Create Missing Launch Scene")]
        public static void CreateLaunchScene()
        {
            // Never overwrite an authored scene or discard unsaved editor work.
            if (File.Exists(LaunchScene)) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(LaunchScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.025f, 0.06f, 0.07f);
            new GameObject("Launch Screen", typeof(LaunchScreen));
            EditorSceneManager.SaveScene(scene, LaunchScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LaunchScene, true) };
            PlayerSettings.companyName = "Football Career RPG";
            PlayerSettings.productName = "Football Career RPG";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Football RPG/Build macOS Prototype")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/Football Career RPG.app");
        }

        [MenuItem("Football RPG/Build Windows Prototype")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Football Career RPG.exe");
        }

        private static void Build(BuildTarget target, string output)
        {
            if (!File.Exists(LaunchScene)) throw new InvalidOperationException("Committed launch scene is missing.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException($"Install the {target} build support module in Unity Hub.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { LaunchScene },
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"FOOTBALL_RPG_BUILD_PASSED: {output}");
        }
    }
}
