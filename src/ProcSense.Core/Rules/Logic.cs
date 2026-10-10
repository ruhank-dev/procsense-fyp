using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Rules
{
    /// <summary>A rule met data of the wrong type at run time (for example "text" &lt; 3). This is a mistake in the rule, so it is loud.</summary>
    public sealed class LogicException : Exception
    {
        public LogicException(string message) : base(message) { }
    }

    /// <summary>
    /// A small JsonLogic subset: var, ==, !=, &lt;, &lt;=, &gt;, &gt;=, and, or, !, in, if, missing, some, all, +, -.
    /// A rule is compiled once (operators and argument counts are checked, so a typo is a load error that names the place)
    /// and then evaluated against plain data (dictionaries, lists, numbers, text, true/false, null).
    /// <list type="bullet">
    /// <item>Strict types, no coercion: 1 == "1" is false, true == 1 is false, and an ordering comparison of a number with text is an error.</item>
    /// <item>A missing variable is null. Comparing null with anything is false; arithmetic on null gives null.</item>
    /// <item>Truthy: not null, not false, not 0, not "" and not an empty list.</item>
    /// <item>some / all run their test with each list item as the data (var "" is the item). some and all of an empty list are both false.</item>
    /// </list>
    /// </summary>
    public sealed class Logic
    {
        public const int MaxDepth = 32;

        private readonly Node _root;

        private Logic(Node root, IReadOnlyList<string>? readKeys) { _root = root; ReadKeys = readKeys; }

        /// <summary>
        /// The top-level data keys this rule reads (sorted), or null when it reads keys it cannot name in advance (a computed variable name, or the whole data).
        /// Callers use it to re-evaluate a rule only when one of those keys changed.
        /// </summary>
        public IReadOnlyList<string>? ReadKeys { get; }

        /// <summary>Compiles a rule from parsed JSON. Throws ContentException naming the path for anything not in the subset.</summary>
        public static Logic Compile(object? json, string path = "rule")
        {
            var root = Build(json, path, 1);
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            return new Logic(root, Collect(json, keys) ? keys.ToList() : null);
        }

        /// <summary>Walks the raw rule and gathers the top-level keys it reads. Returns false if any read cannot be named. Inside some/all the test reads the item, not the data, so only the list expression counts.</summary>
        private static bool Collect(object? json, SortedSet<string> keys)
        {
            if (json is List<object?> list) return list.All(x => Collect(x, keys));
            if (!(json is Dictionary<string, object?> map) || map.Count != 1) return true;
            var entry = map.First();
            var args = entry.Value is List<object?> a ? a : new List<object?> { entry.Value };
            switch (entry.Key)
            {
                case "var":
                    if (args.Count == 0 || !(args[0] is string name) || name.Length == 0) return false;
                    keys.Add(name.Split('.')[0]);
                    return args.Skip(1).All(x => Collect(x, keys));
                case "missing":
                    foreach (var arg in args)
                    {
                        var names = arg is List<object?> l ? l : new List<object?> { arg };
                        foreach (var n in names)
                        {
                            if (!(n is string text) || text.Length == 0) return false;
                            keys.Add(text.Split('.')[0]);
                        }
                    }
                    return true;
                case "some":
                case "all":
                    return args.Count == 0 || Collect(args[0], keys);
                default:
                    return args.All(x => Collect(x, keys));
            }
        }

        /// <summary>The raw result (a number, text, true/false, a list or null).</summary>
        public object? Evaluate(object? data) => _root.Eval(data);

        /// <summary>True when the result is truthy.</summary>
        public bool Test(object? data) => Truthy(_root.Eval(data));

        public static bool Truthy(object? v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length != 0;
                case List<object?> l: return l.Count != 0;
                default: return TryNumber(v, out double n) ? n != 0 : true;
            }
        }

        // ---------------------------------------------------------------- compile

        private static readonly HashSet<string> Operators = new HashSet<string>(StringComparer.Ordinal)
        {
            "var", "==", "!=", "<", "<=", ">", ">=", "and", "or", "!", "in", "if", "missing", "some", "all", "+", "-",
        };

        private static Node Build(object? json, string path, int depth)
        {
            if (depth > MaxDepth) throw new ContentException(path + ": rule is nested more than " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels deep");
            switch (json)
            {
                case null: return new Literal(null);
                case bool _: return new Literal(json);
                case string _: return new Literal(json);
                case long l: return new Literal((double)l);
                case double _: return new Literal(json);
                case List<object?> list:
                    return new ListLiteral(list.Select((x, i) => Build(x, JsonObj.Index(path, i), depth + 1)).ToArray());
                case Dictionary<string, object?> map:
                    if (map.Count != 1) throw new ContentException(path + ": an operation must have exactly one operator, found " + map.Count.ToString(CultureInfo.InvariantCulture));
                    var entry = map.First();
                    if (!Operators.Contains(entry.Key))
                        throw new ContentException(path + ": unknown operator '" + entry.Key + "' (allowed: " + string.Join(", ", Operators.OrderBy(o => o, StringComparer.Ordinal)) + ")");
                    var rawArgs = entry.Value is List<object?> a ? a : new List<object?> { entry.Value };
                    var args = rawArgs.Select((x, i) => Build(x, JsonObj.Index(path + "." + entry.Key, i), depth + 1)).ToArray();
                    CheckArity(entry.Key, args.Length, path);
                    return new Operation(entry.Key, args);
                default:
                    throw new ContentException(path + ": unsupported value of type " + json.GetType().Name);
            }
        }

        private static void CheckArity(string op, int n, string path)
        {
            (int min, int max) = op switch
            {
                "var" => (1, 2),
                "==" or "!=" or ">" or ">=" or "in" or "some" or "all" => (2, 2),
                "<" or "<=" => (2, 3),
                "!" => (1, 1),
                "-" => (1, 2),
                _ => (1, int.MaxValue),   // and, or, if, missing, +
            };
            if (n < min || n > max)
                throw new ContentException(path + ": '" + op + "' takes " + (min == max ? min.ToString(CultureInfo.InvariantCulture) : max == int.MaxValue ? "at least " + min.ToString(CultureInfo.InvariantCulture) : min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture)) + " argument(s), found " + n.ToString(CultureInfo.InvariantCulture));
        }

        // ---------------------------------------------------------------- evaluate

        private abstract class Node
        {
            public abstract object? Eval(object? data);
        }

        private sealed class Literal : Node
        {
            private readonly object? _value;
            public Literal(object? value) { _value = value; }
            public override object? Eval(object? data) => _value;
        }

        private sealed class ListLiteral : Node
        {
            private readonly Node[] _items;
            public ListLiteral(Node[] items) { _items = items; }
            public override object? Eval(object? data)
            {
                var result = new List<object?>(_items.Length);
                foreach (var n in _items) result.Add(n.Eval(data));
                return result;
            }
        }

        private sealed class Operation : Node
        {
            private readonly string _op;
            private readonly Node[] _args;

            public Operation(string op, Node[] args) { _op = op; _args = args; }

            public override object? Eval(object? data)
            {
                switch (_op)
                {
                    case "var": return Var(data);
                    case "==": return Same(_args[0].Eval(data), _args[1].Eval(data));
                    case "!=": return !Same(_args[0].Eval(data), _args[1].Eval(data));
                    case "<": return Ordered(data, strict: true, ascending: true);
                    case "<=": return Ordered(data, strict: false, ascending: true);
                    case ">": return Ordered(data, strict: true, ascending: false);
                    case ">=": return Ordered(data, strict: false, ascending: false);
                    case "and": return Chain(data, stopWhenTruthy: false);
                    case "or": return Chain(data, stopWhenTruthy: true);
                    case "!": return !Truthy(_args[0].Eval(data));
                    case "in": return In(_args[0].Eval(data), _args[1].Eval(data));
                    case "if": return If(data);
                    case "missing": return Missing(data);
                    case "some": return Quantified(data, all: false);
                    case "all": return Quantified(data, all: true);
                    case "+": return Add(data);
                    default: return Subtract(data);   // "-"
                }
            }

            private object? Var(object? data)
            {
                object? name = _args[0].Eval(data);
                object? found = name is string s ? Lookup(data, s) : name is double d ? Lookup(data, d.ToString("R", CultureInfo.InvariantCulture)) : throw new LogicException("var needs a name (text), not " + Describe(name));
                return found ?? (_args.Length == 2 ? _args[1].Eval(data) : null);
            }

            private object? If(object? data)
            {
                int n = _args.Length;
                for (int i = 0; i + 1 < n; i += 2)
                    if (Truthy(_args[i].Eval(data))) return _args[i + 1].Eval(data);
                return n % 2 == 1 ? _args[n - 1].Eval(data) : null;
            }

            private object? Missing(object? data)
            {
                var names = new List<string>();
                foreach (var a in _args)
                {
                    object? v = a.Eval(data);
                    if (v is List<object?> list) foreach (var x in list) names.Add(AsName(x));
                    else names.Add(AsName(v));
                }
                var result = new List<object?>();
                foreach (var n in names) if (Lookup(data, n) == null) result.Add(n);
                return result;
            }

            private object? Quantified(object? data, bool all)
            {
                object? source = _args[0].Eval(data);
                if (source == null) return false;
                if (!(source is List<object?> items)) throw new LogicException((all ? "all" : "some") + " needs a list, not " + Describe(source));
                if (items.Count == 0) return false;
                foreach (var item in items)
                {
                    bool ok = Truthy(_args[1].Eval(item));
                    if (all && !ok) return false;
                    if (!all && ok) return true;
                }
                return all;
            }

            /// <summary>"and" / "or": evaluate left to right and return the first operand that decides, else the last operand's value.</summary>
            private object? Chain(object? data, bool stopWhenTruthy)
            {
                object? last = null;
                foreach (var a in _args)
                {
                    last = a.Eval(data);
                    if (Truthy(last) == stopWhenTruthy) return last;
                }
                return last;
            }

            private object? Ordered(object? data, bool strict, bool ascending)
            {
                var values = new object?[_args.Length];
                for (int i = 0; i < values.Length; i++) values[i] = _args[i].Eval(data);
                for (int i = 0; i + 1 < values.Length; i++)
                {
                    int? c = Compare(values[i], values[i + 1]);
                    if (c == null) return false;
                    int signed = ascending ? c.Value : -c.Value;
                    if (strict ? signed >= 0 : signed > 0) return false;
                }
                return true;
            }

            private object? Add(object? data)
            {
                double sum = 0;
                foreach (var a in _args)
                {
                    object? v = a.Eval(data);
                    if (v == null) return null;
                    sum += Number(v, "+");
                }
                return sum;
            }

            private object? Subtract(object? data)
            {
                object? a = _args[0].Eval(data);
                if (a == null) return null;
                if (_args.Length == 1) return -Number(a, "-");
                object? b = _args[1].Eval(data);
                if (b == null) return null;
                return Number(a, "-") - Number(b, "-");
            }
        }

        private static string AsName(object? v) => v as string ?? (v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : throw new LogicException("a variable name must be text, not " + Describe(v)));

        private static double Number(object? v, string op) => TryNumber(v, out double d) ? d : throw new LogicException("'" + op + "' needs numbers, not " + Describe(v));

        /// <summary>Follows a dotted path ("vitals.spo2", "items.0.name") through dictionaries and lists. Returns null when any step is missing.</summary>
        private static object? Lookup(object? data, string name)
        {
            if (name.Length == 0) return Normalise(data);
            object? current = data;
            foreach (var part in name.Split('.'))
            {
                if (current is IReadOnlyDictionary<string, object?> map) { if (!map.TryGetValue(part, out current)) return null; }
                else if (current is List<object?> list && int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index < list.Count) current = list[index];
                else return null;
            }
            return Normalise(current);
        }

        /// <summary>Whole numbers from content (long/int) behave as numbers; everything else must already be a supported type.</summary>
        private static object? Normalise(object? v)
        {
            switch (v)
            {
                case null: return null;
                case int i: return (double)i;
                case long l: return (double)l;
                case float f: return (double)f;
                case double _: return v;
                case bool _: return v;
                case string _: return v;
                case List<object?> _: return v;
                case IReadOnlyDictionary<string, object?> _: return v;
                default: throw new LogicException("data of type " + v.GetType().Name + " is not supported in a rule");
            }
        }

        /// <summary>Numbers read from JSON arrive as long or double; both count as numbers.</summary>
        private static bool TryNumber(object? v, out double number)
        {
            switch (v)
            {
                case double d: number = d; return true;
                case long l: number = l; return true;
                case int i: number = i; return true;
                case float f: number = f; return true;
                default: number = 0; return false;
            }
        }

        internal static bool Same(object? a, object? b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (TryNumber(a, out double x) && TryNumber(b, out double y)) return x == y;
            if (a is bool p && b is bool q) return p == q;
            if (a is string s && b is string t) return string.Equals(s, t, StringComparison.Ordinal);
            if (a is List<object?> la && b is List<object?> lb)
            {
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!Same(la[i], lb[i])) return false;
                return true;
            }
            return false;   // different types are simply not equal: no coercion
        }

        /// <summary>null = "cannot be ordered" (a missing value), otherwise the sign of a - b. Mixed or unordered types are an error.</summary>
        private static int? Compare(object? a, object? b)
        {
            if (a == null || b == null) return null;
            if (TryNumber(a, out double x) && TryNumber(b, out double y)) return x.CompareTo(y);
            if (a is string s && b is string t) return Math.Sign(string.CompareOrdinal(s, t));
            throw new LogicException("cannot order " + Describe(a) + " against " + Describe(b));
        }

        private static bool In(object? needle, object? haystack)
        {
            if (haystack == null) return false;
            if (haystack is List<object?> list) return list.Any(x => Same(needle, x));
            if (haystack is string text)
            {
                if (needle is string part) return text.IndexOf(part, StringComparison.Ordinal) >= 0;
                if (needle == null) return false;
                throw new LogicException("'in' on text needs text to look for, not " + Describe(needle));
            }
            throw new LogicException("'in' needs a list or text to search, not " + Describe(haystack));
        }

        private static string Describe(object? v) => v switch
        {
            null => "nothing (null)",
            bool _ => "true/false",
            double _ or long _ or int _ => "a number",
            string _ => "text",
            List<object?> _ => "a list",
            _ => "an object",
        };
    }
}
