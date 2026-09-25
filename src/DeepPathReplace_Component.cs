using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class DeepPathReplace_Component : GH_Component
    {
        public DeepPathReplace_Component()
            : base(
                "Deep Path Replace (RegEx)",
                "PathRegEx",
                "Renomeia, reorganiza e modifica os caminhos GH_Path de uma árvore utilizando Expressões Regulares (RegEx), grupos de captura ($1, $2) e aritmética de índices.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("6c7d8e9f-0a1b-2c3d-4e5f-6a7b8c9d0e1f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados cujos caminhos serão renomeados ou reorganizados.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Pattern", "P", "Padrão de busca (RegEx ou template de caminho, ex.: \\{(\\d+);(\\d+)\\} ou {A;B;C}).", GH_ParamAccess.item, "\\{(\\d+);(\\d+)\\}");
            pManager.AddTextParameter("Replacement", "R", "Formato de substituição (ex.: {$2;$1}, {$1;$2+1}, {0;$1;$2}).", GH_ParamAccess.item, "{$2;$1}");

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados resultante com a nova estrutura de caminhos.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Path Map", "Map", "Mapeamento dos caminhos originais para os novos caminhos.", GH_ParamAccess.list);
            pManager.AddTextParameter("New Paths", "P", "Lista dos novos caminhos gerados.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            string pattern = "\\{(\\d+);(\\d+)\\}";
            DA.GetData(1, ref pattern);

            string replacement = "{$2;$1}";
            DA.GetData(2, ref replacement);

            string regexPattern = pattern;
            if (pattern.StartsWith("{") && pattern.EndsWith("}") && !pattern.Contains("("))
            {
                string inside = pattern.Substring(1, pattern.Length - 2);
                string[] parts = inside.Split(';');
                var regexParts = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    regexParts.Add("(\\d+)");
                }
                regexPattern = "\\{" + string.Join(";", regexParts) + "\\}";
            }

            Regex regex;
            try
            {
                regex = new Regex(regexPattern);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"RegEx inválido: {ex.Message}");
                return;
            }

            var outTree = new GH_Structure<IGH_Goo>();
            var pathMap = new List<string>();
            var newPaths = new List<string>();

            foreach (GH_Path oldPath in inTree.Paths)
            {
                string pathStr = oldPath.ToString();
                string replacedStr = regex.Replace(pathStr, match =>
                {
                    string res = replacement;
                    for (int g = 1; g < match.Groups.Count; g++)
                    {
                        string gVal = match.Groups[g].Value;
                        int.TryParse(gVal, out int numVal);

                        res = Regex.Replace(res, "\\$" + g + "\\s*([\\+\\-])\\s*(\\d+)", m =>
                        {
                            char op = m.Groups[1].Value[0];
                            int delta = int.Parse(m.Groups[2].Value);
                            int computed = (op == '+') ? (numVal + delta) : (numVal - delta);
                            if (computed < 0) computed = 0;
                            return computed.ToString();
                        });

                        res = res.Replace("$" + g, gVal);
                    }
                    return res;
                });

                GH_Path targetPath = ParsePath(replacedStr, oldPath);

                var branch = inTree.get_Branch(oldPath);
                outTree.EnsurePath(targetPath);
                for (int i = 0; i < branch.Count; i++)
                {
                    outTree.Append((IGH_Goo)branch[i], targetPath);
                }

                pathMap.Add($"{oldPath} -> {targetPath}");
                if (!newPaths.Contains(targetPath.ToString()))
                {
                    newPaths.Add(targetPath.ToString());
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, pathMap);
            DA.SetDataList(2, newPaths);

            this.Message = $"{outTree.PathCount} caminhos\nRegEx OK";
        }

        private static GH_Path ParsePath(string str, GH_Path fallback)
        {
            try
            {
                string clean = str.Trim();
                if (clean.StartsWith("{") && clean.EndsWith("}"))
                {
                    clean = clean.Substring(1, clean.Length - 2);
                }
                string[] parts = clean.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var indices = new List<int>();
                foreach (var p in parts)
                {
                    if (int.TryParse(p.Trim(), out int val))
                    {
                        indices.Add(val);
                    }
                }
                if (indices.Count > 0)
                {
                    return new GH_Path(indices.ToArray());
                }
            }
            catch
            {
            }
            return fallback;
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DeepPathReplace;
    }
}
