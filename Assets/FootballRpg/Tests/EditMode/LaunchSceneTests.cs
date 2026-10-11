using System.IO;
using FootballRpg.Editor;
using FootballRpg.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class LaunchSceneTests
    {
        [Test]
        public void CommittedLaunchSceneHasOneScreenCameraAndNoMissingScripts()
        {
            Assert.That(File.Exists(PrototypeBuild.LaunchScene), Is.True);
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.LaunchScene, OpenSceneMode.Additive);
            try
            {
                int screens = 0;
                int cameras = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    screens += root.GetComponentsInChildren<LaunchScreen>(true).Length;
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
                }
                Assert.That(screens, Is.EqualTo(1));
                Assert.That(cameras, Is.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void LaunchSceneIsEnabledForEditorPlayAndBuild()
        {
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(
                scene => scene.path == PrototypeBuild.LaunchScene && scene.enabled));
        }
    }
}
