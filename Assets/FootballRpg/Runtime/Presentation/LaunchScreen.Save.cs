using System;
using System.IO;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private CareerSaveStore saveStore;
        private CareerSaveStore Saves => saveStore ?? (saveStore = CreateSaveStore(Path.Combine(Application.persistentDataPath, "career-v1.json")));
        public static CareerSaveStore CreateSaveStore(string path)
        {
            string content = string.Join("\n", new[] { "PrototypeFixture", "CharacterCreation", "ClubVenue" }.Select(n => n + ":" + Resources.Load<TextAsset>(n).text));
            return new CareerSaveStore(path, CareerSaveStore.Hash(content), PrototypeContent.Load());
        }
        public SaveResult SaveCareer() => Saves.Save(Career);
        public SaveResult LoadCareer()
        {
            var result = Saves.Load(); if (!result.Success) return result;
            // Decode completely before touching the current session or its presentation.
            preview?.Close(); practiceView?.Close(); clubView?.Close(); Career?.Week.World?.Dispose();
            preview = null; practiceView = null; clubView = null; clubExploration = null;
            Career = result.Career; creationDraft = null; calendarChoice = null; trainingFocus = null;
            calendarOpen = reportsOpen = coachOpen = socialOpen = meetingsOpen = returnToClub = showTrainingResults = false;
            if (Career.Week.Phase == CareerWeekPhase.Match) ContinueWeek();
            else if (Career.Week.Phase == CareerWeekPhase.Complete) OpenReports();
            else if (Career.Meetings.Pending != null) OpenMeetings();
            else if (Career.Week.Phase == CareerWeekPhase.ClubWeek) OpenCalendar();
            return result;
        }
        private void VerifySaveRestore()
        {
            string dir = Path.Combine(Path.GetTempPath(), "football-save-smoke-" + Guid.NewGuid().ToString("N"));
            CareerState restored = null, matchCopy = null, c = null;
            try
            {
                c = PrototypeContent.CreateCareer(); c.Week.Begin(); var store = CreateSaveStore(Path.Combine(dir, "career.json"));
                c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "mon-training"));
                if (!store.Save(c).Success) throw new InvalidOperationException("Career save failed.");
                var load = store.Load(); if (!load.Success || load.Career.Calendar.Minute != c.Calendar.Minute) throw new InvalidOperationException("Career load failed.");
                restored = load.Career;
                foreach (var x in new[] { c, restored }) x.Calendar.Commit(x.Calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice"));
                if (c.Training.Results.Last().TotalXp != restored.Training.Results.Last().TotalXp) throw new InvalidOperationException("Loaded training diverged.");
                while (c.Calendar.Minute < 25 * 360) c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Wait));
                c.Calendar.Commit(c.Calendar.Preview(CalendarChoiceKind.Attend, "sun-match"));
                c.Week.StartFixture(new CompetitionRules(10, 0, .1)); c.Week.Match.Pause();
                if (!store.Save(c).Success) throw new InvalidOperationException("Match save failed.");
                matchCopy = store.Load().Career;
                if (matchCopy == null || store.Encode(c) != store.Encode(matchCopy)) throw new InvalidOperationException("Match snapshot changed.");
                foreach (var x in new[] { c, matchCopy }) { x.Week.Match.Resume(); x.Week.Match.Advance(TimeSpan.FromSeconds(2)); x.Week.Match.Pause(); }
                if (store.Encode(c) != store.Encode(matchCopy)) throw new InvalidOperationException("Loaded match diverged.");
                File.WriteAllText(store.Path, "corrupt");
                var recovery = store.Load();
                if (recovery.Status != SaveStatus.Recovered || recovery.Career.Calendar.Minute != 360) throw new InvalidOperationException("Backup recovery failed.");
                Debug.Log("FOOTBALL_RPG_SAVE_RESTORE_READY");
            }
            finally { c?.Week.World?.Dispose(); matchCopy?.Week.World?.Dispose(); restored?.Week.World?.Dispose(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
