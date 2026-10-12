using System;
using System.Linq;
using FootballRpg.Content;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed partial class LaunchScreen
    {
        private CharacterDraft creationDraft;
        private GameDefinitions creationContent;
        private CreationRules creationRules;
        private Vector2 footballScroll, personalityScroll;
        private GUIStyle creationLabel, creationField;

        public void BeginCreation()
        {
            creationContent = PrototypeContent.Load();
            creationRules = JsonUtility.FromJson<CreationRules>(Resources.Load<TextAsset>("CharacterCreation").text);
            creationDraft = CharacterCreation.NewDraft(creationContent);
            footballScroll = personalityScroll = Vector2.zero;
        }

        private void UpdateCreation()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { creationDraft = null; return; }
            if (Input.GetKeyDown(KeyCode.F1)) Balance(creationContent.footballAttributes, creationDraft.attributes, creationRules.footballBudget);
            if (Input.GetKeyDown(KeyCode.F2)) Balance(creationContent.personalityTraits, creationDraft.traits, creationRules.personalityBudget);
            if (Input.GetKeyDown(KeyCode.F3)) creationDraft.positionId = Cycle(creationContent.positions.Select(p => p.id).ToArray(), creationDraft.positionId);
            if (Input.GetKeyDown(KeyCode.F4)) creationDraft.appearanceId = Cycle(new[] { "placeholder-1", "placeholder-2", "placeholder-3", "placeholder-4" }, creationDraft.appearanceId);
            if (Input.GetKeyDown(KeyCode.Return))
            {
                try { Career = CharacterCreation.CreateCareer(creationContent, creationDraft, creationRules); creationDraft = null; }
                catch (ArgumentException) { /* The screen explains the validation error. */ }
            }
        }

        private static void Balance(ValueDefinition[] definitions, ValueOverride[] values, int budget)
        {
            CharacterCreation.Remaining(definitions, values, budget);
            foreach (var value in values) value.value = definitions.Single(v => v.id == value.id).minimum;
            int left = budget;
            while (left > 0)
                foreach (var value in values)
                    if (left > 0 && value.value < definitions.Single(v => v.id == value.id).maximum) { value.value++; left--; }
        }

        private void DrawCreation()
        {
            if (creationLabel == null)
            {
                creationLabel = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                creationField = new GUIStyle(GUI.skin.textField) { fontSize = 22 };
            }
            GUI.Label(new Rect(50, 35, 1200, 45), "CREATE YOUR FOOTBALLER", body);
            GUI.Label(new Rect(50, 85, 1250, 40), "Join " + creationContent.clubs.Single(c => c.id == creationContent.fixture.homeClubId).name + " as a young prospect. Your position guides you; it never locks your skills or actions.", small);
            GUI.Label(new Rect(50, 150, 360, 25), "PROFILE", eyebrow);
            GUI.Label(new Rect(50, 195, 360, 25), "Name", creationLabel);
            creationDraft.name = GUI.TextField(new Rect(50, 225, 360, 40), creationDraft.name, 40, creationField);
            creationDraft.age = Slider("Age", creationDraft.age, creationRules.minimumAge, creationRules.maximumAge, 285);
            creationDraft.heightCm = Slider("Height (cm)", (int)creationDraft.heightCm, 120, 230, 355);
            creationDraft.weightKg = Slider("Weight (kg)", (int)creationDraft.weightKg, 35, 160, 425);
            if (GUI.Button(new Rect(50, 505, 360, 38), "Preferred foot: " + creationDraft.preferredFoot, button))
                creationDraft.preferredFoot = Cycle(new[] { "right", "left", "both" }, creationDraft.preferredFoot);
            if (GUI.Button(new Rect(50, 555, 310, 38), "Appearance: " + creationDraft.appearanceId.Replace("placeholder-", "Preset ") + " [F4]", button))
                creationDraft.appearanceId = Cycle(new[] { "placeholder-1", "placeholder-2", "placeholder-3", "placeholder-4" }, creationDraft.appearanceId);
            var tones = new[] { new Color(.9f, .72f, .58f), new Color(.75f, .55f, .38f), new Color(.5f, .32f, .22f), new Color(.28f, .17f, .12f) };
            Fill(new Rect(375, 555, 35, 38), tones[int.Parse(creationDraft.appearanceId.Substring(12)) - 1]);
            var position = creationContent.positions.Single(p => p.id == creationDraft.positionId);
            if (GUI.Button(new Rect(50, 605, 360, 48), "Position: " + position.name + "  [F3]", button))
                creationDraft.positionId = Cycle(creationContent.positions.Select(p => p.id).ToArray(), creationDraft.positionId);
            GUI.Label(new Rect(50, 670, 360, 105), "Taller builds reach higher balls; weight influences dribbling and keeper mobility. Appearance is cosmetic. All values are editable until you join.", creationLabel);
            DrawBudget(450, "FOOTBALL", creationContent.footballAttributes, creationDraft.attributes, creationRules.footballBudget, ref footballScroll, position.recommendedAttributeIds);
            DrawBudget(950, "PERSONALITY", creationContent.personalityTraits, creationDraft.traits, creationRules.personalityBudget, ref personalityScroll, Array.Empty<string>());
            GUI.Label(new Rect(950, 620, 420, 130), "Coach guidance: starred football attributes support your chosen role. You may ignore this advice.\n\nPersonality is separate from temporary confidence, morale and fatigue.", creationLabel);
            string error = "";
            try { CharacterCreation.Validate(creationContent, creationDraft, creationRules); }
            catch (ArgumentException e) { error = e.Message; }
            GUI.Label(new Rect(450, 785, 910, 50), error == "" ? "Ready to join your club. No youth assessment required." : error, creationLabel);
            if (GUI.Button(new Rect(50, 830, 200, 45), "CANCEL", button)) { creationDraft = null; return; }
            GUI.enabled = error == "";
            if (GUI.Button(new Rect(1020, 830, 350, 45), "JOIN CLUB  [ENTER]", button))
            {
                Career = CharacterCreation.CreateCareer(creationContent, creationDraft, creationRules);
                creationDraft = null;
            }
            GUI.enabled = true;
        }

        private int Slider(string label, int value, int min, int max, float y)
        {
            GUI.Label(new Rect(50, y, 360, 25), label + ": " + value, creationLabel);
            return Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(50, y + 35, 360, 20), value, min, max));
        }

        private static string Cycle(string[] options, string current) => options[(Array.IndexOf(options, current) + 1) % options.Length];

        private void DrawBudget(float x, string label, ValueDefinition[] definitions, ValueOverride[] values, int budget, ref Vector2 scroll, string[] recommended)
        {
            int remaining = CharacterCreation.Remaining(definitions, values, budget);
            GUI.Label(new Rect(x, 150, 420, 30), label + "  ·  " + remaining + " / " + budget + " left", eyebrow);
            if (GUI.Button(new Rect(x, 195, 200, 35), label == "FOOTBALL" ? "BALANCE [F1]" : "BALANCE [F2]", button))
                Balance(definitions, values, budget);
            if (GUI.Button(new Rect(x + 210, 195, 210, 35), "CLEAR POINTS", button))
                foreach (var value in values) value.value = definitions.Single(v => v.id == value.id).minimum;
            float height = label == "FOOTBALL" ? 535 : 355;
            scroll = GUI.BeginScrollView(new Rect(x, 245, 440, height), scroll, new Rect(0, 0, 415, definitions.Length * 49));
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i]; var value = values.Single(v => v.id == definition.id); float y = i * 49;
                GUI.Label(new Rect(0, y, 240, 45), (recommended.Contains(definition.id) ? "* " : "") + definition.name, creationLabel);
                GUI.enabled = value.value > definition.minimum;
                if (GUI.Button(new Rect(242, y, 38, 35), "−", button)) value.value--;
                GUI.enabled = true;
                GUI.Label(new Rect(290, y + 5, 45, 30), value.value.ToString(), creationLabel);
                GUI.enabled = value.value < definition.maximum && CharacterCreation.Remaining(definitions, values, budget) > 0;
                if (GUI.Button(new Rect(345, y, 38, 35), "+", button)) value.value++;
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }
    }
}
