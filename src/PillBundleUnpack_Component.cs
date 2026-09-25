using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PillBundleUnpack_Component : GH_Component
    {
        public PillBundleUnpack_Component()
            : base(
                "Pill Bundle Unpack",
                "PillUnpack",
                "Desempacota parâmetros de um PillBundle ou de uma string JSON externa, restaurando as chaves, valores e estruturas de árvore originais com total imutabilidade.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100004-e1ef-4000-8000-000000000004");
        protected override Bitmap Icon => GlauxToolsIcons.PillBundleUnpack;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Bundle", "B", "Pacote PillBundle ou string JSON contendo os parâmetros estruturados.", GH_ParamAccess.item);
            pManager.AddTextParameter("KeyFilter", "F", "Filtro opcional de chaves específicas ou grupos/categorias a extrair (ex: 'ACU', 'GEO', 'MAT', 'ACU_T60'). Se vazio ou '*', desempacota todos os parâmetros contidos.", GH_ParamAccess.list);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Keys", "K", "Lista de chaves extraídas.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "Árvore com os valores desempacotados. O ramo {i} contém os valores da chave K[i].", GH_ParamAccess.tree);
            pManager.AddTextParameter("Summary", "S", "Diagnóstico e resumo do conteúdo desempacotado.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object rawInput = null;
            if (!DA.GetData(0, ref rawInput) || rawInput == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Conecte um PillBundle ou uma string JSON válida.");
                return;
            }

            PillBundle bundle = null;

            if (rawInput is GH_PillBundleGoo pbGoo && pbGoo.Value != null)
            {
                bundle = pbGoo.Value;
            }
            else if (rawInput is GH_ObjectWrapper wrapper)
            {
                if (wrapper.Value is PillBundle b) bundle = b;
                else if (wrapper.Value is GH_PillBundleGoo pb) bundle = pb.Value;
            }
            else if (rawInput is PillBundle directBundle)
            {
                bundle = directBundle;
            }
            else
            {
                string jsonCandidate = rawInput.ToString();
                if (rawInput is GH_String ghStr) jsonCandidate = ghStr.Value;

                if (!string.IsNullOrWhiteSpace(jsonCandidate))
                {
                    try
                    {
                        bundle = PillBundle.FromJson(jsonCandidate);
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Erro ao converter JSON para PillBundle: {ex.Message}");
                        return;
                    }
                }
            }

            if (bundle == null || bundle.Entries == null || bundle.Entries.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "O pacote fornecido está vazio ou não pôde ser interpretado.");
                return;
            }

            var filterList = new List<string>();
            DA.GetDataList(1, filterList);
            var filterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in filterList)
            {
                if (!string.IsNullOrWhiteSpace(f))
                    filterSet.Add(f.Trim());
            }

            var outKeys = new List<string>();
            var outTree = new GH_Structure<IGH_Goo>();
            var sb = new StringBuilder();

            sb.AppendLine($"=== PILL BUNDLE UNPACKED [{bundle.Namespace}] ===");
            sbSummary(bundle, sb);

            int pathIdx = 0;
            foreach (var kvp in bundle.Entries)
            {
                string key = kvp.Key;

                // Suporte a filtro por chave exata, chave limpa ou grupo/categoria (ex: 'ACU', 'GEO')
                if (filterSet.Count > 0 && !filterSet.Contains("*") && !filterSet.Contains("ALL"))
                {
                    string cleanKey = PillHub.CleanUpKey(key);
                    PillHub.ParseKeyMetadata(key, null, out _, out string category, out _, out _);

                    bool matched = false;
                    foreach (var f in filterSet)
                    {
                        if (key.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                            cleanKey.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                            category.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                            category.Equals(PillHub.NormalizeCategory(f), StringComparison.OrdinalIgnoreCase) ||
                            cleanKey.StartsWith(f + "_", StringComparison.OrdinalIgnoreCase) ||
                            cleanKey.StartsWith(f + "::", StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                            break;
                        }
                    }

                    if (!matched) continue;
                }

                outKeys.Add(key);
                var path = new GH_Path(pathIdx);

                string unit = bundle.Units != null && bundle.Units.TryGetValue(key, out var u) ? u : "";
                object val = kvp.Value;

                if (val is System.Collections.IEnumerable enumerable && !(val is string))
                {
                    int itCount = 0;
                    foreach (var item in enumerable)
                    {
                        var goo = WrapGoo(item);
                        outTree.Append(goo, path);
                        itCount++;
                    }
                    sb.AppendLine($"[{pathIdx}] {key} {(string.IsNullOrEmpty(unit) ? "" : "[" + unit + "]")} -> {itCount} itens");
                }
                else
                {
                    var goo = WrapGoo(val);
                    outTree.Append(goo, path);
                    sb.AppendLine($"[{pathIdx}] {key} {(string.IsNullOrEmpty(unit) ? "" : "[" + unit + "]")} = {val}");
                }

                pathIdx++;
            }

            DA.SetDataList(0, outKeys);
            DA.SetDataTree(1, outTree);
            DA.SetData(2, sb.ToString());

            Message = $"{outKeys.Count} Params";
        }

        
        private static IGH_Goo WrapGoo(object obj)
        {
            if (obj == null) return null;
            if (obj is IGH_Goo goo) return goo;
            var ghGoo = GH_Convert.ToGoo(obj);
            if (ghGoo != null) return ghGoo;
            if (obj is double d) return new GH_Number(d);
            if (obj is int i) return new GH_Integer(i);
            if (obj is bool b) return new GH_Boolean(b);
            if (obj is string s)
            {
                if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedD))
                    return new GH_Number(parsedD);
                return new GH_String(s);
            }
            return new GH_ObjectWrapper(obj);
        }

        private static void sbSummary(PillBundle b, StringBuilder sb)
        {
            sb.AppendLine($"Data do Pacote: {b.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Disponível: {b.Entries.Count} parâmetros");
            sb.AppendLine("-------------------------------------------------");
        }
    }
}
