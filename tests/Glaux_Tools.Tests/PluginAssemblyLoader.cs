using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Glaux_Tools.Tests
{
    /// <summary>
    /// O plugin é compilado como Glaux_Tools.gha; o host do .NET só procura Glaux_Tools.dll.
    /// Resolve o assembly pelo arquivo .gha copiado para a pasta de saída dos testes.
    /// </summary>
    internal static class PluginAssemblyLoader
    {
        [ModuleInitializer]
        internal static void Register()
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (!string.Equals(name.Name, "Glaux_Tools", StringComparison.OrdinalIgnoreCase)) return null;
                string gha = Path.Combine(AppContext.BaseDirectory, "Glaux_Tools.gha");
                return File.Exists(gha) ? context.LoadFromAssemblyPath(gha) : null;
            };
        }
    }
}
