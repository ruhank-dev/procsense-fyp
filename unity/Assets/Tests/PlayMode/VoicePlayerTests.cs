using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Replay;
using ProcSense.VR.Audio;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Voice playback and subtitles (p0-voice-audio-s03): every catalogue line VAR-S1-A's correct path calls for plays with its subtitle, one at a time; a line whose audio file is missing is still shown as a
    /// subtitle (with a warning); an urgent staff line goes to the front of the queue but the patient finishes the sentence.
    /// </summary>
    public class VoicePlayerTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>Half a second of silence for every line except the ones listed, which have no audio.</summary>
        private sealed class FakeClips : IVoiceClips
        {
            private readonly HashSet<string> _without;
            public FakeClips(params string[] without) { _without = new HashSet<string>(without); }
            public AudioClip Find(string id) => _without.Contains(id) ? null : AudioClip.Create(id, 22050, 1, 44100, false);
        }

        private VoicePlayer Player(IVoiceClips clips)
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            var go = new GameObject("host");
            _made.Add(go);
            _host = go.AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            var p = new GameObject("voice");
            _made.Add(p);
            p.SetActive(false);
            var player = p.AddComponent<VoicePlayer>();
            player.Clips = clips;
            p.SetActive(true);
            return player;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Every_line_of_the_correct_path_plays_with_its_subtitle_and_a_missing_file_still_shows_the_subtitle()
        {
            const string removed = "PAT-VAR-S1-A-ALLERGIES";
            var player = Player(new FakeClips(removed));
            LogAssert.Expect(LogType.Warning, new Regex("voice line " + removed + " has no audio: subtitle only"));
            var subtitles = new Dictionary<string, string>();
            player.Started += l => subtitles[l.Id] = player.Subtitles.Text;

            foreach (var step in ActionScript.Parse(File.ReadAllText(Path.Combine(Repo, "tests", "golden", "VAR-S1-A_correct.actions.json"))).Where(s => s.T < 200))
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                _host.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, step.Action.Params.ToDictionary(kv => kv.Key, kv => kv.Value)));
                for (int i = 0; i < 40 && (player.Current != null || player.Queue.Count > 0); i++) player.Tick(0.5f);
            }

            var caseLines = player.Catalog.Lines.Where(l => l.Case == "VAR-S1-A").ToList();
            Assert.AreEqual(16, caseLines.Count);
            CollectionAssert.IsSubsetOf(caseLines.Select(l => l.Id).ToList(), player.Played.ToList(), "every line of the case was said");
            foreach (var l in caseLines) Assert.AreEqual("Patient: " + l.Text, subtitles[l.Id], "the subtitle names the speaker and shows the words");
            CollectionAssert.AreEqual(new[] { removed }, player.Missing.ToList(), "the missing file is shown as a subtitle only");
        }

        [Test]
        public void An_urgent_staff_line_goes_first_but_the_patient_finishes_the_sentence()
        {
            var player = Player(new FakeClips());
            var patient = new VoiceLine { Id = "P", Speaker = "patient", Text = "My husband will take me home." };
            var nurse = new VoiceLine { Id = "N", Speaker = "nurse", Text = "Shall I get the observations?" };
            var lab = new VoiceLine { Id = "L", Speaker = "laboratory", Text = "Critical result." };
            player.Say(patient);
            player.Say(nurse);
            player.Say(lab);
            Assert.AreEqual("P", player.Current.Id, "the patient is not cut off");
            CollectionAssert.AreEqual(new[] { "L", "N" }, player.Queue.Select(q => q.Id).ToList(), "the urgent line is next");

            player.Tick(1f);                                   // the patient's half-second clip has finished
            Assert.AreEqual("L", player.Current.Id);
            player.Tick(1f);
            Assert.AreEqual("N", player.Current.Id);
            player.Say(new VoiceLine { Id = "L2", Speaker = "laboratory", Text = "Another critical result." });
            Assert.AreEqual("L2", player.Current.Id, "an urgent line cuts a non-urgent staff line short");
            Assert.GreaterOrEqual(SubtitleView.MinSeconds, 2f);
        }
    }
}
