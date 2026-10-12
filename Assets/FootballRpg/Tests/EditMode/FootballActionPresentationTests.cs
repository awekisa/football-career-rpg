using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Presentation;
using FootballRpg.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace FootballRpg.Tests
{
    public sealed class FootballActionPresentationTests
    {
        [TestCase(false)] [TestCase(true)]
        public void ActionSelectionCommitAndVisualFlightUseTheSameFrozenOption(bool incoming)
        {
            var root = new GameObject("Football Actions View");
            try
            {
                var view = root.AddComponent<PitchPreview>();
                view.Initialize(PrototypeContent.CreateCareer(), openingArrivalHeight: incoming ? (double?)1.8 : null);
                view.ChooseFootballAction(incoming ? FootballActionKind.HeaderPass : FootballActionKind.Shot);
                var first = view.ChosenFootballAction;
                Assert.That(first, Is.Not.Null);
                view.ChooseFootballAction(first.Kind);
                var second = view.ChosenFootballAction;
                Assert.That(second.Variant, Is.Not.EqualTo(first.Variant));
                view.SelectPlayer("meadow-player-01");
                Assert.That(view.ChosenFootballAction, Is.SameAs(second));
                Assert.That(view.CommitChosenPass(), Is.True);
                Assert.That(view.CommitChosenPass(), Is.False);
                Assert.That(view.World.LastFootballAction.Preview, Is.SameAs(second));
                Assert.That(view.World.LastFootballAction.Action.SuccessProbability, Is.EqualTo(second.Percent / 100f));
                view.Match.Advance(TimeSpan.FromMilliseconds(100));
                view.SyncWorldView();
                var ball = root.GetComponentsInChildren<Transform>().Single(t => t.name == "Ball");
                Assert.That(ball.localPosition.y, Is.EqualTo((float)view.World.BallHeight));
                Assert.That(view.Match.DrawCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CrossIsAvailableByCircumstanceAndTargetSelectionFollowsTheRecipient()
        {
            var definitions = PrototypeContent.Load(); definitions.userPlayerId = "harbour-player-09";
            var root = new GameObject("Cross View");
            try
            {
                var view = root.AddComponent<PitchPreview>(); view.Initialize(CareerState.Create(definitions));
                view.SelectPlayer("harbour-player-11"); view.ChooseFootballAction(FootballActionKind.Cross);
                Assert.That(view.ChosenFootballAction.Receiver.Id, Is.EqualTo("harbour-player-11"));
                view.SelectPlayer("harbour-player-08");
                Assert.That(view.ChosenFootballAction.Receiver.Id, Is.EqualTo("harbour-player-08"));
                view.ChoosePassKind(PassKind.ToFeet);
                Assert.That(view.ChosenFootballAction, Is.Null);
                Assert.That(view.ChosenPass.Receiver.Id, Is.EqualTo("harbour-player-08"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
