using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    [Category("FootballVerification")]
    public sealed class DefensePresentationTests
    {
        [Test]
        public void DefenseSelectionInspectionAndCommitUseFrozenProbabilities()
        {
            var root = new GameObject("Defense View");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer());
                view.CommitChosenPass();
                // Finish the opening flight before setting up the spatial threat.
                for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++) view.Match.Advance(TimeSpan.FromMilliseconds(50));
                DefenseTests.Attack(view.World);
                view.Match.Advance(TimeSpan.FromMilliseconds(50));
                Assert.That(view.World.PendingDefense, Is.Not.Null);
                view.ChooseDefense(DefensiveAction.Block); var chosen = view.ChosenDefense;
                view.SelectPlayer("meadow-player-11"); view.SyncWorldView();
                Assert.That(view.ChosenDefense, Is.SameAs(chosen));
                view.TogglePause(); Assert.That(view.Match.State, Is.EqualTo(MatchRunState.Paused));
                Assert.That(view.CommitChosenPass(), Is.True);
                Assert.That(view.World.LastDefense.Preview, Is.SameAs(chosen));
                Assert.That(view.World.LastDefense.Action.SuccessProbability, Is.EqualTo(chosen.Chance));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void SelectedOpponentChangesMarkingTargetWithoutControllingTheirMovement()
        {
            var root = new GameObject("Intent View");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(PrototypeContent.CreateCareer());
                Assert.That(view.ChooseIntent(OffBallIntent.Press), Is.False);
                view.CommitChosenPass();
                for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++) view.Match.Advance(TimeSpan.FromMilliseconds(50));
                DefenseTests.Possess(view.World, "meadow-player-11");
                view.Match.Pause(); view.SelectPlayer("meadow-player-09");
                var opponent = view.World.Players.Single(p => p.Player.Id == "meadow-player-09"); var target = opponent.Target;
                Assert.That(view.ChooseIntent(OffBallIntent.PersonalMark), Is.True);
                Assert.That(view.World.ActiveIntent.TargetId, Is.EqualTo(opponent.Player.Id));
                Assert.That(opponent.Target, Is.EqualTo(target));
                view.SelectPlayer("harbour-player-02");
                Assert.That(view.ChooseIntent(OffBallIntent.PersonalMark), Is.False);
                Assert.That(view.World.ActiveIntent.TargetId, Is.EqualTo(opponent.Player.Id));
                Assert.That(view.ChooseIntent(OffBallIntent.Automatic), Is.True);
                Assert.That(view.World.ActiveIntent, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
