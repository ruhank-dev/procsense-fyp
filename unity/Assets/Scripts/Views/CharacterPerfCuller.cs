using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Views
{
    /// <summary>Writes a blendshape weight only when it differs from what the renderer already holds (a write marks the mesh dirty and re-skins it). Reading the live value keeps it correct if anything else writes weights.</summary>
    public static class CharacterPerfBlend
    {
        public static void Set(SkinnedMeshRenderer r, int index, float weight)
        {
            if (Mathf.Abs(r.GetBlendShapeWeight(index) - weight) > .001f) r.SetBlendShapeWeight(index, weight);
        }
    }

    /// <summary>Put on a character (or any child) to stop <see cref="CharacterPerfCuller"/> from ever culling its Animator.</summary>
    public sealed class CharacterPerfKeepAnimated : MonoBehaviour { }

    /// <summary>
    /// Look-neutral character cost reducer for Quest 2. Two independent measures, both off unless <see cref="Enabled"/> (default true on an Android player only):
    /// (a) SkinnedMeshRenderer.updateWhenOffscreen = false, after locking generous local bounds that contain every bone plus a margin, so skinning is skipped only when the
    ///     character is truly outside the view frustum; (b) Animator.cullingMode = CullCompletely while none of the animator's renderers is visible for a delay, restored the
    ///     frame any renderer becomes visible. Animators with root motion, a <see cref="CharacterPerfKeepAnimated"/> marker, or a gameplay script that reads bones are never touched.
    /// Procedural bone/blendshape scripts (StaffIdleView, PatientIdleView, GownSecondaryMotion ...) are plain LateUpdate code and keep running either way.
    /// Turning <see cref="Enabled"/> off restores every original value.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CharacterPerfCuller : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        public const bool DefaultEnabled = true;
#else
        public const bool DefaultEnabled = false;
#endif
        private static bool _enabled = DefaultEnabled;
        /// <summary>Master switch (A/B). Changing it applies or restores on every live culler immediately.</summary>
        public static bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; Changed?.Invoke(); } }
        public static bool LockSkinnedBounds = true;
        public static bool CullInvisibleAnimators = true;
        /// <summary>CPU fast path (round 3): an animator that is currently running is sampled for visibility every <see cref="SampleInterval"/> seconds instead of every frame (the cull delay is a second, so a sample
        /// interval of a tenth changes nothing), the renderer that was last seen visible is asked first, and a culled animator is still checked every frame so it wakes on the first frame it is seen. false = the original per-frame scan.</summary>
        public static bool FastTick = true;
        public const float SampleInterval = 0.1f;
        private float _lastSample = float.NegativeInfinity;
        /// <summary>Test seam: how visibility is read. Default is Renderer.isVisible (any camera, both eyes, shadow views included).</summary>
        public static Func<Renderer, bool> VisibilityProbe = r => r.isVisible;
        private static event Action Changed;

        /// <summary>Gameplay scripts that read bones or the Animator pose: animators next to them are never culled.</summary>
        private static readonly HashSet<string> ExemptScripts = new HashSet<string> { "PropGrip", "SeatedPerson", "PatientTransportView", "TransportActor", "SensorAttachment", "PatientMotion" };

        [SerializeField, Tooltip("Seconds with nothing visible before an animator is culled.")] private float cullDelaySeconds = 1f;
        [SerializeField, Tooltip("Minimum metres added around the bone extents.")] private float minMargin = .35f;
        [SerializeField, Tooltip("Margin as a fraction of the bone extents (doubled for root-motion characters).")] private float marginFraction = .3f;
        [SerializeField, Tooltip("Optional: only manage this hierarchy (null = every skinned mesh in the loaded scenes).")] private Transform scope;
        [SerializeField, Tooltip("Seconds between scans for newly spawned characters (0 = scene load only).")] private float rescanSeconds = 10f;

        public Transform Scope { get => scope; set => scope = value; }
        public float CullDelaySeconds { get => cullDelaySeconds; set => cullDelaySeconds = value; }

        private sealed class SkinRec { public SkinnedMeshRenderer R; public bool OrigOffscreen; public Bounds OrigBounds; public bool Applied; public bool RootMotion; }
        private sealed class AnimRec { public Animator A; public AnimatorCullingMode Orig; public Renderer[] Rends; public float InvisibleSince = -1f; public bool Culled; public bool Eligible; }
        private readonly List<SkinRec> _skins = new List<SkinRec>();
        private readonly List<AnimRec> _anims = new List<AnimRec>();
        private readonly HashSet<SkinnedMeshRenderer> _knownSkins = new HashSet<SkinnedMeshRenderer>();
        private readonly HashSet<Animator> _knownAnims = new HashSet<Animator>();
        private float _nextScan;

        public int ManagedSkinned => _skins.Count;
        public int ManagedAnimators => _anims.Count;
        public bool IsAnimatorCulled(Animator a) { foreach (var r in _anims) if (r.A == a) return r.Culled; return false; }
        public bool IsAnimatorEligible(Animator a) { foreach (var r in _anims) if (r.A == a) return r.Eligible; return false; }

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CharacterPerfCuller>() != null) return;
            var go = new GameObject("CharacterPerfCuller"); DontDestroyOnLoad(go); go.AddComponent<CharacterPerfCuller>();
        }
