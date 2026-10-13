using System;
using System.Globalization;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private bool meetingsOpen;
        private int meetingReadyFrame, meetingFocusIndex;
        private string meetingSelection, meetingError;
        private MeetingPreview meetingPreview;
        private Vector2 meetingIntroScroll, meetingDetailScroll;
        private GUIStyle meetingText;
        public void OpenMeetings()
        {
            if (Career == null || preview != null || practiceView != null || clubView != null) return;
            meetingsOpen = true; meetingReadyFrame = Time.frameCount + 1; meetingPreview = null; meetingError = null;
            meetingSelection = Career.Meetings.Pending?.Id ?? Career.Meetings.Results.LastOrDefault()?.Meeting.Id ?? Career.Meetings.Meetings.FirstOrDefault()?.Id;
            meetingFocusIndex = Math.Max(0, Array.FindIndex(Career.Calendar.TrainingFocuses.ToArray(), f => f.Id == Career.Meetings.PreferredTrainingFocusId));
        }
        private void PreviewMeeting(int index)
        {
            var pending = Career.Meetings.Pending; if (pending == null || index < 0 || index >= pending.Options.Count) return;
            try { meetingPreview = Career.Meetings.Preview(pending.Options[index].Id, Career.Calendar.TrainingFocuses.ElementAtOrDefault(meetingFocusIndex)?.Id); meetingError = null; meetingDetailScroll = Vector2.zero; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { meetingPreview = null; meetingError = e.Message; }
        }
        private void ConfirmMeeting()
        {
            if (meetingPreview == null) return;
            try { Career.Meetings.Commit(meetingPreview); if (meetingPreview.TrainingFocusId != null) trainingFocus = null; meetingPreview = null; meetingError = null; meetingDetailScroll = Vector2.zero; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { meetingPreview = null; meetingError = e.Message; }
        }
        private void LeaveMeeting()
        { if (Career.Meetings.Pending != null) { Career.Meetings.Leave(); meetingPreview = null; meetingError = null; meetingDetailScroll = Vector2.zero; } }
        private void CycleMeetingFocus()
        { if (Career.Calendar.TrainingFocuses.Count == 0) return; meetingFocusIndex = (meetingFocusIndex + 1) % Career.Calendar.TrainingFocuses.Count; meetingPreview = null; }
        private void UpdateMeetings()
        {
            if (Time.frameCount <= meetingReadyFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape) && Career.Meetings.Pending == null) { meetingsOpen = false; return; }
            var shown = meetingPreview;
            if (Input.GetKeyDown(KeyCode.F)) CycleMeetingFocus();
            for (int i = 0; i < 3; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) PreviewMeeting(i);
            if (Input.GetKeyDown(KeyCode.Return) && shown == meetingPreview) ConfirmMeeting();
        }
        private string ContractSummary()
        {
            var p = Career.UserPlayer;
            return Career.Clubs[p.ClubId].Name + " · Weekly wage " + p.CurrencyCode + " " + (p.WeeklyWageMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture) +
                " · Balance " + p.CurrencyCode + " " + (p.BalanceMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        }
        private static string MeetingResultText(MeetingResult r) => r.Text + (r.Percent.HasValue ? $"\nResponse: {(r.Succeeded == true ? "positive" : "negative")} · {r.Percent}% chance" : "") +
            "\nRecorded training focus: " + (r.FocusId ?? "scheduled") + " · Career priority: " + (r.Preference?.ToString() ?? "not yet stated") + "\n" +
            (r.Changes.Count == 0 ? "No further relationship change." : string.Join("\n", r.Changes.Select(c => c.DimensionId + $": {c.PreviousValue} → {c.CurrentValue}")));
        private void DrawMeetings()
        {
            var model = Career.Meetings; var pending = model.Pending;
            if (meetingText == null) meetingText = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            GUI.Label(new Rect(45, 30, 1320, 45), "CLUB CONVERSATIONS", body);
            GUI.Label(new Rect(45, 88, 1320, 35), ContractSummary(), meetingText);
            GUI.Label(new Rect(45, 126, 1320, 55), "Training plan: " + (model.PreferredTrainingFocusId ?? "scheduled focus") + " · Career priority: " + (model.Preference?.ToString() ?? "not yet stated") + "\nFuture opportunities primarily come through your agent.", meetingText);
            var meetings = model.Meetings.ToArray();
            for (int i = 0; i < meetings.Length; i++)
            {
                var m = meetings[i]; var a = Career.Calendar.ResultFor(m.CommitmentId);
                GUI.enabled = pending == null || pending == m;
                if (GUI.Button(new Rect(45, 210 + i * 115, 345, 98), m.Role + "\n" + (pending == m ? "CONVERSATION IN PROGRESS" : a?.Fact.Attendance.ToString() ?? "Scheduled"), button))
                { meetingSelection = m.Id; meetingPreview = null; meetingIntroScroll = meetingDetailScroll = Vector2.zero; }
            }
            GUI.enabled = true;
            var selected = meetings.FirstOrDefault(m => m.Id == meetingSelection);
            if (selected == null) { GUI.Label(new Rect(430, 210, 930, 90), "No authored meetings are available in this career.", meetingText); if (GUI.Button(new Rect(45, 835, 345, 45), "BACK [ESC]", button)) meetingsOpen = false; return; }
            var contact = Career.Social.Relationships[selected.ContactId];
            string intro = contact.Name + $" · Affinity {contact.Affinity} · Trust {contact.ProfessionalTrust} · Standing {contact.Standing}\n\n" + model.Introduction(selected);
            float introHeight = Math.Max(145, meetingText.CalcHeight(new GUIContent(intro), 890));
            meetingIntroScroll = GUI.BeginScrollView(new Rect(430, 200, 950, 150), meetingIntroScroll, new Rect(0, 0, 910, introHeight));
            GUI.Label(new Rect(0, 0, 890, introHeight), intro, meetingText); GUI.EndScrollView();
            if (pending == selected)
            {
                if (selected.Role == SocialRole.Coach && GUI.Button(new Rect(430, 357, 930, 38), "TAILORED FOCUS: " + Career.Calendar.TrainingFocuses[meetingFocusIndex].Name + " [F]", button)) CycleMeetingFocus();
                for (int i = 0; i < selected.Options.Count; i++)
                    if (GUI.Button(new Rect(430, 407 + i * 55, 930, 47), selected.Options[i].Text + " [" + (i + 1) + "]", button)) PreviewMeeting(i);
                string detail = meetingError ?? "The appointment's time and attendance effects are already recorded. Inspect a response, then confirm. No further time is charged.";
                if (meetingPreview != null)
                {
                    var p = meetingPreview;
                    detail = $"{p.Option.Trait}: {Career.UserPlayer.PersonalityTraits[p.Option.Trait]} · {p.Percent}% positive response\nSuccess: {p.Option.SuccessText}\n{EffectText(p.Option.Success)}\nFailure: {p.Option.FailureText}\n{EffectText(p.Option.Failure)}\nAlways recorded: focus {p.TrainingFocusId ?? "unchanged"}; priority {p.Option.Preference?.ToString() ?? "unchanged"}.";
                }
                DrawMeetingDetail(detail);
                GUI.Label(new Rect(430, 786, 940, 30), "Leaving: " + EffectText(selected.Leave) + ". Time is not refunded.", meetingText);
                if (GUI.Button(new Rect(430, 835, 400, 45), "LEAVE WITHOUT AGREEMENT", button)) LeaveMeeting();
                GUI.enabled = meetingPreview != null && Time.frameCount > meetingReadyFrame;
                if (GUI.Button(new Rect(875, 835, 495, 45), "CONFIRM RESPONSE [ENTER]", button)) ConfirmMeeting(); GUI.enabled = true;
            }
            else
            {
                var result = model.Results.LastOrDefault(r => r.Meeting == selected);
                var commitment = Career.Calendar.Commitments.Single(c => c.Id == selected.CommitmentId);
                DrawMeetingDetail(result == null ? "Scheduled: " + Career.Calendar.TimeLabel(commitment.StartMinute) + ". Attend or skip in the calendar, or visit the contact at the club during the appointment.\n\nAttendance: " + EffectText(selected.Attended) + "\nLate: " + EffectText(selected.Late) + "\nSkipped: " + EffectText(selected.Skipped) : MeetingResultText(result));
                if (GUI.Button(new Rect(45, 835, 345, 45), "BACK [ESC]", button)) meetingsOpen = false;
            }
        }
        private void DrawMeetingDetail(string text)
        {
            float h = Math.Max(180, meetingText.CalcHeight(new GUIContent(text), 895));
            meetingDetailScroll = GUI.BeginScrollView(new Rect(430, 585, 950, 195), meetingDetailScroll, new Rect(0, 0, 910, h));
            GUI.Label(new Rect(0, 0, 895, h), text, meetingText); GUI.EndScrollView();
        }
        private void VerifyMeetings()
        {
            var original = Career;
            try
            {
                Career = FootballRpg.Content.PrototypeContent.CreateCareer();
                foreach (var m in Career.Meetings.Meetings)
                {
                    var slot = Career.Calendar.Commitments.Single(c => c.Id == m.CommitmentId).StartMinute;
                    while (Career.Calendar.Minute < slot) Career.Calendar.Commit(Career.Calendar.Preview(CalendarChoiceKind.Wait));
                    Career.Calendar.Commit(Career.Calendar.Preview(CalendarChoiceKind.Attend, m.CommitmentId)); OpenMeetings();
                    PreviewMeeting(0); if (meetingPreview == null) throw new InvalidOperationException("Missing authored preview.");
                    ConfirmMeeting(); ConfirmMeeting();
                }
                if (Career.Meetings.Results.Count != 3 || Career.Meetings.DrawCount != 3 || Career.Meetings.Preference != CareerPreference.PlayingTime || Career.Meetings.PreferredTrainingFocusId != "role" || Career.Calendar.AwaitingMeeting)
                    throw new InvalidOperationException("Authored meeting state did not persist.");
                if (!ContractSummary().Contains(Career.UserPlayer.CurrencyCode)) throw new InvalidOperationException("Missing contract currency.");
                Debug.Log("FOOTBALL_RPG_MEETINGS_PASSED");
            }
            finally { Career = original; meetingsOpen = false; meetingPreview = null; meetingSelection = null; }
        }
    }
}
