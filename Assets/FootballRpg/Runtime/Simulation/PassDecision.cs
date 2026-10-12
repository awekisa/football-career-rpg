using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    /// <summary>One frozen possession opportunity; inspection cannot refresh or reroll its options.</summary>
    public sealed class PassDecision
    {
        public string Id { get; }
        public long Tick { get; }
        public string PlayerId { get; }
        public IReadOnlyList<PassPreview> Options { get; }
        internal PassDecision(string id, long tick, string playerId, IEnumerable<PassPreview> options)
        {
            Id = id; Tick = tick; PlayerId = playerId;
            Options = System.Array.AsReadOnly(options.ToArray());
        }
    }
}
