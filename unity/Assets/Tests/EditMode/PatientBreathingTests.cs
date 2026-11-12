using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.Core.Observation;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The patient's visible breathing follows the respiratory rate the trainee has taken, and stays at rest before that (p0-characters-s05).</summary>
    public class PatientBreathingTests
    {
        private static List<ObservedVital> Vitals(string value) => new List<ObservedVital> { new ObservedVital("spo2", "97", "t"), new ObservedVital("respiratory_rate", value, "t") };

        [Test] public void Before_a_rate_is_taken_the_patient_breathes_at_rest()
        {
            Assert.AreEqual(PatientBreathing.RestingRate, PatientBreathing.Rate(null));
            Assert.AreEqual(PatientBreathing.RestingRate, PatientBreathing.Rate(new List<ObservedVital> { new ObservedVital("spo2", "97", "t") }));
        }

        [Test] public void The_measured_rate_sets_the_visible_rate() => Assert.AreEqual(24f, PatientBreathing.Rate(Vitals("24")));

        [TestCase("2", 6f)]
        [TestCase("90", 40f)]
        [TestCase("", 14f)]
        [TestCase("fast", 14f)]
        [TestCase("0", 14f)]
        public void A_silly_or_unreadable_rate_is_kept_in_range(string value, float expected) => Assert.AreEqual(expected, PatientBreathing.Rate(Vitals(value)));
    }
}
