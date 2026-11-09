using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>QuestRuntimeSettings installs only in the Android player and never throws where no XR display runs.</summary>
    public sealed class QuestRuntimeSettingsTests
    {
        [Test] public void Installs_only_in_the_android_player()
        {
            Assert.IsTrue(QuestRuntimeSettings.ShouldInstall(RuntimePlatform.Android, false));
            Assert.IsFalse(QuestRuntimeSettings.ShouldInstall(RuntimePlatform.Android, true));
            Assert.IsFalse(QuestRuntimeSettings.ShouldInstall(RuntimePlatform.WindowsPlayer, false));
            Assert.IsFalse(QuestRuntimeSettings.ShouldInstall(RuntimePlatform.WindowsEditor, true));
        }

        [Test] public void Target_rate_is_72_hz_only()
        {
            Assert.IsTrue(QuestRuntimeSettings.IsTargetRate(72f));
            Assert.IsTrue(QuestRuntimeSettings.IsTargetRate(72.3f));
            Assert.IsFalse(QuestRuntimeSettings.IsTargetRate(90f));
            Assert.IsFalse(QuestRuntimeSettings.IsTargetRate(60f));
        }

        [Test] public void Does_nothing_and_does_not_throw_without_an_xr_display()
        {
            Assert.IsFalse(QuestRuntimeSettings.TryApply());
            Assert.IsNull(Object.FindAnyObjectByType<QuestRuntimeSettings>(), "not installed in the Editor");
        }
    }
}
