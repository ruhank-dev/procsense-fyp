using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The bay shows the emergency department dressing for emergency cases and the quiet dressing for every other setting.</summary>
    public class BayVariantTests
    {
        [TestCase("emergency department", true)] [TestCase("Emergency Department", true)] [TestCase("ED", true)] [TestCase("A&E", true)]
        [TestCase("pre-assessment", false)] [TestCase("ward", false)] [TestCase("", false)] [TestCase(null, false)]
        public void Only_emergency_settings_count_as_emergency(string setting, bool expected) => Assert.AreEqual(expected, BayVariant.IsEmergency(setting));

        [Test]
        public void The_two_groups_switch_with_the_setting()
        {
            var host = new GameObject("variant"); var quiet = new GameObject("quiet art"); var ed = new GameObject("ED dressing");
            try
            {
                var v = host.AddComponent<BayVariant>(); v.quiet = new List<GameObject> { quiet }; v.emergency = new List<GameObject> { ed };
                v.Apply("emergency department"); Assert.IsTrue(ed.activeSelf); Assert.IsFalse(quiet.activeSelf); Assert.IsTrue(v.Emergency);
                v.Apply("pre-assessment"); Assert.IsFalse(ed.activeSelf); Assert.IsTrue(quiet.activeSelf); Assert.IsFalse(v.Emergency);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(quiet); Object.DestroyImmediate(ed); }
        }
    }
}
