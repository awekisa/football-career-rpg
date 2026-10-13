using System;
using System.Linq;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private bool socialOpen;
        private string socialPerson, socialError;
        private DialoguePreview socialPreview;
        private Vector2 socialContactsScroll, socialResultScroll;
        private GUIStyle socialTraitsStyle;
        private int socialReadyFrame;
        public void OpenSocial()
        {
            if (Career == null || preview != null || clubView != null || practiceView != null) return;
            socialOpen = true; socialReadyFrame = Time.frameCount + 1; socialPreview = null; socialError = null;
            if (socialPerson == null || !Career.Social.Relationships.ContainsKey(socialPerson)) socialPerson = Career.Social.Relationships.Keys.First();
        }
        private void SelectSocialPerson(string id) { socialPerson = id; socialPreview = null; socialError = null; socialResultScroll = Vector2.zero; }
        private void PreviewSocial(int index)
        {
            if (index < 0 || index >= Career.Social.Options.Count) return;
            try { socialPreview = Career.Social.Preview(socialPerson, Career.Social.Options[index].Id); socialError = null; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { socialPreview = null; socialError = e.Message; }
        }
        private void ConfirmSocial()
        {
            if (socialPreview == null) return;
            try { Career.Social.Commit(socialPreview); socialPreview = null; socialError = null; socialResultScroll = Vector2.zero; }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { socialPreview = null; socialError = e.Message; }
        }
        private void UpdateSocial()
        {
            if (Time.frameCount <= socialReadyFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { socialOpen = false; socialPreview = null; return; }
            var shown = socialPreview;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var people = Career.Social.Relationships.Keys.ToArray(); SelectSocialPerson(people[(Array.IndexOf(people, socialPerson) + 1) % people.Length]);
            }
            for (int i = 0; i < Career.Social.Options.Count && i < 9; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) PreviewSocial(i);
            if (Input.GetKeyDown(KeyCode.Return) && shown == socialPreview) ConfirmSocial();
        }
        private static string EffectText(SocialEffect effect) => $"Affinity {effect.Affinity:+0;-0;0} · Trust {effect.Trust:+0;-0;0} · Standing {effect.Standing:+0;-0;0}";
        private void DrawSocial()
        {
            var social = Career.Social; var contact = social.Relationships[socialPerson];
            GUI.Label(new Rect(45, 25, 1300, 45), "RELATIONSHIPS  ·  " + Career.Calendar.TimeLabel(Career.Calendar.Minute), body);
            GUI.Label(new Rect(45, 80, 1320, 75), "Personal liking and professional trust are independent. Choose how you behave, even against your usual character.\nBrief exchanges cost no slot; each contact has one exchange per slot. Scheduled meetings are separate.", small);
            var people = social.Relationships.Values.ToArray();
            socialContactsScroll = GUI.BeginScrollView(new Rect(45, 170, 435, 590), socialContactsScroll, new Rect(0, 0, 410, people.Length * 75));
            for (int i = 0; i < people.Length; i++)
            {
                var p = people[i];
                if (GUI.Button(new Rect(0, i * 75, 400, 66), (p.PersonId == socialPerson ? "• " : "") + p.Name + "\n" + p.Role + " · " + social.Bond(p.PersonId), button)) SelectSocialPerson(p.PersonId);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(525, 165, 850, 35), contact.Name + "  ·  " + contact.Role + "  ·  " + social.Bond(contact.PersonId), body);
            GUI.Label(new Rect(525, 207, 850, 32), $"Affinity {contact.Affinity} / ±100  ·  Professional trust {contact.ProfessionalTrust}/100  ·  Standing {contact.Standing}/100", small);
            string unavailable = social.UnavailableReason(socialPerson);
            GUI.enabled = unavailable == null;
            for (int i = 0; i < social.Options.Count; i++)
            {
                var option = social.Options[i]; string chance = unavailable == null ? "  ·  " + social.Preview(socialPerson, option.Id).Percent + "%" : "";
                if (GUI.Button(new Rect(525, 260 + i * 51, 850, 44), (socialPreview?.Option.Id == option.Id ? "• " : "") + (i + 1) + ". " + option.Text + chance, button)) PreviewSocial(i);
            }
            GUI.enabled = true;
            string explanation = socialError ?? unavailable ?? "Select a choice to inspect its chance and both possible consequences. Nothing changes until you confirm.";
            if (socialPreview != null)
            {
                var p = socialPreview; var o = p.Option;
                explanation = $"{o.CheckTrait}: {p.TraitValue} · difficulty {o.Difficulty} · actual success chance {p.Percent}%\n" +
                    "On success: " + o.SuccessText + "\n" + EffectText(o.Success) + "\n\nOn failure: " + o.FailureText + "\n" + EffectText(o.Failure) +
                    $"\n\nBehaviour: {o.BehaviorTrait} {o.BehaviorDirection:+0;-0}. {social.Rules.EvidencePerTraitPoint} net observations across separate slots change it by one point. Outcome does not force your next choice.";
            }
            else
            {
                var last = social.Results.LastOrDefault(r => r.Preview.PersonId == socialPerson);
                if (last != null)
                {
                    explanation += "\n\nLAST EXCHANGE: " + last.Preview.Option.Text + "\n" + last.Outcome + $"\nChance {last.Preview.Percent}% · " + (last.Succeeded ? "Succeeded" : "Failed") +
                        "\n" + (last.Changes.Count == 0 ? "No relationship values changed." : string.Join("\n", last.Changes.Select(c => c.DimensionId + $": {c.PreviousValue} → {c.CurrentValue}")));
                    var b = last.Behavior;
                    explanation += $"\nBehaviour: {b.TraitId} · evidence {b.EvidenceAfter:+0;-0;0}/{social.Rules.EvidencePerTraitPoint}" +
                        (b.Counted ? "" : " (already counted this slot)") + (b.CurrentValue == b.PreviousValue ? " · trait unchanged" : $" · trait {b.PreviousValue} → {b.CurrentValue}");
                }
            }
            float height = Mathf.Max(270, small.CalcHeight(new GUIContent(explanation), 800));
            socialResultScroll = GUI.BeginScrollView(new Rect(525, 485, 850, 285), socialResultScroll, new Rect(0, 0, 825, height));
            GUI.Label(new Rect(0, 0, 800, height), explanation, small); GUI.EndScrollView();
            if (socialTraitsStyle == null) socialTraitsStyle = new GUIStyle(small) { fontSize = 16 };
            GUI.Label(new Rect(45, 775, 925, 60), "YOUR CHARACTER\n" + string.Join(" · ", Career.UserPlayer.PersonalityTraits.Select(p => p.Key.Replace('-', ' ') + " " + p.Value)), socialTraitsStyle);
            if (GUI.Button(new Rect(45, 840, 300, 45), "BACK  [ESC]", button)) { socialOpen = false; socialPreview = null; }
            GUI.enabled = socialPreview != null && Time.frameCount > socialReadyFrame;
            if (GUI.Button(new Rect(1020, 810, 355, 60), "CONFIRM CHOICE  [ENTER]", button)) ConfirmSocial();
            GUI.enabled = true;
        }
        private void VerifySocial()
        {
            var original = Career;
            try
            {
                Career = FootballRpg.Content.PrototypeContent.CreateCareer(); OpenSocial();
                foreach (SocialRole role in Enum.GetValues(typeof(SocialRole)))
                {
                    SelectSocialPerson(Career.Social.Relationships.Values.First(p => p.Role == role).PersonId);
                    PreviewSocial(0); var p = socialPreview; ConfirmSocial(); ConfirmSocial();
                    var result = Career.Social.Results.Last();
                    if (result.Preview.Percent != p.Percent || result.Succeeded != (result.Roll < p.Chance)) throw new InvalidOperationException("Social probability mismatch.");
                }
                if (Career.Social.DrawCount != 3 || Career.Social.Results.Count != 3 || Career.Calendar.Minute != 0 ||
                    Career.Social.Relationships.Values.Any(r => r.ProfessionalTrust != 50 || r.Standing != 50)) throw new InvalidOperationException("Social dimensions/commit isolation failed.");
                Debug.Log("FOOTBALL_RPG_SOCIAL_PASSED");
            }
            finally { Career = original; socialOpen = false; socialPerson = null; socialPreview = null; socialError = null; }
        }
    }
}
