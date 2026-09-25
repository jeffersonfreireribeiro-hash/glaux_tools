using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PathDictionary_Component : GH_Component
    {
        public PathDictionary_Component()
            : base(
                "Path Dictionary (Remap & Lookup)",
                "PathDict",
                "Remapeia, consulta e indexa caminhos de DataTrees utilizando dicionários chave-valor O(1).\n" +
                "- Permite traduzir caminhos arbitrários (ex: {5;4} -> {0}).\n" +
                "- Aceita listas de chaves/valores ou pares em texto ('{5;4} -> {0}').\n" +
                "- Modo Auto-Index: se K e V forem vazios, renumera automaticamente todos os caminhos existentes para {0}, {1}, ... {N-1}.\n" +
                "- Suporta filtrar ramos não encontrados no dicionário ou mantê-los inalterados.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("2c8e336c-6ff4-4cb9-a534-8ab94fbace8c");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados cujos caminhos serão remapeados ou consultados.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Keys (From)", "K", "Chaves de origem. Aceita caminhos GH_Path, texto tipo '{5;4}', ou pares tipo '{5;4} -> {0}'. Se vazio, indexa automaticamente todos os caminhos.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddGenericParameter("Values (To)", "V", "Novos caminhos de destino. Aceita caminhos GH_Path, inteiros ou texto. Opcional se K contiver pares 'De -> Para' ou se estiver usando modo auto-indexador.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddIntegerParameter("Unmatched Action", "U", "Ação para caminhos não cadastrados no dicionário:\n0 = Manter Original\n1 = Descartar / Filtrar (Drop)\n2 = Enviar para Caminho Padrão", GH_ParamAccess.item, 0);
            pManager[3].Optional = true;
            pManager.AddGenericParameter("Default Path", "Def", "Caminho padrão quando U = 2. Padrão: {999}.", GH_ParamAccess.item);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Remapped Tree", "T", "Árvore de dados com os caminhos remapeados pelo dicionário.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Dictionary Map", "Dict", "Tabela / Mapeamento do dicionário ({Origem} -> {Destino}).", GH_ParamAccess.list);
            pManager.AddGenericParameter("Unmatched Tree", "Un", "Árvore contendo os ramos que não foram encontrados no dicionário.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Keys Count", "N_keys", "Quantidade total de chaves cadastradas no dicionário.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Matched Count", "N_match", "Quantidade de ramos que foram mapeados com sucesso.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            var rawKeys = new List<object>();
            DA.GetDataList(1, rawKeys);

            var rawValues = new List<object>();
            DA.GetDataList(2, rawValues);

            int unmatchedAction = 0;
            DA.GetData(3, ref unmatchedAction);

            GH_Path defaultPath = new GH_Path(999);
            object rawDef = null;
            if (DA.GetData(4, ref rawDef) && rawDef != null)
            {
                if (TryParsePath(rawDef, out GH_Path parsedDef))
                    defaultPath = parsedDef;
            }

            // Construir Dicionário Chave -> Destino
            var dict = new Dictionary<string, GH_Path>(StringComparer.OrdinalIgnoreCase);
            var dictLog = new List<string>();

            // Modo 1: K e V vazios -> Auto-Indexador sequencial
            if ((rawKeys == null || rawKeys.Count == 0) && (rawValues == null || rawValues.Count == 0))
            {
                int seq = 0;
                foreach (GH_Path p in inTree.Paths)
                {
                    string kStr = NormalizePathString(p.ToString());
                    if (!dict.ContainsKey(kStr))
                    {
                        var target = new GH_Path(seq);
                        dict[kStr] = target;
                        dictLog.Add($"{p} -> {target}");
                        seq++;
                    }
                }
            }
            else
            {
                // Processar entradas fornecidas
                bool hasValuesList = (rawValues != null && rawValues.Count > 0);

                for (int i = 0; i < rawKeys.Count; i++)
                {
                    object kObj = rawKeys[i];
                    if (kObj == null) continue;

                    string kText = kObj.ToString().Trim();

                    // Verificar se é uma regra 'De -> Para' em string única (ex: "{5;4} -> {0}" ou "{5;4} : {0}" ou "{5;4} = {0}")
                    if (TrySplitPair(kText, out string leftKey, out string rightVal))
                    {
                        if (TryParsePath(leftKey, out GH_Path fromP) && TryParsePath(rightVal, out GH_Path toP))
                        {
                            string kNorm = NormalizePathString(fromP.ToString());
                            dict[kNorm] = toP;
                            dictLog.Add($"{fromP} -> {toP}");
                        }
                        continue;
                    }

                    // Se não for par em texto, usar pareamento com a lista V
                    if (TryParsePath(kObj, out GH_Path keyPath))
                    {
                        string kNorm = NormalizePathString(keyPath.ToString());

                        GH_Path valPath;
                        if (hasValuesList && i < rawValues.Count)
                        {
                            if (!TryParsePath(rawValues[i], out valPath))
                                valPath = new GH_Path(i);
                        }
                        else
                        {
                            // Se não forneceu V correspondente, usa o índice de ordem de K
                            valPath = new GH_Path(i);
                        }

                        dict[kNorm] = valPath;
                        dictLog.Add($"{keyPath} -> {valPath}");
                    }
                }
            }

            // Aplicar o dicionário na árvore
            var outTree = new GH_Structure<IGH_Goo>();
            var unTree = new GH_Structure<IGH_Goo>();
            int matchedCount = 0;

            foreach (GH_Path p in inTree.Paths)
            {
                string norm = NormalizePathString(p.ToString());
                var branch = inTree.get_Branch(p);

                if (dict.TryGetValue(norm, out GH_Path targetPath))
                {
                    matchedCount++;
                    outTree.EnsurePath(targetPath);
                    for (int i = 0; i < branch.Count; i++)
                    {
                        outTree.Append((IGH_Goo)branch[i], targetPath);
                    }
                }
                else
                {
                    // Não está no dicionário
                    unTree.EnsurePath(p);
                    for (int i = 0; i < branch.Count; i++)
                    {
                        unTree.Append((IGH_Goo)branch[i], p);
                    }

                    if (unmatchedAction == 0) // Keep Original
                    {
                        outTree.EnsurePath(p);
                        for (int i = 0; i < branch.Count; i++)
                        {
                            outTree.Append((IGH_Goo)branch[i], p);
                        }
                    }
                    else if (unmatchedAction == 2) // Default Path
                    {
                        outTree.EnsurePath(defaultPath);
                        for (int i = 0; i < branch.Count; i++)
                        {
                            outTree.Append((IGH_Goo)branch[i], defaultPath);
                        }
                    }
                    // Se unmatchedAction == 1 (Drop), simplesmente não adiciona em outTree
                }
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, dictLog);
            DA.SetDataTree(2, unTree);
            DA.SetData(3, dict.Count);
            DA.SetData(4, matchedCount);

            string modeDesc = dict.Count == 0 ? "Vazio" : $"{dict.Count} chaves";
            this.Message = $"PathDict\n{modeDesc} | {matchedCount} ok";
        }

        private static bool TrySplitPair(string text, out string left, out string right)
        {
            left = null;
            right = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] separators = new string[] { "->", "=>", ":", "=" };
            foreach (var sep in separators)
            {
                int idx = text.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0 && idx < text.Length - sep.Length)
                {
                    left = text.Substring(0, idx).Trim();
                    right = text.Substring(idx + sep.Length).Trim();
                    return true;
                }
            }
            return false;
        }

        private static bool TryParsePath(object obj, out GH_Path path)
        {
            path = null;
            if (obj == null) return false;

            if (obj is GH_Path gp)
            {
                path = new GH_Path(gp);
                return true;
            }

            if (obj is IGH_Goo goo)
            {
                if (goo is GH_String gs)
                    return TryParsePathString(gs.Value, out path);
                if (goo is GH_Integer gi)
                {
                    path = new GH_Path(gi.Value);
                    return true;
                }
                if (goo is GH_Number gn)
                {
                    path = new GH_Path((int)Math.Round(gn.Value));
                    return true;
                }
                string gooStr = goo.ToString();
                return TryParsePathString(gooStr, out path);
            }

            if (obj is int intVal)
            {
                path = new GH_Path(intVal);
                return true;
            }

            return TryParsePathString(obj.ToString(), out path);
        }

        private static bool TryParsePathString(string str, out GH_Path path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(str)) return false;

            str = str.Trim();
            if (str.StartsWith("{") && str.EndsWith("}"))
            {
                str = str.Substring(1, str.Length - 2).Trim();
            }

            if (string.IsNullOrEmpty(str))
            {
                path = new GH_Path(0);
                return true;
            }

            string[] tokens = str.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var indices = new List<int>();

            foreach (var t in tokens)
            {
                if (int.TryParse(t.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                {
                    indices.Add(val);
                }
                else
                {
                    return false;
                }
            }

            if (indices.Count > 0)
            {
                path = new GH_Path(indices.ToArray());
                return true;
            }

            return false;
        }

        private static string NormalizePathString(string pStr)
        {
            if (string.IsNullOrWhiteSpace(pStr)) return "{0}";
            pStr = pStr.Trim();
            if (!pStr.StartsWith("{")) pStr = "{" + pStr;
            if (!pStr.EndsWith("}")) pStr = pStr + "}";
            return pStr.Replace(" ", "");
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.PathDictionary;
    }
}
