namespace FootballRpg.Simulation
{
    // SplitMix64: explicit arithmetic keeps replays independent of System.Random/runtime versions.
    // This is simulation randomness, not a cryptographic generator.
    internal sealed class SeededRandom
    {
        public ulong State { get; private set; }
        public SeededRandom(ulong seed) => State = seed;

        public float NextUnitFloat()
        {
            ulong value;
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                value = State;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
            }
            // 24 bits fit exactly in a float, including zero but never one.
            return (value >> 40) * (1f / 16777216f);
        }
    }
}
