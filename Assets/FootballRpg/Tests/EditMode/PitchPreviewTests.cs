using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class PitchPreviewTests
    {
        private GameObject root;
        private PitchPreview preview;
        private CareerState career;

        [SetUp]
        public void SetUp()
        {
            career = PrototypeContent.CreateCareer();
            root = new GameObject("Preview Test");
            preview = root.AddComponent<PitchPreview>();
            preview.Initialize(career);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void FieldHasRealScaleGoalsBallAndTwentyTwoUniquePlayers()
        {
            Assert.That(PitchGeometry.Length, Is.EqualTo(105));
            Assert.That(PitchGeometry.Width, Is.EqualTo(68));
            Assert.That(preview.Players.Count, Is.EqualTo(22));
            Assert.That(preview.Players.Select(p => p.PlayerId).Distinct().Count(), Is.EqualTo(22));
            Assert.That(preview.Players.Count(p => p.IsHome), Is.EqualTo(11));
            Assert.That(preview.Players.Count(p => p.IsUser), Is.EqualTo(1));
            var names = root.GetComponentsInChildren<Transform>().Select(t => t.name).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Home Goal", "Away Goal", "Ball", "Your Player" }, names);
            Assert.That(preview.SelectedPlayer, Is.SameAs(career.UserPlayer));
            foreach (var player in preview.Players)
            {
                Assert.That(Mathf.Abs(player.transform.position.x), Is.LessThan(52.5f));
                Assert.That(Mathf.Abs(player.transform.position.z), Is.LessThan(34));
                Assert.That(player.GetComponent<Collider>().enabled, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MouseRaySelectsBothTeamsWithoutChangingSimulationOrCareer(bool running)
        {
            if (running) preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromMilliseconds(100));
            long tick = preview.Match.Tick;
            ulong random = preview.Match.RandomState;
            foreach (var marker in preview.Players)
            {
                var screenPoint = preview.ViewCamera.WorldToScreenPoint(marker.transform.position + Vector3.up * 1.1f);
                Assert.That(preview.SelectAtScreenPoint(screenPoint), Is.True, marker.PlayerId + " at " + screenPoint + " in " + preview.ViewCamera.pixelRect);
                Assert.That(preview.SelectedPlayer, Is.SameAs(career.Players[marker.PlayerId]));
                Assert.That(preview.SelectedPlayer.PrimaryPositionId, Is.Not.Empty);
                Assert.That(preview.SelectedPlayer.FootballAttributes.Count, Is.EqualTo(31));
            }
            Assert.That(preview.Match.Tick, Is.EqualTo(tick));
            Assert.That(preview.Match.RandomState, Is.EqualTo(random));
            Assert.That(preview.Match.Diagnostics.Count, Is.Zero);
            Assert.That(career.UserPlayer.Confidence, Is.EqualTo(.65f));
        }

        [Test]
        public void CameraControlsWorkWhilePausedAndDoNotTouchTheMatchClock()
        {
            Vector3 originalPosition = preview.ViewCamera.transform.position;
            preview.AdjustCamera(10, 5, 25, -10);
            Assert.That(preview.ViewCamera.transform.position, Is.Not.EqualTo(originalPosition));
            Assert.That(preview.Zoom, Is.EqualTo(40));
            Assert.That(preview.Yaw, Is.EqualTo(25));
            preview.AdjustCamera(999, -999, 0, -999);
            Assert.That(preview.Zoom, Is.EqualTo(25));
            Assert.That(preview.Match.State, Is.EqualTo(MatchRunState.Paused));
            Assert.That(preview.Match.Tick, Is.Zero);
            preview.ResetCamera();
            Assert.That(preview.ViewCamera.transform.position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void InvalidSelectionAndSidebarClicksLeaveTheSelectionUntouched()
        {
            var selected = preview.SelectedPlayer;
            Assert.That(preview.SelectPlayer("not-in-lineup"), Is.False);
            Assert.That(preview.SelectAtScreenPoint(new Vector2(Screen.width - 1, Screen.height / 2)), Is.False);
            Assert.That(preview.SelectedPlayer, Is.SameAs(selected));
        }

        [Test]
        public void RunningClockCanBePausedWhileInspectionRemainsAvailable()
        {
            preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromMilliseconds(150));
            Assert.That(preview.Match.Tick, Is.EqualTo(3));
            preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromHours(1));
            Assert.That(preview.Match.Tick, Is.EqualTo(3));
            Assert.That(preview.SelectPlayer(preview.Players.Last().PlayerId), Is.True);
        }

        [Test]
        public void ViewsAndTacticalMarkerFollowTheWorldAndFreezeOnPause()
        {
            preview.TogglePause();
            preview.Match.Advance(TimeSpan.FromSeconds(5));
            preview.SyncWorldView();
            foreach (var marker in preview.Players)
            {
                var motion = preview.World.Players.Single(p => p.Player.Id == marker.PlayerId);
                Assert.That(marker.transform.localPosition.x, Is.EqualTo((float)motion.Position.X));
                Assert.That(marker.transform.localPosition.z, Is.EqualTo((float)motion.Position.Z));
            }
            var target = root.GetComponentsInChildren<Transform>().Single(t => t.name == "Expected Tactical Position");
            var user = preview.World.Players.Single(p => p.Player.Id == career.UserPlayerId);
            Assert.That(target.localPosition.x, Is.EqualTo((float)user.Target.X));
            preview.TogglePause();
            var previous = target.localPosition;
            preview.Match.Advance(TimeSpan.FromHours(1));
            preview.SyncWorldView();
            Assert.That(target.localPosition, Is.EqualTo(previous));
        }

        [Test]
        public void ClosingPreviewRestoresTheOriginalCamera()
        {
            preview.Close();
            Assert.That(root == null, Is.True);
        }
    }
}
