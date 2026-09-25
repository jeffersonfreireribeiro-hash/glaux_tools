using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Tree Search / List Search
    /// Busca termos específicos, palavras-chave ou padrões regex em listas ou árvores de dados.
    /// Retorna os itens correspondentes, seus índices (IDs locais), caminhos, itens não correspondentes e máscara booleana.
    /// </summary>
    public class TreeSearch_Component : GH_Component
    {
        private bool _exactMatch = false;
        private bool _caseSensitive = false;
        private bool _regexMode = false;
        private bool _matchAllQueries = false;
        private bool _invertSelection = false;

        public TreeSearch_Component()
            : base(
                "Tree Search",
                "TreeSearch",
                "Busca termos específicos, palavras-chave ou padrões regex em listas ou árvores de dados. Retorna os itens correspondentes, seus índices (IDs locais), caminhos, itens descartados e máscara booleana.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c14");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore ou lista de dados onde a pesquisa será realizada.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Query", "Q", "Termo(s), palavra-chave ou padrão regex a ser buscado.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Exact Match", "E", "Se True, exige correspondência exata do texto inteiro. Se False, busca como substring (Contém).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Case Sensitive", "C", "Se True, diferencia maiúsculas de minúsculas.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Regex", "Rx", "Se True, trata a query como uma Expressão Regular (Regex).", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Matched Items", "M", "Itens que contêm o termo pesquisado (preservando estrutura de árvore).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Matched Indices", "iM", "Índices (IDs locais 0-indexados) dos itens encontrados dentro de seus respectivos ramos.", GH_ParamAccess.tree);
            pManager.AddPathParameter("Matched Paths", "P", "Caminhos (GH_Path) onde ocorreram correspondências.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Remainder Items", "NM", "Itens que NÃO atenderam à busca (resto/descartados).", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Match Mask", "B", "Máscara booleana (True para match, False para descarte) com a mesma estrutura da árvore de entrada.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Match Count", "N", "Quantidade total de itens encontrados em toda a árvore.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Dados";
                return;
            }

            var queryList = new List<string>();
            DA.GetDataList(1, queryList);

            bool exactParam = _exactMatch;
            if (DA.GetData(2, ref exactParam)) _exactMatch = exactParam;

            bool caseParam = _caseSensitive;
            if (DA.GetData(3, ref caseParam)) _caseSensitive = caseParam;

            bool rxParam = _regexMode;
            if (DA.GetData(4, ref rxParam)) _regexMode = rxParam;

            var activeQueries = queryList
                .Where(q => !string.IsNullOrEmpty(q))
                .ToList();

            var outMatched = new GH_Structure<IGH_Goo>();
            var outIndices = new GH_Structure<GH_Integer>();
            var outRemainder = new GH_Structure<IGH_Goo>();
            var outMask = new GH_Structure<GH_Boolean>();
            var matchedPathsList = new List<GH_Path>();

            int totalMatches = 0;
            int totalItems = 0;

            if (activeQueries.Count == 0)
            {
                this.Message = "Sem Termo";
                foreach (var path in inTree.Paths)
                {
                    var branch = inTree[path];
                    for (int i = 0; i < branch.Count; i++)
                    {
                        outMask.Append(new GH_Boolean(false), path);
                        outRemainder.Append(branch[i], path);
                    }
                }
                DA.SetDataTree(0, outMatched);
                DA.SetDataTree(1, outIndices);
                DA.SetDataList(2, matchedPathsList);
                DA.SetDataTree(3, outRemainder);
                DA.SetDataTree(4, outMask);
                DA.SetData(5, 0);
                return;
            }

            List<Regex> compiledRegexes = null;
            if (_regexMode)
            {
                compiledRegexes = new List<Regex>();
                var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                foreach (var q in activeQueries)
                {
                    try
                    {
                        compiledRegexes.Add(new Regex(q, options | RegexOptions.Compiled));
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Regex inválido '{q}': {ex.Message}");
                    }
                }
            }

            StringComparison comp = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var path in inTree.Paths)
            {
                var branch = inTree[path];
                bool branchHadMatch = false;

                for (int i = 0; i < branch.Count; i++)
                {
                    totalItems++;
                    var item = branch[i];
                    string itemStr = item == null ? "" : (item.SafeScriptVariable()?.ToString() ?? item.ToString());

                    bool isMatch;
                    if (_regexMode && compiledRegexes != null && compiledRegexes.Count > 0)
                    {
                        if (_matchAllQueries)
                            isMatch = compiledRegexes.All(rx => rx.IsMatch(itemStr));
                        else
                            isMatch = compiledRegexes.Any(rx => rx.IsMatch(itemStr));
                    }
                    else
                    {
                        if (_exactMatch)
                        {
                            if (_matchAllQueries)
                                isMatch = activeQueries.All(q => string.Equals(itemStr, q, comp));
                            else
                                isMatch = activeQueries.Any(q => string.Equals(itemStr, q, comp));
                        }
                        else
                        {
                            if (_matchAllQueries)
                                isMatch = activeQueries.All(q => itemStr.IndexOf(q, comp) >= 0);
                            else
                                isMatch = activeQueries.Any(q => itemStr.IndexOf(q, comp) >= 0);
                        }
                    }

                    if (_invertSelection)
                    {
                        isMatch = !isMatch;
                    }

                    outMask.Append(new GH_Boolean(isMatch), path);

                    if (isMatch)
                    {
                        outMatched.Append(item, path);
                        outIndices.Append(new GH_Integer(i), path);
                        totalMatches++;
                        branchHadMatch = true;
                    }
                    else
                    {
                        outRemainder.Append(item, path);
                    }
                }

                if (branchHadMatch)
                {
                    matchedPathsList.Add(path);
                }
            }

            this.Message = $"{totalMatches}/{totalItems} encontrados\n({(_exactMatch ? "Exato" : "Contém")})";

            DA.SetDataTree(0, outMatched);
            DA.SetDataTree(1, outIndices);
            DA.SetDataList(2, matchedPathsList);
            DA.SetDataTree(3, outRemainder);
            DA.SetDataTree(4, outMask);
            DA.SetData(5, totalMatches);
        }

        #region Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var exactItem = new ToolStripMenuItem("Correspondência Exata (Exact Match)")
            {
                Checked = _exactMatch
            };
            exactItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Correspondência Exata");
                _exactMatch = !_exactMatch;
                ExpireSolution(true);
            };
            menu.Items.Add(exactItem);

            var caseItem = new ToolStripMenuItem("Diferenciar Maiúsculas/Minúsculas (Case Sensitive)")
            {
                Checked = _caseSensitive
            };
            caseItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Case Sensitive");
                _caseSensitive = !_caseSensitive;
                ExpireSolution(true);
            };
            menu.Items.Add(caseItem);

            var regexItem = new ToolStripMenuItem("Modo Expressão Regular (Regex)")
            {
                Checked = _regexMode
            };
            regexItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Modo Regex");
                _regexMode = !_regexMode;
                ExpireSolution(true);
            };
            menu.Items.Add(regexItem);

            var matchAllItem = new ToolStripMenuItem("Exigir Todos os Termos (AND ao invés de OR)")
            {
                Checked = _matchAllQueries
            };
            matchAllItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Combinação de Termos");
                _matchAllQueries = !_matchAllQueries;
                ExpireSolution(true);
            };
            menu.Items.Add(matchAllItem);

            menu.Items.Add(new ToolStripSeparator());

            var invertItem = new ToolStripMenuItem("Inverter Seleção (NOT)")
            {
                Checked = _invertSelection
            };
            invertItem.Click += (s, e) =>
            {
                RecordUndoEvent("Inverter Seleção");
                _invertSelection = !_invertSelection;
                ExpireSolution(true);
            };
            menu.Items.Add(invertItem);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("ExactMatch", _exactMatch);
            writer.SetBoolean("CaseSensitive", _caseSensitive);
            writer.SetBoolean("RegexMode", _regexMode);
            writer.SetBoolean("MatchAllQueries", _matchAllQueries);
            writer.SetBoolean("InvertSelection", _invertSelection);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("ExactMatch")) _exactMatch = reader.GetBoolean("ExactMatch");
            if (reader.ItemExists("CaseSensitive")) _caseSensitive = reader.GetBoolean("CaseSensitive");
            if (reader.ItemExists("RegexMode")) _regexMode = reader.GetBoolean("RegexMode");
            if (reader.ItemExists("MatchAllQueries")) _matchAllQueries = reader.GetBoolean("MatchAllQueries");
            if (reader.ItemExists("InvertSelection")) _invertSelection = reader.GetBoolean("InvertSelection");
            return base.Read(reader);
        }

        #endregion

        protected override Bitmap Icon => GlauxToolsIcons.TreeSearch;
    }
}
