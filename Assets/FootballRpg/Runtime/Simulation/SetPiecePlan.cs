using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    public enum SetPieceDuty { FollowZone, MarkPlayer, AttackSpace, Counterattack }
    [Serializable]
    public sealed class SetPieceDefinition
    {
        public string[] penaltyTakers;
        public string[] freeKickTakers;
        public string[] cornerTakers;
        public SetPieceRoleDefinition[] roles;
    }
    [Serializable]
    public sealed class SetPieceRoleDefinition
    {
        public string positionId;
        public string attackingDuty;
        public string defendingDuty;
    }
    public sealed class SetPiecePlan
    {
        public IReadOnlyList<string> PenaltyTakers { get; }
        public IReadOnlyList<string> FreeKickTakers { get; }
        public IReadOnlyList<string> CornerTakers { get; }
        private readonly Dictionary<string, SetPieceDuty> attack = new Dictionary<string, SetPieceDuty>();
        private readonly Dictionary<string, SetPieceDuty> defend = new Dictionary<string, SetPieceDuty>();
        internal SetPiecePlan(SetPieceDefinition source)
        {
            PenaltyTakers = Array.AsReadOnly((string[])(source?.penaltyTakers ?? Array.Empty<string>()).Clone());
            FreeKickTakers = Array.AsReadOnly((string[])(source?.freeKickTakers ?? Array.Empty<string>()).Clone());
            CornerTakers = Array.AsReadOnly((string[])(source?.cornerTakers ?? Array.Empty<string>()).Clone());
            foreach (var role in source?.roles ?? Array.Empty<SetPieceRoleDefinition>())
            { attack.Add(role.positionId, (SetPieceDuty)Enum.Parse(typeof(SetPieceDuty), role.attackingDuty)); defend.Add(role.positionId, (SetPieceDuty)Enum.Parse(typeof(SetPieceDuty), role.defendingDuty)); }
        }
        public SetPieceDuty Duty(string role, bool attacking)
        {
            if ((attacking ? attack : defend).TryGetValue(role, out var duty)) return duty;
            if (role == "gk") return SetPieceDuty.FollowZone;
            if (attacking) return role == "st" || role == "cb" || role == "am" ? SetPieceDuty.AttackSpace : SetPieceDuty.FollowZone;
            return role == "st" ? SetPieceDuty.Counterattack : role == "cb" ? SetPieceDuty.MarkPlayer : SetPieceDuty.FollowZone;
        }
        public IReadOnlyList<string> Takers(RestartKind kind) => kind == RestartKind.Penalty ? PenaltyTakers : kind == RestartKind.Corner ? CornerTakers : FreeKickTakers;
    }
    public sealed class SetPieceAssignment
    {
        public string PlayerId { get; }
        public string CoachId { get; }
        public SetPieceDuty Duty { get; }
        public PitchPoint Zone { get; }
        public string MarkId { get; }
        internal SetPieceAssignment(string player, string coach, SetPieceDuty duty, PitchPoint zone, string mark)
        { PlayerId = player; CoachId = coach; Duty = duty; Zone = zone; MarkId = mark; }
    }
    public sealed class SetPieceRecord
    {
        public string RestartId { get; }
        public string CoachId { get; }
        public string PlayerId { get; }
        public string Kind { get; }
        public string Choice { get; }
        public bool Deviated { get; }
        public string ActionEventId { get; }
        public bool? Succeeded { get; }
        public long Tick { get; }
        internal SetPieceRecord(string restart, string coach, string player, string kind, string choice, bool deviated, string action, bool? success, long tick)
        { RestartId = restart; CoachId = coach; PlayerId = player; Kind = kind; Choice = choice; Deviated = deviated; ActionEventId = action; Succeeded = success; Tick = tick; }
    }
}
