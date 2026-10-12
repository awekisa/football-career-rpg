using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class DecisionPresentationTests
    {
        private GameObject root;
        private PitchPreview view;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Decision View Test");
            view = root.AddComponent<PitchPreview>();
            view.Initialize(PrototypeContent.CreateCareer());
        }
        [TearDown] public void TearDown() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void InspectingOpponentsPreservesFrozenPassAndCameraDoesNotAdvancePlay()
        {
            var choice = view.ChosenPass;
            Assert.That(choice, Is.Not.Null);
            var decision = view.World.PendingDecision;
            Assert.That(view.SelectPlayer("meadow-player-01"), Is.True);
            Assert.That(view.SelectedPlayer.PrimaryPositionId, Is.EqualTo("gk"));
            Assert.That(view.ChosenPass, Is.SameAs(choice));
            view.AdjustCamera(10, 5, 20, -5);
            view.TogglePause(); view.Match.Advance(TimeSpan.FromSeconds(30));
            Assert.That(view.World.PendingDecision, Is.SameAs(decision));
            Assert.That(view.Match.Tick, Is.Zero);
            Assert.That(view.Match.DrawCount, Is.Zero);
        }

        [Test]
        public void TargetActionAndMarkerMatchExactlyTheCommittedChoice()
        {
            view.SelectPlayer("harbour-player-11");
            view.ChoosePassKind(PassKind.ThroughBall);
            view.SyncWorldView();
            var choice = view.ChosenPass;
            Assert.That(choice.Kind, Is.EqualTo(PassKind.ThroughBall));
            Assert.That(choice.Receiver.Id, Is.EqualTo("harbour-player-11"));
            var marker = root.GetComponentsInChildren<Transform>().Single(t => t.name == "Chosen Pass Target");
            Assert.That(marker.localPosition.x, Is.EqualTo((float)choice.Target.X));
            Assert.That(marker.localPosition.z, Is.EqualTo((float)choice.Target.Z));
            Assert.That(view.CommitChosenPass(), Is.True);
            Assert.That(view.CommitChosenPass(), Is.False);
            Assert.That(view.World.LastPass.Preview, Is.SameAs(choice));
            Assert.That(view.Match.DrawCount, Is.EqualTo(1));
            view.SyncWorldView();
            Assert.That(marker.gameObject.activeSelf, Is.False);
            Assert.That(view.Match.State, Is.EqualTo(MatchRunState.Running));
        }

        [Test]
        public void CyclingTargetsVisitsAllTenTeammatesWithoutChangingMatchState()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 10; i++) { view.CyclePassTarget(); ids.Add(view.ChosenPass.Receiver.Id); }
            Assert.That(ids.Count, Is.EqualTo(10));
            Assert.That(view.Match.Tick, Is.Zero);
            Assert.That(view.Match.DrawCount, Is.Zero);
        }
    }
}
