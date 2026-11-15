using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-API-08: the headset reads the same contract examples as the server's pytest (tests/contracts/p3/*.json, checked by server/tests/test_p3_contracts.py). Every example parses, and the
    /// fields of a spoken turn that the server accepts use only the values both sides know (interaction mode menu|voice, session mode practice|assessment|examination, a short request id).
    /// </summary>
    public class P3ContractFixturesTests
    {
        [System.Serializable] private class Body { public string interaction_mode; public string session_mode; public string request_id; public string addressee; public int hints_used; public string verdict_source; }
        [System.Serializable] private class Fixture { public string name; public string route; public string schema; public bool valid; public Body body; }

        private static readonly HashSet<string> InteractionModes = new HashSet<string> { "", "menu", "voice" };
        private static readonly HashSet<string> SessionModes = new HashSet<string> { "", "practice", "assessment", "examination" };
        private static readonly Regex RequestId = new Regex(@"^[A-Za-z0-9._:-]{1,64}$");

        private static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tests", "contracts", "p3"));

        [Test]
        public void EveryExampleParsesAndValidOnesUseKnownValues()
        {
            var files = Directory.GetFiles(Dir, "*.json");
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(5), "the shared examples are missing");
            int valid = 0;
            foreach (var path in files)
            {
                var f = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
                Assert.That(f.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
                Assert.That(f.body, Is.Not.Null, f.name);
                if (!f.valid || f.schema == "SessionReport") continue;
                valid++;
                Assert.That(InteractionModes.Contains(f.body.interaction_mode ?? ""), f.name + ": interaction_mode " + f.body.interaction_mode);
                Assert.That(SessionModes.Contains(f.body.session_mode ?? ""), f.name + ": session_mode " + f.body.session_mode);
                if (!string.IsNullOrEmpty(f.body.request_id)) Assert.That(RequestId.IsMatch(f.body.request_id), f.name + ": request_id");
            }
            Assert.That(valid, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void TheReportExampleCarriesTheReplayHintsAndVerdictSource()
        {
            var f = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Dir, "session_report_replay.json")));
            Assert.That(f.body.verdict_source, Is.EqualTo("server_replay"));
            Assert.That(f.body.hints_used, Is.EqualTo(2));
        }

        [Test]
        public void TheInvalidExamplesAreOnesTheHeadsetWouldNeverSend()
        {
            var bad = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Dir, "voice_turn_bad_request_id.json")));
            Assert.That(bad.valid, Is.False);
            Assert.That(RequestId.IsMatch(bad.body.request_id), Is.False);
            var mode = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Dir, "voice_turn_bad_interaction_mode.json")));
            Assert.That(InteractionModes.Contains(mode.body.interaction_mode), Is.False);
        }
    }
}
