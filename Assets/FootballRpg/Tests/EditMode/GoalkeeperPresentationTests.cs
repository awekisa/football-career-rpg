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
    public sealed class GoalkeeperPresentationTests
    {
        [Test]
        public void KeeperDistributionTargetsInspectionAndCommitUseFrozenOptions()
        {
            var root = new GameObject("Keeper View");
            try
            {
                var data = PrototypeContent.Load(); data.userPlayerId = "harbour-player-01";
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(data));
                Assert.That(view.World.PendingDecision, Is.Not.Null); Assert.That(view.World.PendingKeeper, Is.Not.Null);
                view.ChooseKeeperAction(KeeperAction.ShortKick); view.SelectPlayer("harbour-player-02"); var choice = view.ChosenKeeperAction;
                Assert.That(choice.Receiver.Id, Is.EqualTo("harbour-player-02"));
                view.SelectPlayer("meadow-player-11"); Assert.That(view.ChosenKeeperAction, Is.SameAs(choice));
                view.CycleKeeperTarget(); Assert.That(view.ChosenKeeperAction.Receiver.Id, Is.Not.EqualTo(choice.Receiver.Id));
                choice = view.ChosenKeeperAction; Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.CommitChosenPass(), Is.False);
                Assert.That(view.World.LastKeeperAction.Preview, Is.SameAs(choice));
                view.Match.Advance(TimeSpan.FromMilliseconds(100)); view.SyncWorldView();
                view.TogglePause(); var ball = view.World.BallPosition; view.Match.Advance(TimeSpan.FromHours(1)); Assert.That(view.World.BallPosition, Is.EqualTo(ball));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void KeeperCanChooseOrdinaryFootballActionsAndOnlyOneMenuCanCommit()
        {
            var root = new GameObject("Keeper Feet View");
            try
            {
                var data = PrototypeContent.Load(); data.userPlayerId = "harbour-player-01";
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(data));
                var keeperDecision = view.World.PendingKeeper; var keeperChoice = keeperDecision.Options[0];
                view.ToggleKeeperFeet(); view.ChooseFootballAction(FootballActionKind.Dribble);
                Assert.That(view.ChosenKeeperAction, Is.Null); Assert.That(view.ChosenFootballAction, Is.Not.Null);
                Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.World.PendingKeeper, Is.Null);
                Assert.That(view.World.LastFootballAction.Preview.Kind, Is.EqualTo(FootballActionKind.Dribble));
                Assert.Throws<InvalidOperationException>(() => view.World.CommitKeeperDecision(keeperDecision.Id, keeperChoice.OptionId));
                Assert.That(view.Match.DrawCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavePromptCanBeInspectedAndCommittedThroughTheView()
        {
            var root = new GameObject("Keeper Save View");
            try
            {
                var data = PrototypeContent.Load(); data.userPlayerId = "harbour-player-01";
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(data));
                view.ChooseKeeperAction(KeeperAction.ShortKick); view.CommitChosenPass();
                for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++) view.Match.Advance(TimeSpan.FromMilliseconds(50));
                // The successful-shot helper reuses the actual committed attacking action path.
                bool prompt = false;
                for (int attempt = 0; attempt < 100 && !prompt; attempt++)
                {
                    GoalkeeperTests.ArrangeAttack(view.World); var shot = GoalkeeperTests.StartAttack(view.World);
                    if (!shot.Action.Succeeded) continue;
                    for (int i = 0; i < 500 && view.World.BallState == BallPhase.Travelling; i++) view.Match.Advance(TimeSpan.FromMilliseconds(50));
                    prompt = view.World.PendingKeeper != null;
                }
                Assert.That(prompt, Is.True);
                view.ChooseKeeperAction(KeeperAction.Parry); var choice = view.ChosenKeeperAction;
                view.SelectPlayer("meadow-player-11"); view.SyncWorldView();
                Assert.That(view.ChosenKeeperAction, Is.SameAs(choice));
                view.TogglePause(); Assert.That(view.Match.State, Is.EqualTo(MatchRunState.Paused));
                Assert.That(view.CommitChosenPass(), Is.True); Assert.That(view.World.LastKeeperAction.Preview, Is.SameAs(choice));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
