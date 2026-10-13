using System.Linq;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class PitchPreview
    {
        private bool coachPanel;
        private Vector2 coachHistoryScroll;
        private void OpenMatchCoach() { coachPanel = true; Match.Pause(); }
        private void DrawMatchCoach(float width)
        {
            var c = Career.Coach; var r = c.Relationship;
            Panel(new Rect(0, 0, width, 900), Ink);
            Text(new Rect(40, 35, width - 80, 60), c.Profile.Name + " · MATCH INSTRUCTIONS", heading, Color.white);
            Text(new Rect(40, 115, width - 80, 115), World.CoachMessage + "\nPreferred formation " + c.Profile.PreferredFormation + $" · Discipline {c.Profile.DisciplinePreference:P0} · Creativity {c.Profile.CreativityPreference:P0}\nTrust {r.ProfessionalTrust} · Standing {r.Standing} · {(c.EstablishedStar ? "Established star: more freedom" : "Prospect: earn tactical freedom")}", label, Lime);
            Text(new Rect(40, 250, width - 80, 110), "Automatic movement follows the current instruction. Use off-ball choices [I] to depart from it.\nCoach evaluates the result, danger, late attacking need and your standing.\nThis screen pauses play; closing it leaves the match paused so you can inspect or choose.", muted, Color.white);
            var rows = c.Reactions.Reverse().Take(20).ToArray();
            coachHistoryScroll = GUI.BeginScrollView(new Rect(40, 390, width - 80, 330), coachHistoryScroll, new Rect(0, 0, width - 110, Mathf.Max(330, rows.Length * 70)));
            for (int i = 0; i < rows.Length; i++) Text(new Rect(0, i * 70, width - 130, 65), rows[i].Reason + $"\nTrust {rows[i].TrustDelta:+0;-0;0} · Standing {rows[i].StandingDelta:+0;-0;0}", muted, Color.white);
            GUI.EndScrollView();
            if (Button(new Rect(40, 790, 340, 60), "BACK TO MATCH [T / ESC]")) coachPanel = false;
        }
    }
}
