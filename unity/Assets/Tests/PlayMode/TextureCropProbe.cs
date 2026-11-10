using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Boot;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Texture quality probe (audit lane 3). Renders every text-bearing texture (posters with small print, the notice board, reception and TV screens, monitor UI and labels) the way a Quest eye sees it: one 1832 x 1920 eye,
    /// 4x MSAA, 96 degree vertical field of view (about 20 pixels per degree), camera 0.8 to 1.2 m in front of the surface looking at its centre. The caller changes the Standalone import format of those textures between
    /// launches (Editor method TextureQualityProbe.Apply with TEXPROBE_FMT = U, A66 or A44, then CLEAR), so a pixel comparison of the same pose shows what compression costs.
    ///
    /// Poses: the first launch (state U, TEXPROBE_SELECT=1) derives the pose of each surface from the renderer itself: the thinnest axis of its LOCAL bounds (the surface normal, correct for rotated objects),
    /// both sides, every renderer that uses the texture; it keeps the first view whose projected oriented rectangle holds enough edge energy to contain text (mean luminance gradient at least the per-surface minimum).
    /// Surfaces with no such view are written as dropped. The poses go to poses.json in TEXPROBE_DIR; every later launch (A66, A44) replays exactly those poses.
    ///
    /// Environment: TEXPROBE_DIR (output folder), TEXPROBE_TAG (state name), TEXPROBE_SELECT=1 (derive and write the poses; otherwise poses.json must already exist).
    /// Output per state: one PNG per accepted surface, formats.csv (the format and size Unity loaded for each texture), mem.txt. Test filter: ProcSense.Tests.PlayMode.TextureCropProbe.
    /// Not a regression test: it asserts only that it produced pictures.
    /// </summary>
    public sealed class TextureCropProbe
    {
        private const int W = 1832, H = 1920;
        private const float EdgeMinDefault = 4f, EdgeMinDarkScreen = 2f;

        // Textures that carry small print (checked against the source PNGs). Excluded: poster_07 and print_1..3 (a headline and artwork only) and the wall clock.
        private static readonly string[] Targets =
        {
            "reception_screen", "tv_welcome", "bulletin_board", "poster_01", "poster_02", "poster_03", "poster_04", "poster_05", "poster_06", "poster_10", "poster_11", "poster_12",
            "monitor_ui", "monitor_labels",
        };
        private static readonly HashSet<string> DarkScreens = new HashSet<string> { "tv_welcome", "monitor_ui", "monitor_labels" };

        [Serializable] private sealed class PoseRow { public string target; public string renderer; public bool valid; public string note; public Vector3 position; public Quaternion rotation; public float x0, y0, x1, y1, edge, edge_min; }
        [Serializable] private sealed class PoseFile { public List<PoseRow> poses = new List<PoseRow>(); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Capture_text_crops()
        {
            string dir = Environment.GetEnvironmentVariable("TEXPROBE_DIR"); Assert.IsFalse(string.IsNullOrEmpty(dir), "set TEXPROBE_DIR");
            string tag = Environment.GetEnvironmentVariable("TEXPROBE_TAG") ?? "state";
            bool select = Environment.GetEnvironmentVariable("TEXPROBE_SELECT") == "1";
            string outDir = Path.Combine(dir, tag); Directory.CreateDirectory(outDir);
            string posesPath = Path.Combine(dir, "poses.json");
            PoseFile poses = new PoseFile();
            if (!select) { Assert.IsTrue(File.Exists(posesPath), "poses.json missing: run state U with TEXPROBE_SELECT=1 first"); poses = JsonUtility.FromJson<PoseFile>(File.ReadAllText(posesPath)); }

            int authored = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("TexProbeCamera"); var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f; cam.fieldOfView = 96f; cam.clearFlags = CameraClearFlags.Skybox; cam.aspect = (float)W / H;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var fmt = new StringBuilder("target,renderer,texture,width,height,format,graphics_format,mip_count\n");
            int shots = 0;

            // Renders from (pos, rot), reads the frame into tex, returns the edge energy inside the screen rectangle of the given world corners (screen y from the top).
            IEnumerator Shoot(Vector3 pos, Quaternion rot, Vector3[] corners, Action<float, Vector4> done)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                yield return null; cam.Render(); yield return null;
                var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
                foreach (var corner in corners)
                {
                    var sp = cam.WorldToScreenPoint(corner); x0 = Mathf.Min(x0, sp.x); y0 = Mathf.Min(y0, sp.y); x1 = Mathf.Max(x1, sp.x); y1 = Mathf.Max(y1, sp.y);
                }
                var rect = new Vector4(Mathf.Max(0, x0), Mathf.Max(0, H - y1), Mathf.Min(W, x1), Mathf.Min(H, H - y0));
                done(EdgeEnergy(tex, rect), rect);
            }

            if (select)
            {
                foreach (var name in Targets)
                {
                    float need = DarkScreens.Contains(name) ? EdgeMinDarkScreen : EdgeMinDefault;
                    var row = new PoseRow { target = name, valid = false, note = "no renderer uses this texture", edge_min = need };
                    var cands = new List<Renderer>();
                    foreach (var r in renderers) if (UsesTexture(r, name)) cands.Add(r);
                    cands.Sort((a, c) => a.bounds.size.sqrMagnitude.CompareTo(c.bounds.size.sqrMagnitude));
                    float best = -1f;
                    for (int ci = 0; ci < cands.Count && ci < 8 && !row.valid; ci++)
                    {
                        var r = cands[ci]; Vector3 centre; Vector3[] corners; float size;
                        var dirs = Facings(r, out centre, out corners, out size);
                        foreach (var n in dirs)
                        {
                            float dist = Mathf.Clamp(size / (2f * Mathf.Tan(48f * Mathf.Deg2Rad) * 0.75f), 0.8f, 3.0f);
                            if (size < 1f) dist = Mathf.Clamp(dist, 0.8f, 1.2f);
                            var pos = centre + n * dist; var rot = Quaternion.LookRotation(-n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up);
                            float edge = 0f; var rect = Vector4.zero;
                            yield return Shoot(pos, rot, corners, (en, rc) => { edge = en; rect = rc; });
                            if (edge > best) { best = edge; row.renderer = r.name; row.position = pos; row.rotation = rot; row.x0 = rect.x; row.y0 = rect.y; row.x1 = rect.z; row.y1 = rect.w; row.edge = edge; row.note = "best edge energy so far"; }
                            if (edge >= need && rect.z - rect.x >= 100f && rect.w - rect.y >= 60f)
                            {
                                // Proof that the crop really shows THIS texture (not, say, ceiling tiles): swap it for plain grey and require the rectangle to change.
                                var before = SampleGrid(tex, rect);
                                var swapped = SwapTexture(r, name, Texture2D.grayTexture);
                                float change = 0f;
                                if (swapped)
                                {
                                    yield return Shoot(pos, rot, corners, (en2, rc2) => { });
                                    var after = SampleGrid(tex, rect);
                                    for (int gi = 0; gi < before.Length; gi++) change += Mathf.Abs(before[gi] - after[gi]);
                                    change /= before.Length;
                                    SwapTexture(r, null, null);
                                }
                                if (swapped && change >= 6f) { row.valid = true; row.note = "text found, texture visible (grey swap changes the crop by " + change.ToString("0.0", CultureInfo.InvariantCulture) + ")"; break; }
                                row.note = "crop has edges but does not show this texture (grey swap changes it by " + change.ToString("0.0", CultureInfo.InvariantCulture) + ")";
                            }
                        }
                    }
                    if (!row.valid && cands.Count > 0) row.note = "dropped: no view shows this texture with text (best edge energy " + best.ToString("0.0", CultureInfo.InvariantCulture) + ", needs " + need.ToString("0.0", CultureInfo.InvariantCulture) + "; last test: " + row.note + ")";
                    poses.poses.Add(row);
                }
                File.WriteAllText(posesPath, JsonUtility.ToJson(poses, true));
            }

            foreach (var row in poses.poses)
            {
                // What did Unity actually load for this texture in this import state?
                foreach (var r in renderers)
                {
                    if (!UsesTexture(r, row.target)) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        foreach (var pn in m.GetTexturePropertyNames())
                        {
                            var t = m.GetTexture(pn); if (t == null || t.name != row.target) continue;
                            var t2 = t as Texture2D;
                            fmt.AppendLine(string.Join(",", row.target, r.name.Replace(',', ';'), t.name, t.width, t.height, t2 != null ? t2.format.ToString() : "?", t.graphicsFormat.ToString(), t2 != null ? t2.mipmapCount : 0));
                            goto formatDone;
                        }
                    }
                }
            formatDone:
                if (!row.valid) continue;
                cam.transform.SetPositionAndRotation(row.position, row.rotation);
                yield return null; cam.Render(); yield return null;
                var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, row.target + ".png"), tex.EncodeToPNG());
                shots++;
            }
            File.WriteAllText(Path.Combine(outDir, "formats.csv"), fmt.ToString());
            File.WriteAllText(Path.Combine(outDir, "mem.txt"), "tag=" + tag + "\ncurrentTextureMemory_bytes=" + Texture.currentTextureMemory + "\ntotalTextureMemory_bytes=" + Texture.totalTextureMemory + "\ndesiredTextureMemory_bytes=" + Texture.desiredTextureMemory + "\n");
            cam.targetTexture = null; rt.Release(); UnityEngine.Object.Destroy(go); QualitySettings.SetQualityLevel(authored, true);
            Assert.Greater(shots, 0, "no picture was produced (no surface with readable text in the poses)");
        }

        private static bool UsesTexture(Renderer r, string name)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                foreach (var pn in m.GetTexturePropertyNames()) { var t = m.GetTexture(pn); if (t != null && t.name == name) return true; }
            }
            return false;
        }

        /// <summary>
        /// Facing directions of a renderer in world space, best guess first: the thinnest axis of its LOCAL bounds (both signs, correct for a rotated surface), then the thinnest axis of the world bounds (both signs).
        /// Also returns the world-space centre, the eight corners of the oriented box (for the screen rectangle) and its largest dimension.
        /// </summary>
        private static List<Vector3> Facings(Renderer r, out Vector3 centre, out Vector3[] corners, out float size)
        {
            var tr = r.transform; var lb = r.localBounds; var le = lb.extents;
            centre = tr.TransformPoint(lb.center);
            corners = new Vector3[8];
            for (int k = 0; k < 8; k++)
                corners[k] = tr.TransformPoint(lb.center + new Vector3(((k & 1) == 0 ? -1 : 1) * le.x, ((k & 2) == 0 ? -1 : 1) * le.y, ((k & 4) == 0 ? -1 : 1) * le.z));
            var ws = new Vector3((corners[1] - corners[0]).magnitude, (corners[2] - corners[0]).magnitude, (corners[4] - corners[0]).magnitude);
            size = Mathf.Max(ws.x, ws.y, ws.z);
            var list = new List<Vector3>();
            int la = ws.x <= ws.y && ws.x <= ws.z ? 0 : (ws.y <= ws.z ? 1 : 2);
            var axis = la == 0 ? Vector3.right : la == 1 ? Vector3.up : Vector3.forward;
            var worldN = tr.TransformDirection(axis).normalized;
            list.Add(worldN); list.Add(-worldN);
            var e = r.bounds.extents; int wa = e.x <= e.y && e.x <= e.z ? 0 : (e.y <= e.z ? 1 : 2);
            var wv = wa == 0 ? Vector3.right : wa == 1 ? Vector3.up : Vector3.forward;
            foreach (var c in new[] { wv, -wv })
            {
                bool dup = false; foreach (var x in list) if (Vector3.Dot(x, c) > 0.97f) dup = true;
                if (!dup) list.Add(c);
            }
            foreach (var c in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                bool dup = false; foreach (var x in list) if (Vector3.Dot(x, c) > 0.97f) dup = true;
                if (!dup) list.Add(c);
            }
            return list;
        }

        private sealed class SavedBlock { public Renderer r; public int index; public MaterialPropertyBlock block; public bool had; }
        private static readonly List<SavedBlock> Saved = new List<SavedBlock>();

        /// <summary>Replaces every texture named <paramref name="name"/> on the renderer with <paramref name="with"/> through a property block (the shared materials stay untouched); with name == null restores what was there. Returns false when the renderer has no such texture slot.</summary>
        private static bool SwapTexture(Renderer r, string name, Texture with)
        {
            if (name == null)
            {
                foreach (var sb in Saved) { if (sb.r == null) continue; sb.r.SetPropertyBlock(sb.had ? sb.block : null, sb.index); }
                Saved.Clear(); return true;
            }
            bool any = false; var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                foreach (var pn in mats[i].GetTexturePropertyNames())
                {
                    var t = mats[i].GetTexture(pn); if (t == null || t.name != name) continue;
                    var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb, i);
                    var keep = new MaterialPropertyBlock(); r.GetPropertyBlock(keep, i);
                    Saved.Add(new SavedBlock { r = r, index = i, block = keep, had = !keep.isEmpty });
                    mpb.SetTexture(pn, with); r.SetPropertyBlock(mpb, i); any = true;
                }
            }
            return any;
        }

        /// <summary>A 48 x 48 grid of luminance samples inside the rectangle of the last frame (screen y from the top), for comparing two renders of the same pose.</summary>
        private static float[] SampleGrid(Texture2D t, Vector4 rect)
        {
            const int N = 48; var g = new float[N * N];
            var raw = t.GetRawTextureData<byte>();
            for (int gy = 0; gy < N; gy++)
                for (int gx = 0; gx < N; gx++)
                {
                    int x = Mathf.Clamp((int)Mathf.Lerp(rect.x, rect.z, (gx + 0.5f) / N), 0, W - 1), y = Mathf.Clamp((int)Mathf.Lerp(rect.y, rect.w, (gy + 0.5f) / N), 0, H - 1);
                    int i = ((H - 1 - y) * W + x) * 3;
                    g[gy * N + gx] = raw[i] * 0.299f + raw[i + 1] * 0.587f + raw[i + 2] * 0.114f;
                }
            return g;
        }

        /// <summary>Mean absolute luminance gradient (0 to 255 scale) over the rectangle of an RGB24 frame, sampling every second pixel. A plain wall scores about 1, printed text above 4.</summary>
        private static float EdgeEnergy(Texture2D t, Vector4 rect)
        {
            int x0 = Mathf.Clamp((int)rect.x, 0, W - 3), y0 = Mathf.Clamp((int)rect.y, 0, H - 3), x1 = Mathf.Clamp((int)rect.z, 0, W - 3), y1 = Mathf.Clamp((int)rect.w, 0, H - 3);
            if (x1 - x0 < 8 || y1 - y0 < 8) return 0f;
            var raw = t.GetRawTextureData<byte>();
            double sum = 0; long n = 0;
            for (int y = y0; y < y1; y += 2)
            {
                int row = (H - 1 - y) * W; // ReadPixels stores the bottom row first
                for (int x = x0; x < x1; x += 2)
                {
                    int i = (row + x) * 3; int j = i + 3; int k = ((H - 1 - (y + 1)) * W + x) * 3;
                    float l0 = raw[i] * 0.299f + raw[i + 1] * 0.587f + raw[i + 2] * 0.114f;
                    float lx = raw[j] * 0.299f + raw[j + 1] * 0.587f + raw[j + 2] * 0.114f;
                    float ly = raw[k] * 0.299f + raw[k + 1] * 0.587f + raw[k + 2] * 0.114f;
                    sum += Mathf.Abs(lx - l0) + Mathf.Abs(ly - l0); n++;
                }
            }
            return n == 0 ? 0f : (float)(sum / n);
        }
    }
}
