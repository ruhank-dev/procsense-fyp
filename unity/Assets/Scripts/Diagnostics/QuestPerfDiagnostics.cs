#if PROCSENSE_PERF_DIAG
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using ProcSense.VR.Boot;
using ProcSense.VR.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace ProcSense.VR.Diagnostics
{
    /// <summary>
    /// The Quest diagnostic build's benchmark helper (P3-PRF-01; only compiled with PROCSENSE_PERF_DIAG, which only BuildScript's "diag" variants set).
    /// Controls (Quest Touch): left thumbstick click = next configuration (B0..B14), right thumbstick click = next benchmark view (V1..V8), hold left X for 1.5 s = automatic sweep of every
    /// configuration at the current view (3 s settle + 10 s measure each), both thumbsticks together = hide or show the readout.
    /// Every second it appends a row to persistentDataPath/perf/diag_*.csv; each sweep step adds a row to perf/sweep_*.csv. Numbers come from the frame times and FrameTimingManager on the device
    /// (no estimate). OVR Metrics Tool stays the reference for GPU/CPU levels and throttling.
    /// </summary>
    public sealed class QuestPerfDiagnostics : MonoBehaviour
    {
        // Benchmark views of docs/qa/quest2_sannan_benchmark_guide.md: camera position and look-at point in world metres (the QA capture views, unity/Tools/QA/views)
        private static readonly (string id, Vector3 p, Vector3 l)[] Views =
        {
            ("V1 bay near wall", new Vector3(0f, 1.6f, -1.4f), new Vector3(0f, 1.6f, -1.95f)),
            ("V2 bay overview", new Vector3(2.2f, 1.65f, -1.8f), new Vector3(-1.3f, 1.0f, 0.9f)),
            ("V3 bay window", new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f)),
            ("V4 corridor long", new Vector3(4.5f, 1.7f, 13.6f), new Vector3(4.5f, 1.5f, -9f)),
            ("V5 corridor people", new Vector3(3.2f, 1.55f, 5.6f), new Vector3(5.45f, 1.2f, 7.9f)),
            ("V6 reception", new Vector3(4.5f, 1.6f, -1f), new Vector3(4.5f, 1.4f, -9f)),
            ("V7 theatre", new Vector3(7f, 1.7f, 5.2f), new Vector3(11f, 1.2f, 5.4f)),
            ("V8 corridor exterior", new Vector3(4.5f, 1.6f, 9f), new Vector3(4.5f, 1.5f, 14.2f)),
        };

        private readonly PerfToggles _toggles = new PerfToggles();
        private readonly PerfWindow _second = new PerfWindow(), _rolling = new PerfWindow(), _sweep = new PerfWindow();
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private int _preset, _view = -1;
        private float _nextRow, _nextApply, _xHeld;
        private bool _l, _r, _hudOn = true, _sweeping;
        private string _csv = "", _sweepCsv = "", _status = "";
        private TextMeshPro _hud;
        private PerfSummary _last;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("Quest perf diagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestPerfDiagnostics>();
        }

        private void Start()
        {
            string dir = Path.Combine(Application.persistentDataPath, "perf");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "Z";
            _csv = Path.Combine(dir, "diag_" + stamp + ".csv");
            _sweepCsv = Path.Combine(dir, "sweep_" + stamp + ".csv");
            string about = "# ProcSense diagnostic build " + Application.version + ", device " + SystemInfo.deviceModel + ", " + SystemInfo.graphicsDeviceType + ", gpu skinning " + SystemInfo.supportsComputeShaders + "/" + GpuSkinningLabel + ", started " + stamp;
            File.WriteAllText(_csv, about + "\nutc,config,view,room,eye_scale,foveation," + PerfSummary.CsvHeader + "\n");
            File.WriteAllText(_sweepCsv, about + "\nutc,config,label,changes_look,view,room," + PerfSummary.CsvHeader + "\n");
            FrameTimingManager.CaptureFrameTimings();
        }

        /// <summary>Which skinning this APK was built with (set by BuildScript for the diag-gpuskin variant through a second define).</summary>
#if PROCSENSE_PERF_GPUSKIN
        private const string GpuSkinningLabel = "GPU";
#else
        private const string GpuSkinningLabel = "CPU";
#endif

        private void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            double cpu = double.NaN, gpu = double.NaN;
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0) { cpu = _timing[0].cpuFrameTime; gpu = _timing[0].gpuFrameTime; }
            double ms = Time.unscaledDeltaTime * 1000.0;
            _second.Add(ms, cpu, gpu); _rolling.Add(ms, cpu, gpu); if (_sweeping) _sweep.Add(ms, cpu, gpu);

            ReadInput();
            if (Time.unscaledTime >= _nextApply) { _nextApply = Time.unscaledTime + 0.5f; _toggles.Apply(PerfPresets.All[_preset], Camera.main, Room()); }
            if (Time.unscaledTime >= _nextRow)
            {
                _nextRow = Time.unscaledTime + 1f;
                var s = _second.Summarise(); _second.Clear();
                File.AppendAllText(_csv, Utc() + "," + PerfPresets.All[_preset].Id + "," + ViewId() + "," + Room() + "," + F(XRSettings.eyeTextureResolutionScale) + "," + F(QuestRenderTier.Foveation) + "," + s.Csv() + "\n");
                if (_rolling.Count > 400) { _last = _rolling.Summarise(); _rolling.Clear(); }
                UpdateHud();
            }
        }

        private void ReadInput()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool l);
            right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool r);
            left.TryGetFeatureValue(CommonUsages.primaryButton, out bool x);
            if (l && r && !(_l && _r)) { _hudOn = !_hudOn; if (_hud != null) _hud.gameObject.SetActive(_hudOn); }
            else if (!_sweeping && l && !_l && !r) Select((_preset + 1) % PerfPresets.All.Count);
            else if (!_sweeping && r && !_r && !l) GoToView((_view + 1) % Views.Length);
            _l = l; _r = r;
            _xHeld = x ? _xHeld + Time.unscaledDeltaTime : 0f;
            if (_xHeld > 1.5f && !_sweeping) { _xHeld = -999f; StartCoroutine(Sweep()); }
        }

        private void Select(int preset)
        {
            _preset = preset; _rolling.Clear(); _nextApply = 0f;
            _status = PerfPresets.All[_preset].Id + " " + PerfPresets.All[_preset].Label;
        }

        private IEnumerator Sweep()
        {
            _sweeping = true;
            for (int i = 0; i < PerfPresets.All.Count; i++)
            {
                Select(i); _status = "Sweep " + (i + 1) + "/" + PerfPresets.All.Count + ": " + PerfPresets.All[i].Id + " (settling)";
                yield return new WaitForSecondsRealtime(3f);
                _sweep.Clear(); _status = "Sweep " + (i + 1) + "/" + PerfPresets.All.Count + ": " + PerfPresets.All[i].Id + " (measuring)";
                yield return new WaitForSecondsRealtime(10f);
                var p = PerfPresets.All[i];
                File.AppendAllText(_sweepCsv, Utc() + "," + p.Id + ",\"" + p.Label + "\"," + (p.ChangesLook ? "yes" : "no") + "," + ViewId() + "," + Room() + "," + _sweep.Summarise().Csv() + "\n");
            }
            Select(0); _sweeping = false; _status = "Sweep done: " + _sweepCsv;
        }

        private void GoToView(int v)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) { _status = "No XR rig to move"; return; }
            _view = v;
            var cc = origin.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
            origin.MoveCameraToWorldLocation(Views[v].p);
            var fwd = Views[v].l - Views[v].p; fwd.y = 0f;
            var cam = origin.Camera.transform.forward; cam.y = 0f;
            if (fwd.sqrMagnitude > 1e-4f && cam.sqrMagnitude > 1e-4f) origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(cam, fwd, Vector3.up));
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            _rolling.Clear(); _status = Views[v].id;
        }

        private void UpdateHud()
        {
            if (!_hudOn) return;
            if (_hud == null)
            {
                var cam = Camera.main; if (cam == null) return;
                var go = new GameObject("Perf readout"); go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = new Vector3(0.12f, -0.12f, 0.7f);
                _hud = go.AddComponent<TextMeshPro>(); _hud.fontSize = 0.18f; _hud.rectTransform.sizeDelta = new Vector2(0.5f, 0.3f); _hud.alignment = TextAlignmentOptions.TopLeft; _hud.color = new Color(1f, 1f, .6f);
            }
            var p = PerfPresets.All[_preset];
            _hud.text = p.Id + (p.ChangesLook ? " (measurement only)" : "") + "\n" + p.Label + "\n" + ViewId() + " · " + Room()
                + "\nFPS " + F(_last.AvgFps) + "  1% low " + F(_last.OnePercentLowFps)
                + "\nCPU " + (_last.HasCpu ? F(_last.AvgCpuMs) + " ms" : "n/a") + "  GPU " + (_last.HasGpu ? F(_last.AvgGpuMs) + " ms" : "n/a")
                + "\nfov " + F(QuestRenderTier.Foveation) + "  scale " + F(XRSettings.eyeTextureResolutionScale) + "  skin " + GpuSkinningLabel
                + "\n" + _status;
        }

        private string ViewId() => _view < 0 ? "free" : Views[_view].id.Substring(0, 2);
        private static string Room() { var r = FindAnyObjectByType<RoomLoader>(); return r != null ? r.CurrentRoom : "?"; }
        private static string Utc() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
        private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
#endif