#endif

        private void OnEnable() { Changed += OnSwitch; SceneManager.sceneLoaded += OnScene; Rescan(); OnSwitch(); }
        private void OnDisable() { Changed -= OnSwitch; SceneManager.sceneLoaded -= OnScene; RestoreAll(); }
        private void OnScene(Scene s, LoadSceneMode m) { Rescan(); OnSwitch(); }
        private void OnSwitch() { if (_enabled) ApplyAll(); else RestoreAll(); }

        /// <summary>Finds characters not yet managed. Cheap enough for a one-off; also runs on scene load and every <c>rescanSeconds</c>.</summary>
        public void Rescan()
        {
            _skins.RemoveAll(r => r.R == null); _anims.RemoveAll(r => r.A == null);
            _knownSkins.RemoveWhere(r => r == null); _knownAnims.RemoveWhere(r => r == null);
            foreach (var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!InScope(r.transform) || !_knownSkins.Add(r)) continue;
                _skins.Add(new SkinRec { R = r, OrigOffscreen = r.updateWhenOffscreen, OrigBounds = r.localBounds });
            }
            foreach (var a in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!InScope(a.transform) || !_knownAnims.Add(a)) continue;
                var rends = a.GetComponentsInChildren<Renderer>(true);
                _anims.Add(new AnimRec { A = a, Orig = a.cullingMode, Rends = rends, Eligible = rends.Length > 0 && IsEligible(a) });
            }
            if (_enabled) ApplyAll();
        }

        private bool InScope(Transform t) => scope == null || t == scope || t.IsChildOf(scope);

        private static bool IsEligible(Animator a)
        {
            if (a.applyRootMotion) return false;
            if (a.GetComponentInParent<CharacterPerfKeepAnimated>(true) != null || a.GetComponentInChildren<CharacterPerfKeepAnimated>(true) != null) return false;
            foreach (var mb in a.GetComponentsInParent<MonoBehaviour>(true)) if (mb != null && ExemptScripts.Contains(mb.GetType().Name)) return false;
            foreach (var mb in a.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null && ExemptScripts.Contains(mb.GetType().Name)) return false;
            return true;
        }

        private void ApplyAll()
        {
            if (!LockSkinnedBounds) { foreach (var r in _skins) Restore(r); return; }
            foreach (var r in _skins) Lock(r);
        }

        private void Lock(SkinRec rec)
        {
            var s = rec.R; if (s == null || rec.Applied || s.sharedMesh == null) return;
            var root = s.rootBone != null ? s.rootBone : s.transform;
            var b = s.localBounds;
            var bones = s.bones;
            foreach (var bone in bones) if (bone != null) b.Encapsulate(root.InverseTransformPoint(bone.position));
            bool rootMotion = false; var an = s.GetComponentInParent<Animator>(); if (an != null) rootMotion = an.applyRootMotion;
            float m = Mathf.Max(minMargin, marginFraction * b.extents.magnitude) * (rootMotion ? 2f : 1f);
            b.Expand(m * 2f);                                   // Expand adds to size, so the extents grow by m on every side
            s.localBounds = b; s.updateWhenOffscreen = false; rec.Applied = true;
        }

        private static void Restore(SkinRec rec)
        {
            if (rec.R == null || !rec.Applied) return;
            rec.R.updateWhenOffscreen = rec.OrigOffscreen; rec.R.localBounds = rec.OrigBounds; rec.Applied = false;
        }

        private void RestoreAll()
        {
            foreach (var r in _skins) Restore(r);
            foreach (var r in _anims) if (r.A != null && r.Culled) { r.A.cullingMode = r.Orig; r.Culled = false; r.InvisibleSince = -1f; }
        }

        private void Update()
        {
            if (!_enabled) return;
            long t0 = CullCost.Begin(); Tick(Time.unscaledTime); CullCost.End(CullCost.CharUpdate, t0);
        }

        /// <summary>One visibility pass. Public so tests can drive it with synthetic time.</summary>
        public void Tick(float now)
        {
            if (rescanSeconds > 0f && now >= _nextScan) { _nextScan = now + rescanSeconds; long t1 = CullCost.Begin(); Rescan(); CullCost.End(CullCost.CharRescan, t1); }
            if (!_enabled || !CullInvisibleAnimators) { if (!CullInvisibleAnimators) ReleaseCulled(); return; }
            bool sampleRunning = !FastTick || now < _lastSample || now - _lastSample >= SampleInterval;
            if (sampleRunning) _lastSample = now;
            for (int ai = 0; ai < _anims.Count; ai++)
            {
                var r = _anims[ai];
                if (r.A == null || !r.Eligible) continue;
                if (FastTick && !r.Culled && !sampleRunning) continue;
                bool visible = false;
                for (int k = 0; k < r.Rends.Length; k++)
                {
                    var rd = r.Rends[k];
                    if (rd != null && VisibilityProbe(rd)) { visible = true; if (FastTick && k > 0) { r.Rends[k] = r.Rends[0]; r.Rends[0] = rd; } break; }
                }
                if (visible)
                {
                    r.InvisibleSince = -1f;
                    if (r.Culled) { r.A.cullingMode = r.Orig; r.Culled = false; }
                }
                else if (!r.Culled)
                {
                    if (r.InvisibleSince < 0f) r.InvisibleSince = now;
                    else if (now - r.InvisibleSince >= cullDelaySeconds && r.A.isActiveAndEnabled) { r.A.cullingMode = AnimatorCullingMode.CullCompletely; r.Culled = true; }
                }
            }
        }

        private void ReleaseCulled() { foreach (var r in _anims) if (r.A != null && r.Culled) { r.A.cullingMode = r.Orig; r.Culled = false; r.InvisibleSince = -1f; } }
    }
}
