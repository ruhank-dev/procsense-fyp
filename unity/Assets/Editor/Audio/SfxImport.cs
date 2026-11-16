using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools.Audio
{
    /// <summary>
    /// P3-SND-09: the import settings of the sound list (Assets/Audio/Sfx), by group from the clip's id, as the sound plan sets them: ambience and music are Vorbis and streamed (long, looping,
    /// one or two at a time); UI, voice cues, short equipment sounds and foley are ADPCM, decompressed on load (short, many, no decode cost when they fire); everything is mono (3D sounds need it,
    /// and 2D ones lose nothing). The Android override is the same so the headset build uses exactly these. Applied on every import, so a new clip needs nothing by hand.
    /// </summary>
    public sealed class SfxImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Audio/Sfx/";

        /// <summary>The settings for a clip id (amb_, music_ and the transition loops are long and streamed; stings, cues and effects are short).</summary>
        public static AudioImporterSampleSettings SettingsFor(string id)
        {
            bool longClip = id.StartsWith("amb_") || id.StartsWith("music_") || id.EndsWith("_loop") && !id.StartsWith("vox_");
            var s = new AudioImporterSampleSettings
            {
                loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM,
                quality = longClip ? .5f : 1f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            };
            return s;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            var s = SettingsFor(System.IO.Path.GetFileNameWithoutExtension(assetPath));
            importer.forceToMono = true;
            importer.loadInBackground = s.loadType == AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = s;
            importer.SetOverrideSampleSettings("Android", s);
        }
    }
}
