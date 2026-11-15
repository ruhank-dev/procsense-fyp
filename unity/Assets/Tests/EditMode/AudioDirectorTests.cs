using NUnit.Framework;
using ProcSense.VR.Audio;
using ProcSense.VR.Flow;
using UnityEngine;
using UnityEngine.Audio;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The mixer and the mix rules (P3-SND-01): six groups with exposed volumes, four snapshots, music never during a session, states from MenuFlow, ducking under a voice.</summary>
    public class AudioDirectorTests
    {
        [Test]
        public void The_mixer_has_the_six_groups_their_volumes_and_the_four_snapshots()
        {
            var mixer = Resources.Load<AudioMixer>(AudioDirector.MixerResource);
            Assert.IsNotNull(mixer, "Resources/" + AudioDirector.MixerResource);
            foreach (MixGroup g in System.Enum.GetValues(typeof(MixGroup)))
            {
                Assert.AreEqual(1, System.Array.FindAll(mixer.FindMatchingGroups("Master"), x => x.name == g.ToString()).Length, g.ToString());
                Assert.IsTrue(mixer.GetFloat(g + "Volume", out _), g + "Volume is exposed");
            }
            foreach (var s in new[] { "Menu", "Play", "Paused", "Results" }) Assert.IsNotNull(mixer.FindSnapshot(s), s);
        }

        [Test]
        public void Music_never_plays_during_a_session_and_the_room_is_silent_on_the_menu()
        {
            foreach (var s in new[] { MixState.Play, MixState.Paused })
                Assert.AreEqual(AudioDirector.Silent, AudioDirector.Level(MixGroup.Music, 1f, s, 0f), s.ToString());
            Assert.AreEqual(0f, AudioDirector.Level(MixGroup.Music, 1f, MixState.Menu, 0f));
            Assert.AreEqual(AudioDirector.Silent, AudioDirector.Level(MixGroup.Ambience, 1f, MixState.Menu, 0f));
            Assert.AreEqual(0f, AudioDirector.Level(MixGroup.Ambience, 1f, MixState.Play, 0f));
        }

        [Test]
        public void A_voice_ducks_the_room_but_not_the_voice_itself()
        {
            Assert.AreEqual(AudioDirector.DuckDb, AudioDirector.Level(MixGroup.Ambience, 1f, MixState.Play, 1f), 1e-4);
            Assert.AreEqual(0f, AudioDirector.Level(MixGroup.Voice, 1f, MixState.Play, 1f));
            Assert.AreEqual(AudioDirector.Silent, AudioDirector.Level(MixGroup.Voice, 1f, MixState.Paused, 0f), "nobody talks over the pause menu");
        }

        [Test]
        public void The_volume_setting_maps_to_decibels_and_zero_is_silent()
        {
            Assert.AreEqual(0f, AudioDirector.ToDb(1f), 1e-4);
            Assert.AreEqual(-6.02f, AudioDirector.ToDb(.5f), .01f);
            Assert.AreEqual(AudioDirector.Silent, AudioDirector.ToDb(0f));
            Assert.AreEqual(AudioDirector.Silent, AudioDirector.Level(MixGroup.UI, 0f, MixState.Play, 0f));
        }

        [Test]
        public void Each_menu_phase_maps_to_its_mix_state_and_snapshot()
        {
            Assert.AreEqual(MixState.Menu, AudioDirector.StateFor(MenuPhase.MainMenu, false));
            Assert.AreEqual(MixState.Menu, AudioDirector.StateFor(MenuPhase.Consent, false));
            Assert.AreEqual(MixState.Play, AudioDirector.StateFor(MenuPhase.Playing, false));
            Assert.AreEqual(MixState.Paused, AudioDirector.StateFor(MenuPhase.Playing, true));
            Assert.AreEqual(MixState.Results, AudioDirector.StateFor(MenuPhase.Results, false));
            Assert.AreEqual(MixState.Play, AudioDirector.StateFor(null, false), "no menu flow: the Editor's fixed case plays");
            foreach (MixState m in System.Enum.GetValues(typeof(MixState))) Assert.AreEqual(m.ToString(), AudioDirector.SnapshotFor(m));
        }
    }
}
