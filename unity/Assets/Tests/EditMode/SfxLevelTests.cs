using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-SND-11: the rendered sound list keeps a sane mix: nothing clips, nothing is silent, and the UI sounds sit clearly below the equipment and foley sounds (the voice is set in the mixer, measured in the headset).</summary>
    public sealed class SfxLevelTests
    {
        private static readonly string Folder = Path.Combine(Application.dataPath, "Audio/Sfx/Resources/Sfx");

        /// <summary>Largest sample of a 16-bit PCM wav, 0 to 1 (read from the file: compressed clips cannot be sampled in the Editor).</summary>
        private static float Peak(string file)
        {
            var b = File.ReadAllBytes(file);
            Assert.AreEqual(16, BitConverter.ToInt16(b, 34), Path.GetFileName(file) + " must be 16-bit PCM");
            int top = 0;
            for (int i = 44; i + 1 < b.Length; i += 2) top = Math.Max(top, Math.Abs((int)BitConverter.ToInt16(b, i)));
            return top / 32768f;
        }

        private static string[] Wavs() => Directory.GetFiles(Folder, "*.wav");

        [Test]
        public void EverySfxIsAudibleAndNeverClips()
        {
            Assert.Greater(Wavs().Length, 40, "the sound list should be present");
            foreach (var f in Wavs())
            {
                float p = Peak(f);
                Assert.Less(p, .98f, Path.GetFileName(f) + " clips");
                Assert.Greater(p, .05f, Path.GetFileName(f) + " is silent");
            }
        }

        [Test]
        public void UiSoundsSitBelowEquipmentAndFoley()
        {
            float ui = Wavs().Where(f => Path.GetFileName(f).StartsWith("ui_")).Max(Peak);
            float other = Wavs().Where(f => Path.GetFileName(f).StartsWith("eq_") || Path.GetFileName(f).StartsWith("foley_")).Min(Peak);
            Assert.LessOrEqual(ui, .5f, "the loudest UI sound");
            Assert.Less(ui, other, "UI peaks must stay below the quietest equipment or foley sound");
        }
    }
}
