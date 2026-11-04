using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Diagnostic, not a gate: finds what changes in the lit state of a room around the first two seconds of play (the capture clips brighten/darken in one step near 1.75 s). Logs every frame on which the snapshot differs from the previous one.</summary>
    public sealed class BrightnessJumpProbe
    {
        private static string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) sb.Append($"L:{l.name}|{l.enabled}|{l.gameObject.activeInHierarchy}|{l.intensity:F3}|{l.shadows}|{l.shadowStrength:F2};");
            sb.Append($"AMB:{RenderSettings.ambientSkyColor}|{RenderSettings.ambientEquatorColor}|{RenderSettings.ambientGroundColor}|{RenderSettings.ambientIntensity:F3}|{RenderSettings.reflectionIntensity:F3};");
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)) sb.Append($"V:{v.name}|{v.enabled}|{v.weight:F3};");
            var st = VolumeManager.instance.stack; if (st != null) { var ca = st.GetComponent<ColorAdjustments>(); if (ca != null) sb.Append($"CA:{ca.postExposure.value:F3}|{ca.contrast.value:F1};"); var bl = st.GetComponent<Bloom>(); if (bl != null) sb.Append($"BL:{bl.intensity.value:F3};"); }
            foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include)) sb.Append($"RP:{rp.name}|{(rp.texture != null ? rp.texture.GetEntityId().ToString() : "0")}|{rp.enabled};");
            sb.Append($"REFLTEX:{(RenderSettings.customReflection != null ? RenderSettings.customReflection.GetEntityId().ToString() : "0")};SHD:{QualitySettings.shadowDistance:F1}|{QualitySettings.shadowCascades};LMAP:{LightmapSettings.lightmaps.Length}|{(LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0)};");
            sb.Append($"BEHAVIOURS:{Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude).Count(m => m != null && m.enabled)};");
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator Report_lighting_state_changes_in_the_first_seconds()
        {
            yield return SceneManager.LoadSceneAsync("PatientBay");
            for (int i = 0; i < 40; i++) yield return null;
            Time.captureDeltaTime = 1f / 12f;
            string prev = Snapshot(); int changes = 0;
            for (int f = 0; f < 40; f++)
            {
                yield return null; var s = Snapshot();
                if (s != prev)
                {
                    changes++; var a = prev.Split(';'); var b = s.Split(';');
                    var diff = b.Where(x => !a.Contains(x)).Take(6);
                    Debug.Log($"[JumpProbe] frame {f} t={Time.time:F2} changed: {string.Join(" || ", diff)}");
                }
                prev = s;
            }
            Time.captureDeltaTime = 0f; Debug.Log($"[JumpProbe] {changes} frames with a state change");
            Assert.Pass();
        }
    }
}
