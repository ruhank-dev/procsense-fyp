using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The patient's skin signs (p0-characters-s03): pallor, flush, jaundice, cyanosis (each 0 to 1, tints the skin) and sweat (0 to 1, makes it glossy). The skin materials of the head and body get a per-renderer property block,
    /// so the shared materials never change, and the signs blend in over <see cref="blendSeconds"/>. Cosmetic only: it decides nothing. Whatever shows the state (Core's observable state) calls <see cref="SetSigns"/>;
    /// the fields are also the live preview in the Editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatientSkin : MonoBehaviour
    {
        [Range(0f, 1f)] public float pallor, flush, jaundice, cyanosis, sweat;
        [Range(.1f, 3f), Tooltip("Seconds for a sign to settle (changes must blend smoothly).")] public float blendSeconds = .6f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Smoothness = Shader.PropertyToID("_Smoothness");
        private struct Surface { public Renderer Renderer; public int Index; public Color Color; public float Gloss; }
        private readonly List<Surface> surfaces = new List<Surface>();
        private float[] now = new float[5];
        private MaterialPropertyBlock block;
        private bool found;

        /// <summary>Sets all the signs at once (each clamped to 0..1).</summary>
        public void SetSigns(float pallor, float flush, float jaundice, float cyanosis, float sweat)
        {
            this.pallor = Mathf.Clamp01(pallor); this.flush = Mathf.Clamp01(flush); this.jaundice = Mathf.Clamp01(jaundice); this.cyanosis = Mathf.Clamp01(cyanosis); this.sweat = Mathf.Clamp01(sweat);
        }

        /// <summary>The colour the skin is multiplied by for the signs (white when there are none).</summary>
        public static Color Tint(float pallor, float flush, float jaundice, float cyanosis)
        {
            var c = Color.white;
            c = Mul(c, new Color(.84f, .9f, 1f), pallor);        // less red: ashen
            c = Mul(c, new Color(1f, .78f, .78f), flush);        // red cheeks and neck
            c = Mul(c, new Color(1f, .97f, .68f), jaundice);     // yellow
            c = Mul(c, new Color(.55f, .78f, 1f), cyanosis);     // blue-grey
            return c;
        }

        /// <summary>The skin's smoothness with sweat: damp skin shines.</summary>
        public static float Gloss(float baseSmoothness, float sweat) => Mathf.Clamp01(baseSmoothness + .45f * sweat);

        private static Color Mul(Color c, Color k, float w) => new Color(c.r * Mathf.Lerp(1f, k.r, w), c.g * Mathf.Lerp(1f, k.g, w), c.b * Mathf.Lerp(1f, k.b, w), 1f);

        /// <summary>The signs now (after blending), in the order pallor, flush, jaundice, cyanosis, sweat. For tests and review.</summary>
        public float Current(int sign) => now[sign];

        private static bool IsSkin(string materialName) => materialName.StartsWith("m", System.StringComparison.Ordinal) && (materialName.Contains("_body") || materialName.Contains("_head"));

        private void Find()
        {
            found = true; surfaces.Clear();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && IsSkin(mats[i].name))
                        surfaces.Add(new Surface { Renderer = r, Index = i, Color = mats[i].HasProperty(BaseColor) ? mats[i].GetColor(BaseColor) : Color.white, Gloss = mats[i].HasProperty(Smoothness) ? mats[i].GetFloat(Smoothness) : .3f });
            }
            block = new MaterialPropertyBlock();
        }

        private float Goal(int sign) { switch (sign) { case 0: return pallor; case 1: return flush; case 2: return jaundice; case 3: return cyanosis; default: return sweat; } }   // no array per frame: this runs on the headset

        private void LateUpdate() => Step(Time.deltaTime);

        /// <summary>One frame of blending toward the signs; the review tool calls it outside play mode.</summary>
        public void Step(float dt)
        {
            if (!found) Find();
            float step = dt / Mathf.Max(.05f, blendSeconds); bool moving = false;
            for (int i = 0; i < 5; i++) { float before = now[i]; now[i] = Mathf.MoveTowards(now[i], Goal(i), step); moving |= now[i] != before; }
            if (!moving) return;                                                                  // nothing changed this frame: the block already holds the signs
            bool clear = now[0] + now[1] + now[2] + now[3] + now[4] <= 0f;
            var tint = Tint(now[0], now[1], now[2], now[3]);
            foreach (var s in surfaces)
            {
                if (s.Renderer == null) continue;
                if (clear) { s.Renderer.SetPropertyBlock(null, s.Index); continue; }       // no signs: back to the shared material (keeps SRP batching)
                s.Renderer.GetPropertyBlock(block, s.Index);
                block.SetColor(BaseColor, s.Color * tint); block.SetFloat(Smoothness, Gloss(s.Gloss, now[4]));
                s.Renderer.SetPropertyBlock(block, s.Index);
            }
        }
    }
}
