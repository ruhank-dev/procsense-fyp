using System.Collections.Generic;
using System.Globalization;
using ProcSense.Core.Observation;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The patient's visible breathing rate follows the respiratory rate the trainee has gathered (p0-characters-s05: visible breaths match the monitor). Until a rate has been taken the patient breathes at the resting default,
    /// because a view never reads more than the observable state shows.
    /// </summary>
    public static class PatientBreathing
    {
        public const float RestingRate = 14f, Slowest = 6f, Fastest = 40f;
        public const string VitalName = "respiratory_rate";

        public static float Rate(IReadOnlyList<ObservedVital> vitals)
        {
            if (vitals == null) return RestingRate;
            foreach (var v in vitals)
                if (v.Name == VitalName && float.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) && rate > 0f)
                    return rate < Slowest ? Slowest : rate > Fastest ? Fastest : rate;
            return RestingRate;
        }
    }
}
