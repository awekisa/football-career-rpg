using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FootballRpg.Simulation
{
    /// <summary>A frozen choice from a future football resolver, not a formula or a skill gate.</summary>
    public sealed class ActionRequest
    {
        public string RequestId { get; }
        public string PlayerId { get; }
        public string ActionId { get; }
        public string TargetId { get; }
        public float Chance { get; }
        public string SuccessOutcomeId { get; }
        public string FailureOutcomeId { get; }
        public IReadOnlyDictionary<string, string> Context { get; }

        public ActionRequest(string requestId, string playerId, string actionId, string targetId,
            float chance, string successOutcomeId, string failureOutcomeId,
            IDictionary<string, string> context)
        {
            RequestId = Required(requestId);
            PlayerId = Required(playerId);
            ActionId = Required(actionId);
            TargetId = Required(targetId);
            DefinitionValidator.Unit(chance, nameof(chance));
            Chance = chance;
            SuccessOutcomeId = Required(successOutcomeId);
            FailureOutcomeId = Required(failureOutcomeId);
            if (context == null) throw new ArgumentNullException(nameof(context));
            var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in context) snapshot.Add(Required(entry.Key), Required(entry.Value));
            Context = new ReadOnlyDictionary<string, string>(snapshot);
        }

        internal bool SameChoice(ActionRequest other) =>
            RequestId == other.RequestId && PlayerId == other.PlayerId && ActionId == other.ActionId &&
            TargetId == other.TargetId && Chance == other.Chance &&
            SuccessOutcomeId == other.SuccessOutcomeId && FailureOutcomeId == other.FailureOutcomeId &&
            Context.SequenceEqual(other.Context);

        internal static string Required(string value) =>
            !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A stable reference or context value is required.");
    }

    public sealed class ActionDiagnostic
    {
        public ActionRequest Request { get; }
        public ActionResolved Result { get; }
        public long SimulationTick { get; }
        public long SimulationTimeTicks { get; }
        public ulong Seed { get; }
        public ulong RandomStateBefore { get; }
        public ulong RandomStateAfter { get; }

        internal ActionDiagnostic(ActionRequest request, ActionResolved result, long tick, long timeTicks,
            ulong seed, ulong randomStateBefore, ulong randomStateAfter)
        {
            Request = request;
            Result = result;
            SimulationTick = tick;
            SimulationTimeTicks = timeTicks;
            Seed = seed;
            RandomStateBefore = randomStateBefore;
            RandomStateAfter = randomStateAfter;
        }

        // One escaped JSON object per action. Culture-independent, stable context ordering.
        public string ToJsonLine()
        {
            var c = CultureInfo.InvariantCulture;
            return "{\"eventId\":" + Quote(Result.EventId) +
                ",\"requestId\":" + Quote(Request.RequestId) +
                ",\"sequence\":" + Result.Sequence.ToString(c) +
                ",\"tick\":" + SimulationTick.ToString(c) +
                ",\"timeTicks\":" + SimulationTimeTicks.ToString(c) +
                ",\"seed\":" + Quote(Seed.ToString(c)) +
                ",\"randomBefore\":" + Quote(RandomStateBefore.ToString(c)) +
                ",\"randomAfter\":" + Quote(RandomStateAfter.ToString(c)) +
                ",\"playerId\":" + Quote(Result.PlayerId) +
                ",\"actionId\":" + Quote(Result.ActionId) +
                ",\"targetId\":" + Quote(Result.TargetId) +
                ",\"chance\":" + Result.SuccessProbability.ToString("R", c) +
                ",\"roll\":" + Result.Roll.ToString("R", c) +
                ",\"succeeded\":" + (Result.Succeeded ? "true" : "false") +
                ",\"outcomeId\":" + Quote(Result.OutcomeId) +
                ",\"context\":{" + string.Join(",", Request.Context.Select(p => Quote(p.Key) + ":" + Quote(p.Value))) + "}}";
        }

        private static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char ch in value)
            {
                if (ch == '"' || ch == '\\') text.Append('\\').Append(ch);
                else if (ch < 32) text.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(ch);
            }
            return text.Append('"').ToString();
        }
    }
}
