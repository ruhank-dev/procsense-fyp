using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>Marks a used needle or blade the trainee carries: it can be put in a <see cref="SharpsBin"/>.</summary>
    public sealed class UsedSharp : MonoBehaviour { }

    /// <summary>
    /// The sharps bin (equipment spec row 33): a used sharp dropped through its opening goes into the bin. A visual and physical cue only (no numeric data, no engine action): the sharp disappears,
    /// the bin counts it and a snap plays. Needs a trigger collider over the opening.
    /// </summary>
    public sealed class SharpsBin : MonoBehaviour
    {
        public int Count { get; private set; }

        /// <summary>Takes a used sharp into the bin. False for anything else (a clean item stays in the hand).</summary>
        public bool Dispose(UsedSharp sharp)
        {
            if (sharp == null || !sharp.gameObject.activeInHierarchy) return false;
            sharp.gameObject.SetActive(false);
            Count++;
            ProcSense.VR.Audio.SoundPlayer.Instance?.Play("foley_wristband_snap");   // a plastic snap: the only clip close to a sharp dropping in
            return true;
        }

        private void OnTriggerEnter(Collider other) => Dispose(other.GetComponentInParent<UsedSharp>());
    }
}
