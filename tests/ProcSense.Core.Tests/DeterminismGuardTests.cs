using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// Reads the compiled Core assembly (not the source) and fails if it calls anything that would make a replay differ:
    /// the wall clock, System.Random, random GUIDs, tick counters, per-process hash codes, real waiting, the console, or Unity.
    /// The source scan in tools/ci/repo_checks.py (core-rules) catches the same mistakes early; this one cannot be fooled by formatting.
    /// </summary>
    public class DeterminismGuardTests
    {
        // type -> members that are banned ("*" = the whole type)
        private static readonly Dictionary<string, string[]> Banned = new Dictionary<string, string[]>
        {
            ["System.DateTime"] = new[] { "get_Now", "get_UtcNow", "get_Today" },
            ["System.DateTimeOffset"] = new[] { "get_Now", "get_UtcNow" },
            ["System.Environment"] = new[] { "get_TickCount", "get_TickCount64", "GetEnvironmentVariable", "GetEnvironmentVariables" },
            ["System.Guid"] = new[] { "NewGuid" },
            ["System.Random"] = new[] { "*" },
            ["System.HashCode"] = new[] { "*" },                       // seeded randomly for every process
            ["System.Object"] = new[] { "GetHashCode" },               // what s.GetHashCode() compiles to: randomised per process, use the stable hash
            ["System.Diagnostics.Stopwatch"] = new[] { "*" },
            ["System.Threading.Thread"] = new[] { "Sleep" },
            ["System.Threading.Tasks.Task"] = new[] { "Delay" },
            ["System.Threading.Timer"] = new[] { "*" },
            ["System.Timers.Timer"] = new[] { "*" },
            ["System.Console"] = new[] { "*" },
        };

        /// <summary>Every banned member or type the assembly refers to, as "Type::member", plus any Unity assembly reference.</summary>
        internal static List<string> Violations(string assemblyPath)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            using var stream = System.IO.File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            var md = pe.GetMetadataReader();

            foreach (var handle in md.AssemblyReferences)
            {
                string name = md.GetString(md.GetAssemblyReference(handle).Name);
                if (name.StartsWith("UnityEngine", StringComparison.Ordinal) || name.StartsWith("UnityEditor", StringComparison.Ordinal))
                    found.Add("assembly reference: " + name);
            }

            foreach (var handle in md.TypeReferences)
            {
                string full = FullName(md, md.GetTypeReference(handle));
                if (Banned.TryGetValue(full, out var members) && members.Contains("*")) found.Add(full);
            }

            foreach (var handle in md.MemberReferences)
            {
                var member = md.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference) continue;
                string type = FullName(md, md.GetTypeReference((TypeReferenceHandle)member.Parent));
                string name = md.GetString(member.Name);
                if (Banned.TryGetValue(type, out var members) && members.Contains(name)) found.Add(type + "::" + name);
            }
            return found.ToList();
        }

        private static string FullName(MetadataReader md, TypeReference t)
        {
            string name = md.GetString(t.Name), ns = md.GetString(t.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        [Fact]
        public void Core_calls_nothing_that_breaks_replay()
        {
            var violations = Violations(typeof(CoreInfo).Assembly.Location);

            Assert.True(violations.Count == 0, "Core uses banned APIs: " + string.Join(", ", violations));
        }

        // Proof the guard works: this test assembly deliberately uses banned APIs below, and the scanner must see them.
        [Fact]
        public void The_guard_catches_banned_calls_when_they_exist()
        {
            _ = Decoy.Wallclock();
            _ = Decoy.RandomNumber();
            _ = Decoy.StringHash("x");

            var violations = Violations(Assembly.GetExecutingAssembly().Location);

            Assert.Contains("System.DateTime::get_UtcNow", violations);
            Assert.Contains("System.Random", violations);
            Assert.Contains("System.Object::GetHashCode", violations);
        }

        private static class Decoy
        {
            public static DateTime Wallclock() => DateTime.UtcNow;
            public static int RandomNumber() => new System.Random(1).Next();
            public static int StringHash(string s) => s.GetHashCode();
        }
    }
}
