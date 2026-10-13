using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FootballRpg.Simulation;
using UnityEngine;

namespace FootballRpg.Content
{
    [Serializable] public sealed class CareerSaveEnvelope
    { public int version; public string contentHash, checksum, payload; }
    public enum SaveStatus { Saved, Loaded, Recovered, Missing, Corrupt, Incompatible, Unavailable, Failed }
    public sealed class SaveResult
    {
        public SaveStatus Status { get; }
        public string Message { get; }
        public CareerState Career { get; }
        public bool Success => Status == SaveStatus.Saved || Status == SaveStatus.Loaded || Status == SaveStatus.Recovered;
        internal SaveResult(SaveStatus status, string message, CareerState career = null) { Status = status; Message = message; Career = career; }
    }
    public sealed class CareerSaveStore
    {
        public const int Version = 1;
        private const long MaxBytes = 128 * 1024 * 1024;
        public string Path { get; }
        public string BackupPath => Path + ".bak";
        private readonly string contentHash;
        private readonly GameDefinitions definitions;
        public CareerSaveStore(string path, string contentHash, GameDefinitions definitions)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(contentHash) || definitions == null) throw new ArgumentException("Save path and content are required.");
            Path = System.IO.Path.GetFullPath(path); this.contentHash = contentHash; this.definitions = definitions;
        }
        public static string Hash(string value)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        public static string Unavailable(CareerState c)
        {
            if (c?.Week.Active != true) return "Start a career before saving.";
            if (c.Calendar.AwaitingPractice) return "Finish or end guided practice before saving.";
            if (c.Week.Match != null && !c.Week.Match.CanSave) return "Pause the match or wait for the next decision before saving.";
            return null;
        }
        public string Encode(CareerState c)
        {
            string unavailable = Unavailable(c); if (unavailable != null) throw new InvalidOperationException(unavailable);
            var envelope = new CareerSaveEnvelope { version = Version, contentHash = contentHash, payload = JsonUtility.ToJson(CareerSnapshotGraph.Capture(c)) };
            envelope.checksum = Hash(envelope.version + ":" + envelope.contentHash + ":" + envelope.payload);
            string json = JsonUtility.ToJson(envelope); if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidOperationException("Career exceeds the supported save size."); return json;
        }
        public SaveResult Decode(string json)
        {
            CareerState restored = null;
            try
            {
                if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) return new SaveResult(SaveStatus.Corrupt, "Save is empty or too large.");
                var envelope = JsonUtility.FromJson<CareerSaveEnvelope>(json);
                if (envelope == null || string.IsNullOrEmpty(envelope.payload) || string.IsNullOrEmpty(envelope.checksum)) return new SaveResult(SaveStatus.Corrupt, "Save is incomplete.");
                if (envelope.version != Version || envelope.contentHash != contentHash) return new SaveResult(SaveStatus.Incompatible, "Save belongs to a different game or content version.");
                if (envelope.checksum != Hash(envelope.version + ":" + envelope.contentHash + ":" + envelope.payload)) return new SaveResult(SaveStatus.Corrupt, "Save integrity check failed.");
                restored = CareerSnapshotGraph.Restore(JsonUtility.FromJson<SnapshotGraph>(envelope.payload));
                Validate(restored); restored.ReconnectAfterLoad();
                return new SaveResult(SaveStatus.Loaded, "Career loaded. The same decision and future outcomes are preserved.", restored);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            { return new SaveResult(SaveStatus.Corrupt, "Save is corrupt or its state schema is incompatible."); }
        }
        private void Validate(CareerState c)
        {
            if (c == null || !c.Week.Active || c.Calendar == null || c.Players == null || c.Fixture.Id != definitions.fixture.id ||
                c.UserPlayer == null || c.Calendar.Minute < 0 || c.Calendar.Minute > c.Calendar.TotalMinutes ||
                !c.Players.Keys.OrderBy(x => x).SequenceEqual(definitions.players.Select(p => p.id).OrderBy(x => x)) ||
                !c.Clubs.Keys.OrderBy(x => x).SequenceEqual(definitions.clubs.Select(p => p.id).OrderBy(x => x))) throw new FormatException("Career references are invalid.");
            foreach (var p in c.Players.Values)
            {
                if (!c.Clubs.ContainsKey(p.ClubId) || !definitions.positions.Any(x => x.id == p.PrimaryPositionId) || p.InjuryDaysRemaining < 0) throw new FormatException("Invalid player.");
                foreach (var a in definitions.footballAttributes) if (!p.FootballAttributes.TryGetValue(a.id, out int value) || value < a.minimum || value > a.maximum) throw new FormatException("Invalid football attributes.");
                foreach (float condition in new[] { p.Confidence, p.Morale, p.Fatigue })
                    if (float.IsNaN(condition) || condition < 0 || condition > 1) throw new FormatException("Invalid condition.");
            }
            if (Unavailable(c) != null || (c.Week.World == null) != (c.Week.Match == null) ||
                (c.Week.Phase == CareerWeekPhase.Match || c.Week.Phase == CareerWeekPhase.Complete) && c.Week.World == null) throw new FormatException("Invalid saved boundary.");
            if (c.Week.World != null && c.Week.World.Players.Any(p => !ReferenceEquals(p.Player, c.Players[p.Player.Id]))) throw new FormatException("Player identity is inconsistent.");
        }
        private SaveResult Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return new SaveResult(SaveStatus.Missing, "No saved career found.");
                if (new FileInfo(path).Length > MaxBytes) return new SaveResult(SaveStatus.Corrupt, "Save is too large.");
                return Decode(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
            { return new SaveResult(SaveStatus.Failed, "Unable to read the save file. Check access and try again."); }
        }
        public SaveResult Load()
        {
            var primary = Read(Path); if (primary.Success) return primary;
            var backup = Read(BackupPath);
            if (backup.Success) return new SaveResult(SaveStatus.Recovered, "The latest save was unavailable. Recovered the previous valid save.", backup.Career);
            return primary;
        }
        public SaveResult Save(CareerState c)
        {
            string unavailable = Unavailable(c); if (unavailable != null) return new SaveResult(SaveStatus.Unavailable, unavailable);
            string temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string json = Encode(c);
                var verify = Decode(json); if (!verify.Success) return new SaveResult(SaveStatus.Failed, "Save verification failed; your previous save was kept."); verify.Career.Week.World?.Dispose();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { byte[] bytes = Encoding.UTF8.GetBytes(json); file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                var existing = Read(Path); bool valid = existing.Success; existing.Career?.Week.World?.Dispose();
                if (File.Exists(Path)) File.Replace(temp, Path, valid ? BackupPath : null);
                else File.Move(temp, Path);
                return new SaveResult(SaveStatus.Saved, "Career saved. The previous valid save is kept as a backup.");
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            { return new SaveResult(SaveStatus.Failed, "Could not save the career; the previous save was kept. Check disk space and file access."); }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
