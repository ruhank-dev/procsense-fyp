using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// Strict typed view of one JSON object. Every key you read is marked used; Finish() rejects any key nobody read
    /// (so a misspelled or extra field in a content file is an error, not silently ignored).
    /// </summary>
    internal sealed class JsonObj
    {
        private readonly Dictionary<string, object?> _d;
        private readonly string _path;
        private readonly HashSet<string> _used = new HashSet<string>();

        public JsonObj(object? node, string path)
        {
            _d = node as Dictionary<string, object?> ?? throw new ContentException(Where(path) + ": expected an object");
            _path = path;
        }

        public string Str(string key) => Get<string>(key, "text");
        /// <summary>A nested object, or null when the file says null.</summary>
        public JsonObj? ObjOrNull(string key) => Raw(key) == null ? null : new JsonObj(Raw(key), Sub(key));

        public bool Bool(string key) => Get<bool>(key, "true/false");
        public long Int(string key) => Get<long>(key, "a whole number");
        public JsonObj Obj(string key) => new JsonObj(Raw(key), Sub(key));
        public List<object?> Arr(string key) => Get<List<object?>>(key, "a list");

        public double Num(string key)
        {
            object? v = Raw(key);
            if (v is long l) return l;
            if (v is double d) return d;
            throw new ContentException(Where(Sub(key)) + ": expected a number");
        }

        /// <summary>Text restricted to the allowed values.</summary>
        public string Enum(string key, params string[] allowed)
        {
            string v = Str(key);
            if (!allowed.Contains(v)) throw new ContentException(Where(Sub(key)) + ": '" + v + "' is not one of " + string.Join(", ", allowed));
            return v;
        }

        /// <summary>A nested object whose values are all text (optionally matching a pattern, e.g. the refs map).</summary>
        public SortedDictionary<string, string> StringMap(string key, System.Text.RegularExpressions.Regex? valuePattern = null, string expected = "text")
        {
            var map = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var kv in Get<Dictionary<string, object?>>(key, "an object"))
            {
                if (!(kv.Value is string v) || (valuePattern != null && !valuePattern.IsMatch(v)))
                    throw new ContentException(Where(Sub(key)) + "." + kv.Key + ": expected " + expected);
                map[kv.Key] = v;
            }
            return map;
        }

        /// <summary>The value as parsed, whatever its type (used for rule logic and test states).</summary>
        public object? Any(string key) => Raw(key);

        public bool Has(string key) => _d.ContainsKey(key);

        public string Sub(string key) => _path.Length == 0 ? key : _path + "." + key;

        public void Finish()
        {
            foreach (var key in _d.Keys.OrderBy(k => k, System.StringComparer.Ordinal))
                if (!_used.Contains(key)) throw new ContentException(Where(Sub(key)) + ": unknown field");
        }

        private object? Raw(string key)
        {
            if (!_d.TryGetValue(key, out var v)) throw new ContentException(Where(Sub(key)) + ": missing field");
            _used.Add(key);
            return v;
        }

        private T Get<T>(string key, string expected)
        {
            object? v = Raw(key);
            if (v is T t) return t;
            throw new ContentException(Where(Sub(key)) + ": expected " + expected);
        }

        private static string Where(string path) => path.Length == 0 ? "<root>" : path;

        public static string Index(string path, int i) => path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
    }
}
