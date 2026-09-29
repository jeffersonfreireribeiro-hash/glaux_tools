using System;
using System.Collections.Generic;
using Buraqueira_Tools.ProjectState;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>Leitura das entradas comuns dos componentes da pilha Vault.</summary>
    internal static class VaultInputs
    {
        /// <summary>Junta os PillBundles recebidos em parâmetros nomeados (avisando colisões de nomes).</summary>
        public static void AddBundles(IList<IGH_Goo> items, SnapshotParts parts, ICollection<string> warnings)
        {
            foreach (var goo in items)
            {
                PillBundle bundle = null;
                if (goo is GH_PillBundleGoo pb) bundle = pb.Value;
                else if (goo is GH_ObjectWrapper w && w.Value is PillBundle wb) bundle = wb;
                else if (goo is GH_String s && !string.IsNullOrWhiteSpace(s.Value))
                {
                    try { bundle = PillBundle.FromJson(s.Value); } catch { }
                }

                if (bundle == null)
                {
                    if (goo != null) warnings.Add($"'Parameters' espera PillBundle; item {goo.TypeName} ignorado.");
                    continue;
                }

                var units = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in BundleTrees.FromBundle(bundle, units))
                {
                    if (parts.Parameters.ContainsKey(kv.Key)) warnings.Add($"Parâmetro '{kv.Key}' repetido entre bundles; mantido o último.");
                    parts.Parameters[kv.Key] = kv.Value;
                }
                foreach (var kv in units) parts.Units[kv.Key] = kv.Value;
            }
        }
    }
}
