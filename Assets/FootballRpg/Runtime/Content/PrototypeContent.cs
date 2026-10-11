using System;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Content
{
    public static class PrototypeContent
    {
        public const string ResourcePath = "PrototypeFixture";

        public static GameDefinitions Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing prototype content: " + ResourcePath);
            return Parse(asset.text);
        }

        public static GameDefinitions Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Content JSON is empty.");
            var definitions = JsonUtility.FromJson<GameDefinitions>(json);
            DefinitionValidator.Validate(definitions);
            return definitions;
        }

        public static CareerState CreateCareer() => CareerState.Create(Load());
    }
}
