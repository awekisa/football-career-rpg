using System;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private void VerifyPractice()
        {
            foreach (PracticeFamily family in Enum.GetValues(typeof(PracticeFamily)))
            {
                var career = PrototypeContent.CreateCareer();
                var choice = career.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training", practice: family);
                career.Calendar.Commit(choice);
                var view = new GameObject("Practice Smoke").AddComponent<GuidedPracticeView>();
                view.Initialize(career, null);
                if (view.ViewCamera == null || !career.Calendar.AwaitingPractice || career.Training.Results.Count != 0)
                    throw new InvalidOperationException("Practice attendance failed.");
                for (int round = 0; round < 3; round++)
                {
                    view.Select(0); int chance = view.Session.Decision.Options[0].Percent;
                    view.Continue();
                    if (Math.Abs(view.Session.LastResult.SuccessProbability - chance / 100f) > .00001)
                        throw new InvalidOperationException("Practice differs from displayed probability.");
                    if (round < 2) view.Continue();
                }
                career.Calendar.Commit(choice); career.Training.FinishPractice();
                if (career.Calendar.Minute != 360 || career.Calendar.AwaitingPractice || career.Training.Results.Count != 1 ||
                    career.Training.Results[0].TotalXp <= 0 || view.Session.DrawCount != 3)
                    throw new InvalidOperationException("Practice reward/slot accounting failed.");
                view.Close();
            }
            Debug.Log("FOOTBALL_RPG_GUIDED_PRACTICE_PASSED");
        }
    }
}
