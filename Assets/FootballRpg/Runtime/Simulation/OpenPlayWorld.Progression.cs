using System.Collections.Generic;

namespace FootballRpg.Simulation
{
    public sealed partial class OpenPlayWorld
    {
        private SkillProgression progression;
        private CareerReadiness readiness;
        private string progressionMatchId;
        private void Progress(ActionResolved action, IReadOnlyDictionary<string, float> skills, double significance, RestartKind? restart = null)
        {
            if (progressionMatchId == null) return; // Practice has its own future training contract.
            string specialist = restart == RestartKind.Penalty ? "penalties" : restart == RestartKind.Corner ? "corners"
                : restart == RestartKind.DirectFreeKick || restart == RestartKind.IndirectFreeKick ? "free-kicks" : null;
            if (specialist != null) skills = new Dictionary<string, float> { [specialist] = .8f, ["decisions"] = .2f };
            if (restart == RestartKind.Penalty) significance = 3;
            else if (restart == RestartKind.Corner || specialist == "free-kicks") significance = 1.5;
            else if (restart.HasValue) significance = .5;
            progression.Apply(progressionMatchId, action, skills, significance);
            readiness.ApplyPerformance(progressionMatchId, action, significance);
        }
    }
}
