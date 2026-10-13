using System;
using System.Collections.Generic;
using System.Linq;

namespace FootballRpg.Simulation
{
    [Serializable] public sealed class ClubVenueDefinition
    {
        public string name;
        public string[] rows;
        public float cellSize = 2;
        public ClubDestinationDefinition[] destinations;
    }
    [Serializable] public sealed class ClubDestinationDefinition { public string id, name; public int x, z; }
    public sealed class ClubDestination
    {
        public string Id { get; }
        public string Name { get; }
        public int X { get; }
        public int Z { get; }
        internal ClubDestination(ClubDestinationDefinition d) { Id = d.id; Name = d.name; X = d.x; Z = d.z; }
    }
    /// <summary>Immutable authored walk grid. All path segments join adjacent open cell centres.</summary>
    public sealed class ClubVenue
    {
        private readonly string[] rows;
        public string Name { get; }
        public double CellSize { get; }
        public int Width => rows[0].Length;
        public int Height => rows.Length;
        public IReadOnlyList<ClubDestination> Destinations { get; }
        public ClubVenue(ClubVenueDefinition d)
        {
            string[] ids = { "arrival", "coach", "teammates", "agent", "media", "training" };
            if (d == null || string.IsNullOrWhiteSpace(d.name) || d.rows == null || d.rows.Length < 3 || d.rows.Length > 100 ||
                d.rows.Any(r => r == null || r.Length < 3 || r.Length > 100 || r.Length != d.rows[0].Length || r.Any(c => c != '.' && c != '#' && c != 'x')) ||
                float.IsNaN(d.cellSize) || float.IsInfinity(d.cellSize) || d.cellSize < .5 || d.cellSize > 10 || d.destinations == null ||
                d.destinations.Length != ids.Length || d.destinations.Any(p => p == null || !ids.Contains(p.id) || string.IsNullOrWhiteSpace(p.name)) ||
                d.destinations.Select(p => p.id).Distinct().Count() != ids.Length || d.destinations.Select(p => (p.x, p.z)).Distinct().Count() != ids.Length)
                throw new ArgumentException("Invalid club venue definition.");
            rows = (string[])d.rows.Clone(); Name = d.name; CellSize = d.cellSize;
            Destinations = Array.AsReadOnly(d.destinations.Select(p => new ClubDestination(p)).ToArray());
            if (Destinations.Any(p => !Walkable(p.X, p.Z))) throw new ArgumentException("Club destinations must be on open ground.");
            var spawn = Destinations.Single(p => p.Id == "arrival");
            if (Destinations.Any(p => Path(spawn.X, spawn.Z, p.X, p.Z) == null)) throw new ArgumentException("All club destinations must be reachable from arrival.");
        }
        public bool IsWall(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height && rows[z][x] == '#';
        public bool Walkable(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height && rows[z][x] == '.';
        public PitchPoint Point(int x, int z) => new PitchPoint((x - (Width - 1) / 2d) * CellSize, (z - (Height - 1) / 2d) * CellSize);
        public PitchPoint Point(ClubDestination d) => Point(d.X, d.Z);
        public bool Cell(PitchPoint point, out int x, out int z)
        {
            x = z = -1;
            if (!PassActor.Finite(point) || Math.Abs(point.X) > Width * CellSize / 2 || Math.Abs(point.Z) > Height * CellSize / 2) return false;
            x = (int)Math.Round(point.X / CellSize + (Width - 1) / 2d, MidpointRounding.AwayFromZero);
            z = (int)Math.Round(point.Z / CellSize + (Height - 1) / 2d, MidpointRounding.AwayFromZero);
            return Walkable(x, z);
        }
        public IReadOnlyList<PitchPoint> Path(int startX, int startZ, int endX, int endZ)
        {
            if (!Walkable(startX, startZ) || !Walkable(endX, endZ)) return null;
            int start = startZ * Width + startX, end = endZ * Width + endX;
            var previous = new Dictionary<int, int> { [start] = -1 }; var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0 && !previous.ContainsKey(end))
            {
                int cell = queue.Dequeue(), x = cell % Width, z = cell / Width;
                foreach (var neighbor in new[] { (x + 1, z), (x, z + 1), (x - 1, z), (x, z - 1) })
                {
                    int next = neighbor.Item2 * Width + neighbor.Item1;
                    if (!Walkable(neighbor.Item1, neighbor.Item2) || previous.ContainsKey(next)) continue;
                    previous[next] = cell; queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(end)) return null;
            var result = new List<PitchPoint>();
            for (int at = end; at != -1; at = previous[at]) result.Add(Point(at % Width, at / Width));
            result.Reverse(); return result.AsReadOnly();
        }
    }
    public enum ClubInteractionKind { Calendar, TrainingGuidance, Squad, Activity }
    public sealed class ClubInteraction
    {
        public string Title { get; }
        public string Description { get; }
        public bool Available { get; }
        public ClubInteractionKind Kind { get; }
        public CalendarChoice Choice { get; }
        internal ClubInteraction(string title, string description, bool available, ClubInteractionKind kind, CalendarChoice choice = null)
        { Title = title; Description = description; Available = available; Kind = kind; Choice = choice; }
    }
    /// <summary>Walking is presentation time only. Interaction previews delegate all time costs to CareerCalendar.</summary>
    public sealed class ClubExploration
    {
        public CareerState Career { get; }
        public ClubVenue Venue { get; }
        public PitchPoint Position { get; private set; }
        public ClubDestination Destination { get; private set; }
        public string Message { get; private set; }
        private IReadOnlyList<PitchPoint> path = Array.Empty<PitchPoint>();
        private int waypoint;
        public bool Moving => waypoint < path.Count;
        public bool AtDestination => !Moving && Destination != null && Position.Distance(Venue.Point(Destination)) < .001;
        public IReadOnlyList<PitchPoint> Path => path;
        public ClubExploration(CareerState career, ClubVenue venue)
        {
            Career = career ?? throw new ArgumentNullException(nameof(career)); Venue = venue ?? throw new ArgumentNullException(nameof(venue));
            Destination = venue.Destinations.Single(d => d.Id == "arrival"); Position = venue.Point(Destination); Message = "Welcome to the club. Walking costs no calendar time.";
        }
        public bool WalkTo(string destinationId)
        {
            var destination = Venue.Destinations.SingleOrDefault(d => d.Id == destinationId);
            if (destination == null) return Reject("Unknown destination.");
            return WalkTo(Venue.Point(destination));
        }
        public bool WalkTo(PitchPoint target)
        {
            if (!Venue.Cell(target, out int x, out int z) || !Venue.Cell(Position, out int startX, out int startZ)) return Reject("Unreachable: choose open ground or a marked destination.");
            var route = Venue.Path(startX, startZ, x, z);
            if (route == null) return Reject("Unreachable: there is no open route to that point.");
            path = route; waypoint = 0; Destination = Venue.Destinations.SingleOrDefault(d => d.X == x && d.Z == z);
            Message = "Walking to " + (Destination?.Name ?? "selected ground") + ". Calendar time is unchanged.";
            Advance(0); return true;
        }
        private bool Reject(string reason) { path = Array.Empty<PitchPoint>(); waypoint = 0; Destination = null; Message = reason; return false; }
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Moving) return;
            double distance = seconds * 4;
            while (Moving)
            {
                double remaining = Position.Distance(path[waypoint]);
                if (remaining > distance + .000001) { Position = Position.MoveTowards(path[waypoint], distance); break; }
                Position = path[waypoint++]; distance = Math.Max(0, distance - remaining);
            }
            if (!Moving) Message = Destination == null ? "Choose a destination or another point to walk to." : "Arrived at " + Destination.Name + ". Inspect the interaction before committing.";
        }
        public ClubInteraction Inspect(string id)
        {
            var d = Venue.Destinations.SingleOrDefault(p => p.Id == id) ?? throw new ArgumentException("Unknown club destination.");
            var calendar = Career.Calendar;
            if (calendar.AwaitingPractice) return Unavailable("Finish the current guided practice before starting another activity.");
            if (calendar.Finished) return Unavailable("The prototype week is complete. You can still explore the club.");
            var current = calendar.CurrentCommitments;
            bool squadBusy = current.Any(c => c.Activity.Training || c.Activity.Id == "travel" || c.Activity.Id == "match");
            bool night = calendar.Minute / calendar.MinutesPerSlot % calendar.Slots.Count == calendar.Slots.Count - 1;
            if (d.Id == "arrival")
            {
                var travel = current.FirstOrDefault(c => c.Activity.Id == "travel");
                return travel == null ? new ClubInteraction("Review your week", "Your car is parked here. Check commitments, rest or plan the next activity. Opening the calendar costs no time.", true, ClubInteractionKind.Calendar) : Attend(travel);
            }
            if (d.Id == "coach") return squadBusy || night ? Unavailable(squadBusy ? "The coach is with the squad. Return during a free daytime slot, or go to training." : "The coach's office is closed for the night.") :
                new ClubInteraction("Discuss training direction", "The coach can explain development options for your role. Advice and inspection cost no time; attending training uses its scheduled slot.", true, ClubInteractionKind.TrainingGuidance);
            if (d.Id == "teammates") return squadBusy || night ? Unavailable(squadBusy ? "The squad is at its scheduled activity. The teammate area is empty." : "The squad has gone home for the night.") :
                new ClubInteraction("Inspect your teammates", "Meet the squad and inspect positions and skills. Friendship and rivalry conversations arrive in the next club-life components.", true, ClubInteractionKind.Squad);
            string activity = d.Id == "agent" ? "agent" : d.Id == "media" ? "media" : "training";
            var appointment = current.FirstOrDefault(c => c.Activity.Id == activity);
            if (appointment != null)
            {
                if (d.Id == "training" && Career.UserPlayer.InjuryDaysRemaining > 0)
                    return new ClubInteraction("Review training while injured", "Physical practice is unavailable while injured. You can inspect the schedule and record simulated attendance with zero physical XP, or choose recovery instead.", true, ClubInteractionKind.Calendar);
                return Attend(appointment);
            }
            if (d.Id == "training" && Career.UserPlayer.InjuryDaysRemaining == 0)
            {
                try
                {
                    var extra = calendar.Preview(CalendarChoiceKind.ExtraPractice, "extra-practice");
                    return new ClubInteraction("Review extra practice", "The next free slot is available for extra practice. Choose simulated or guided training in the calendar before confirming.", true, ClubInteractionKind.Activity, extra);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { }
            }
            var next = calendar.Commitments.FirstOrDefault(c => c.Activity.Id == activity && c.EndMinute > calendar.Minute && calendar.ResultFor(c.Id) == null);
            return Unavailable((d.Id == "media" ? "No media appointment now. A small club does not have constant media events." : d.Id == "agent" ? "Your agent attends by appointment." : Career.UserPlayer.InjuryDaysRemaining > 0 ? "Recover from injury before extra physical practice." : "No training or eligible extra-practice slot now.") +
                (next == null ? " No further appointment this week." : " Next: " + calendar.TimeLabel(next.StartMinute) + "."));
        }
        private ClubInteraction Attend(CalendarCommitment c) => new ClubInteraction("Review " + c.Activity.Name,
            "Available now. Inspect the time cost and consequences in the calendar; arrival does not commit attendance.", true, ClubInteractionKind.Activity,
            Career.Calendar.Preview(CalendarChoiceKind.Attend, c.Id));
        private static ClubInteraction Unavailable(string why) => new ClubInteraction("Unavailable", why, false, ClubInteractionKind.Calendar);
        public ClubInteraction Interact()
        {
            if (!AtDestination) throw new InvalidOperationException("Walk to the destination before interacting.");
            var interaction = Inspect(Destination.Id);
            if (!interaction.Available) throw new InvalidOperationException(interaction.Description);
            return interaction;
        }
    }
}
