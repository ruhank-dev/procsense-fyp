using System.Collections.Generic;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using ProcSense.VR.UI.Kit;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The one place sounds of the sound list are played (P3-SND-04, P3-SND-05, P3-MNU-10). Every sound has one trigger:
    /// <list type="bullet">
    /// <item>the kit's events (hover, press, dialog open and close, confirm, submit, toggle, toast, error, board open and close) through the UI group;</item>
    /// <item>the engine's feedback stream: a completed goal chimes in training only (the examination gives no such signal), a hint revealed, the submission confirmed, the examination's end;</item>
    /// <item>the game flow: the menu theme plays on the menu screens and never during a case (Briefing, Playing), the results play the sting of their verdict, and each change of screen fires
    /// one hook (open, select, back, music start or stop; MNU-10).</item>
    /// </list>
    /// <see cref="Plays"/> counts what played, for the tests and the loudness pass.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        public static SoundPlayer Instance { get; private set; }
        public AudioSource Music { get; private set; }
        /// <summary>True while the menu theme is meant to play (the source may be silent with no audio device, as in batch tests).</summary>
        public bool MusicOn { get; private set; }
        public readonly Dictionary<string, int> Plays = new Dictionary<string, int>();
        /// <summary>The menu hooks fired, in order ("music_start", "music_stop", "open", "back", "select"), for MNU-10.</summary>
        public readonly List<string> Hooks = new List<string>();
        public System.Func<MenuFlow> FlowSource = () => MenuFlow.Current;

        private AudioSource _ui, _fx;
        private MenuPhase? _phase;
        private ProcSense.Core.Flow.ResultSummary _stungFor;

        public static SoundPlayer Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Sound player");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return go.AddComponent<SoundPlayer>();
        }

        private void Awake()
        {
            Instance = this;
            Music = Source(MixGroup.Music, true);
            _ui = Source(MixGroup.UI, false);
            _fx = Source(MixGroup.Effects, false);
            KitEvents.Sound += Play;
            SimFeedback.Default.Raised += OnFeedback;
        }

        private void OnDestroy()
        {
            KitEvents.Sound -= Play;
            SimFeedback.Default.Raised -= OnFeedback;
            if (Instance == this) Instance = null;
        }

        private AudioSource Source(MixGroup group, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f;
            AudioDirector.Route(s, group);
            return s;
        }

        public static AudioClip Clip(string id) => Resources.Load<AudioClip>("Sfx/" + id);

        /// <summary>Plays one clip of the list once (UI sounds on the UI group, the rest on effects).</summary>
        public void Play(string id)
        {
            Plays[id] = Plays.TryGetValue(id, out var k) ? k + 1 : 1;
            var clip = Clip(id);
            if (clip == null) return;
            (id.StartsWith("ui_") ? _ui : _fx).PlayOneShot(clip);
        }

        public int Count(string id) => Plays.TryGetValue(id, out var k) ? k : 0;

        public void OnFeedback(SimFeedbackEvent e)
        {
            switch (e.Kind)
            {
                case FeedbackKind.GoalCompleted:
                    if (ModeGate.PracticeAids) Play("trn_step_chime");   // never in the examination: a chime would tell the order
                    break;
                case FeedbackKind.ActionAccepted:
                    if (e.Id == "request_hint") Play("exm_hint_reveal");
                    else if (e.Id == "submit_patient") Play("exm_submit_confirm");
                    else if (e.Id == "sign_consent") Play("foley_pen_sign");
                    else if (e.Id == "open_notes") Play("foley_paper");
                    break;
                case FeedbackKind.EndState:
                    if (!ModeGate.PracticeAids) Play("exm_end");
                    break;
            }
        }

        private void Update() => Step();

        /// <summary>Follows the game flow's screen (Update calls it; tests call it directly).</summary>
        public void Step()
        {
            var flow = FlowSource?.Invoke();
            var phase = flow != null ? flow.Phase : (MenuPhase?)null;
            if (phase == _phase) return;
            var before = _phase;
            _phase = phase;
            bool menu = phase == MenuPhase.Login || phase == MenuPhase.Consent || phase == MenuPhase.MainMenu || phase == MenuPhase.Surgery;
            if (menu && !MusicOn) { MusicOn = true; Music.clip = Clip("music_menu_loop"); if (Music.clip != null) Music.Play(); Hook("music_start"); }
            else if (!menu && MusicOn) { MusicOn = false; Music.Stop(); Hook("music_stop"); }

            if (phase == MenuPhase.Results && flow.Summary != null && !ReferenceEquals(flow.Summary, _stungFor))
            {
                _stungFor = flow.Summary;
                var s = flow.Summary;
                Play(s.Abandoned ? "sting_not_completed" : s.Grade != null ? (s.Grade.Passed ? "sting_pass" : "sting_not_pass") : s.Result == "FAIL" ? "sting_not_pass" : "sting_pass");
            }
            if (before.HasValue && phase.HasValue)
            {
                if ((int)phase.Value < (int)before.Value && phase != MenuPhase.Briefing) Hook("back");              // a step back through the menus
                else if (phase == MenuPhase.Briefing) { Hook("select"); if (before == MenuPhase.Results) Play("tr_whoosh"); }
                else Hook("open");
            }
        }

        private void Hook(string name) => Hooks.Add(name);
    }
}
