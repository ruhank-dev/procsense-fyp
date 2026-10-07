using System.Linq;
using ProcSense.Core;

namespace ProcSense.Core.Tests
{
    /// <summary>Guards the rule that Core stays engine-free so it runs identically headless and on Quest.</summary>
    public class CoreBoundaryTests
    {
        [Fact]
        public void Core_does_not_reference_Unity()
        {
            var refs = typeof(CoreInfo).Assembly.GetReferencedAssemblies().Select(a => a.Name);

            Assert.DoesNotContain(refs, n => n != null && n.StartsWith("UnityEngine"));
        }

        [Fact]
        public void Core_targets_netstandard_2_1()
        {
            var framework = typeof(CoreInfo).Assembly
                .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>()
                .Single();

            Assert.Equal(".NETStandard,Version=v2.1", framework.FrameworkName);
        }
    }
}
