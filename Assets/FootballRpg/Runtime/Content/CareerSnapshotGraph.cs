using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using FootballRpg.Simulation;

namespace FootballRpg.Content
{
    [Serializable] public sealed class SnapshotGraph { public int root; public SnapshotNode[] nodes; }
    [Serializable] public sealed class SnapshotNode { public string type, scalar; public string[] fields; public int[] references; }

    // Versioned, closed-type graph snapshot. No assembly names, executable callbacks, Unity objects or BinaryFormatter.
    // Identity is retained for canonical players, immutable collection views and exactly-once ledgers.
    internal static class CareerSnapshotGraph
    {
        private const int MaxNodes = 1000000;
        private static readonly Dictionary<string, Type> simple = new Dictionary<string, Type> {
            ["string"] = typeof(string), ["bool"] = typeof(bool), ["int"] = typeof(int), ["long"] = typeof(long),
            ["ulong"] = typeof(ulong), ["float"] = typeof(float), ["double"] = typeof(double), ["time"] = typeof(TimeSpan)
        };
        private static readonly Dictionary<string, Type> generic = new Dictionary<string, Type> {
            ["list"] = typeof(List<>), ["map"] = typeof(Dictionary<,>), ["sorted"] = typeof(SortedDictionary<,>),
            ["set"] = typeof(HashSet<>), ["view"] = typeof(ReadOnlyCollection<>), ["mapview"] = typeof(ReadOnlyDictionary<,>),
            ["ilist"] = typeof(IReadOnlyList<>), ["imap"] = typeof(IReadOnlyDictionary<,>), ["nullable"] = typeof(Nullable<>),
            ["pair"] = typeof(ValueTuple<,>), ["triple"] = typeof(ValueTuple<,,>), ["quad"] = typeof(ValueTuple<,,,>)
        };
        private static readonly Dictionary<string, Type> models = typeof(CareerState).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CareerState).Namespace && !t.IsGenericType && !t.IsAbstract && !t.IsDefined(typeof(CompilerGeneratedAttribute), false) && !typeof(Delegate).IsAssignableFrom(t))
            .ToDictionary(t => "model:" + t.FullName, t => t, StringComparer.Ordinal);
        private sealed class Identity : IEqualityComparer<object>
        { public new bool Equals(object a, object b) => ReferenceEquals(a, b); public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value); }
        private static string TypeKey(Type t)
        {
            foreach (var p in simple) if (p.Value == t) return p.Key;
            if (t.IsArray && t.GetArrayRank() == 1) return "array[" + TypeKey(t.GetElementType()) + "]";
            if (t.IsGenericType)
                foreach (var p in generic) if (p.Value == t.GetGenericTypeDefinition()) return p.Key + "[" + string.Join(",", t.GetGenericArguments().Select(TypeKey)) + "]";
            if (models.TryGetValue("model:" + t.FullName, out var m) && m == t) return "model:" + t.FullName;
            throw new InvalidOperationException("Unsupported save state type: " + t.Name);
        }
        private static Type ParseType(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 1024) throw new FormatException("Invalid state type.");
            int at = 0; var result = ParseType(key, ref at, 0); if (at != key.Length) throw new FormatException("Invalid state type suffix."); return result;
        }
        private static Type ParseType(string key, ref int at, int depth)
        {
            if (depth > 12) throw new FormatException("State type is too deep.");
            int begin = at; while (at < key.Length && key[at] != '[' && key[at] != ',' && key[at] != ']') at++;
            string name = key.Substring(begin, at - begin);
            if (at == key.Length || key[at] != '[')
            { if (simple.TryGetValue(name, out var s)) return s; if (models.TryGetValue(name, out var m)) return m; throw new FormatException("Unknown state type."); }
            at++; var args = new List<Type>();
            while (true)
            {
                args.Add(ParseType(key, ref at, depth + 1)); if (at >= key.Length) throw new FormatException("Unclosed state type.");
                char separator = key[at++]; if (separator == ']') break; if (separator != ',' || args.Count >= 4) throw new FormatException("Invalid type arguments.");
            }
            if (name == "array" && args.Count == 1) return args[0].MakeArrayType();
            if (!generic.TryGetValue(name, out var g) || g.GetGenericArguments().Length != args.Count) throw new FormatException("Unknown collection type.");
            return g.MakeGenericType(args.ToArray());
        }
        private static FieldInfo[] Fields(Type t)
        {
            var fields = new List<FieldInfo>();
            for (var current = t; current != null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
                fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType)));
            return fields.OrderBy(f => FieldKey(f), StringComparer.Ordinal).ToArray();
        }
        private static string FieldKey(FieldInfo f) => f.DeclaringType.Name + ":" + f.Name;
        private static string Kind(Type t) => t.IsArray ? "array" : t.IsGenericType ? generic.Single(p => p.Value == t.GetGenericTypeDefinition()).Key : "object";
        private static bool Scalar(Type t) => t.IsEnum || simple.Values.Contains(t);
        private static object Backing(object value, string kind) => value.GetType().GetProperty(kind == "view" ? "Items" : "Dictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        internal static SnapshotGraph Capture(CareerState career)
        {
            var scalars = new Dictionary<(Type, string), int>();
            var nodes = new List<SnapshotNode>(); var ids = new Dictionary<object, int>(new Identity());
            int Add(object value)
            {
                if (value == null) return 0;
                var scalarType = value.GetType();
                // Mono can round a short "R" double to a neighbour on parse; retain all 17 digits.
                string scalarValue = Scalar(scalarType) ? scalarType == typeof(TimeSpan) ? ((TimeSpan)value).Ticks.ToString(CultureInfo.InvariantCulture) :
                    scalarType.IsEnum ? Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture) : scalarType == typeof(float) ? ((float)value).ToString("R", CultureInfo.InvariantCulture) :
                    scalarType == typeof(double) ? ((double)value).ToString("G17", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) : null;
                if (scalarValue != null && scalars.TryGetValue((scalarType, scalarValue), out int scalarId)) return scalarId;
                if (ids.TryGetValue(value, out int existing)) return existing;
                if (nodes.Count >= MaxNodes) throw new InvalidOperationException("Career is too large to save.");
                int id = nodes.Count + 1; ids.Add(value, id); var t = value.GetType(); var node = new SnapshotNode { type = TypeKey(t) }; nodes.Add(node);
                if (Scalar(t)) { node.scalar = scalarValue; scalars.Add((t, scalarValue), id); }
                else
                {
                    string kind = Kind(t);
                    if (kind == "view" || kind == "mapview") node.references = new[] { Add(Backing(value, kind)) };
                    else if (kind == "map" || kind == "sorted")
                    {
                        var refs = new List<int>(); foreach (DictionaryEntry entry in (IDictionary)value) { refs.Add(Add(entry.Key)); refs.Add(Add(entry.Value)); } node.references = refs.ToArray();
                    }
                    else if (kind == "array" || kind == "list" || kind == "set") node.references = ((IEnumerable)value).Cast<object>().Select(Add).ToArray();
                    else { var fields = Fields(t); node.fields = fields.Select(FieldKey).ToArray(); node.references = fields.Select(f => Add(f.GetValue(value))).ToArray(); }
                }
                return id;
            }
            int root = Add(career); return new SnapshotGraph { root = root, nodes = nodes.ToArray() };
        }
        internal static CareerState Restore(SnapshotGraph graph)
        {
            if (graph?.nodes == null || graph.nodes.Length == 0 || graph.nodes.Length > MaxNodes || graph.root < 1 || graph.root > graph.nodes.Length) throw new FormatException("Invalid state graph.");
            var types = graph.nodes.Select(n => n == null ? throw new FormatException("Missing state node.") : ParseType(n.type)).ToArray();
            var objects = new object[types.Length]; var progress = new byte[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                var n = graph.nodes[i]; var t = types[i];
                if (n.references?.Any(r => r < 0 || r > types.Length) == true) throw new FormatException("Invalid state reference.");
                if (Scalar(t))
                {
                    if (n.scalar == null) throw new FormatException("Missing scalar.");
                    objects[i] = t == typeof(TimeSpan) ? TimeSpan.FromTicks(long.Parse(n.scalar, CultureInfo.InvariantCulture)) :
                        t.IsEnum ? Enum.ToObject(t, long.Parse(n.scalar, CultureInfo.InvariantCulture)) : Convert.ChangeType(n.scalar, t, CultureInfo.InvariantCulture);
                    if (t.IsEnum && !Enum.IsDefined(t, objects[i])) throw new FormatException("Unknown enum value.");
                }
                else
                {
                    if (n.references == null) throw new FormatException("Missing references.");
                    string kind = Kind(t);
                    if (kind == "array") objects[i] = Array.CreateInstance(t.GetElementType(), n.references.Length);
                    else if (kind == "view" || kind == "mapview") { if (n.references.Length != 1 || n.references[0] == 0) throw new FormatException("Invalid collection view."); }
                    else if (kind == "list" || kind == "map" || kind == "set" || kind == "sorted")
                        objects[i] = kind == "sorted" && t.GetGenericArguments()[0] == typeof(string) ? Activator.CreateInstance(t, StringComparer.Ordinal) : Activator.CreateInstance(t);
                    else
                    {
                        var fields = Fields(t);
                        if (n.fields == null || n.fields.Length != n.references.Length || !fields.Select(FieldKey).SequenceEqual(n.fields)) throw new FormatException("Save model schema is incompatible.");
                        objects[i] = FormatterServices.GetUninitializedObject(t);
                    }
                }
            }
            // Construct read-only views around the exact backing collection, before filling any graph cycles.
            for (int i = 0; i < types.Length; i++)
                if (objects[i] == null)
                {
                    var backing = objects[graph.nodes[i].references[0] - 1] ?? throw new FormatException("Invalid view backing.");
                    objects[i] = Activator.CreateInstance(types[i], backing);
                }
            object Fill(int id, int depth)
            {
                if (id == 0) return null;
                if (depth > 256) throw new FormatException("State graph is too deep.");
                int i = id - 1; if (progress[i] != 0) return objects[i]; progress[i] = 1;
                var n = graph.nodes[i]; var t = types[i]; var value = objects[i];
                if (!Scalar(t))
                {
                    string kind = Kind(t);
                    if (kind == "view" || kind == "mapview") Fill(n.references[0], depth + 1);
                    else if (kind == "array") { for (int x = 0; x < n.references.Length; x++) ((Array)value).SetValue(Fill(n.references[x], depth + 1), x); }
                    else if (kind == "list") foreach (int r in n.references) ((IList)value).Add(Fill(r, depth + 1));
                    else if (kind == "map" || kind == "sorted")
                    {
                        if (n.references.Length % 2 != 0) throw new FormatException("Invalid map entries.");
                        for (int x = 0; x < n.references.Length; x += 2) ((IDictionary)value).Add(Fill(n.references[x], depth + 1), Fill(n.references[x + 1], depth + 1));
                    }
                    else if (kind == "set") foreach (int r in n.references) t.GetMethod("Add").Invoke(value, new[] { Fill(r, depth + 1) });
                    else { var fields = Fields(t); for (int x = 0; x < fields.Length; x++) fields[x].SetValue(value, Fill(n.references[x], depth + 1)); }
                }
                progress[i] = 2; return value;
            }
            var result = Fill(graph.root, 0) as CareerState ?? throw new FormatException("Missing career root.");
            if (progress.Any(p => p != 2)) throw new FormatException("Unreachable state nodes.");
            return result;
        }
    }
}
