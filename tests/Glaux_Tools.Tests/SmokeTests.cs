using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void PluginAssemblyLoads()
        {
            Assert.Equal("ACU_T60", PillHub.CleanUpKey("ACU_T60 [s]"));
        }

        [Fact]
        public void PluginInfoVersion_ComesFromTheAssembly()
        {
            var v = typeof(PillHub).Assembly.GetName().Version;
            Assert.Equal($"{v.Major}.{v.Minor}.{v.Build}", new BuraqueiraToolsAssemblyInfo().Version);
        }
    }
}
