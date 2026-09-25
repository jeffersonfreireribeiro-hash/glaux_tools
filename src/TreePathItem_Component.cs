using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Tree Path Item (Dimensional Slicer)
    /// Fatia e seleciona ramos de uma Árvore de Dados (DataTree) por dimensões de caminho {a;b;c...},
    /// funcionando como um List Item multidimensional para árvores do Grasshopper.
    /// Gera dinamicamente entradas para cada nível de ramificação ({0}, {1}, {2}...).
    /// Se uma dimensão ficar vazia, inclui todos os ramos (*) filtrando apenas pelas dimensões especificadas.
    /// </summary>
    public class TreePathItem_Component : GH_Component, IGH_VariableParameterComponent
    {
        private bool _autoSyncDimensions = true;
        private bool _simplifySlicedPaths = false;
        private bool _wrapNegativeIndices = true;
        private bool _isSyncing = false;

        public TreePathItem_Component()
            : base(
                "Tree Path Item",
                "PathItem",
                "Fatia e seleciona ramos de uma Árvore de Dados (DataTree) por dimensões de caminho {a;b;c...}, similar a um List Item multidimensional. Gera dinamicamente entradas para cada nível de ramificação ({0}, {1}, {2}...). Se uma dimensão for omitida, seleciona todos os ramos (*) filtrando pelas demais.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("5b6c7d8e-9f0a-1b2c-3d4e-5f6a7b8c9d13");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados de entrada a ser fatiada/selecionada.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Index {0}", "i0", "Índice(s) desejado(s) para a 1ª dimensão do caminho {x;..}. Vazio = Todos (*). Suporta negativos (-1 = último) e listas.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Index {1}", "i1", "Índice(s) desejado(s) para a 2ª dimensão do caminho {..;x;..}. Vazio = Todos (*). Suporta negativos (-1 = último) e listas.", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Selected Tree", "T", "Árvore de dados contendo apenas os ramos selecionados.", GH_ParamAccess.tree);
            pManager.AddPathParameter("Selected Paths", "P", "Lista dos caminhos (GH_Path) dos ramos selecionados.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Remainder Tree", "R", "Árvore de dados com os ramos descartados / não selecionados (resto/inverso).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Dimension Summary", "I", "Resumo dos índices únicos existentes em cada dimensão da árvore de entrada.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Branch Count", "N", "Quantidade de ramos selecionados.", GH_ParamAccess.item);
        }

        private class DimensionFilter
        {
            public bool IsWildcard { get; set; } = true;
            public HashSet<int> AllowedSet { get; } = new HashSet<int>();
            public List<int> DistinctAvailable { get; set; } = new List<int>();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            if (inTree.PathCount == 0)
            {
                this.Message = "Árvore Vazia";
                DA.SetDataTree(0, new GH_Structure<IGH_Goo>());
                DA.SetDataList(1, new List<GH_Path>());
                DA.SetDataTree(2, new GH_Structure<IGH_Goo>());
                DA.SetDataList(3, new List<string>());
                DA.SetData(4, 0);
                return;
            }

            int maxDepth = inTree.Paths.Max(p => p.Length);
            int currentDimCount = Params.Input.Count - 1;

            // Auto-sincronizar entradas se a profundidade da árvore for diferente
            if (_autoSyncDimensions && maxDepth > 0 && maxDepth != currentDimCount && !_isSyncing)
            {
                ScheduleDimensionSync(maxDepth);
            }

            int activeDims = Math.Min(currentDimCount, maxDepth);
            var dimensionFilters = new List<DimensionFilter>();
            var summaryList = new List<string>();

            for (int d = 0; d < currentDimCount; d++)
            {
                var filter = new DimensionFilter();

                // Índices reais existentes nesta dimensão na árvore de entrada
                var distinct = inTree.Paths
                    .Where(p => p.Length > d)
                    .Select(p => p[d])
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                filter.DistinctAvailable = distinct;

                string distinctStr = distinct.Count <= 10
                    ? string.Join(", ", distinct)
                    : $"{string.Join(", ", distinct.Take(8))}... (+{distinct.Count - 8})";

                summaryList.Add($"Dim {{{d}}}: [{distinctStr}] (Total: {distinct.Count} índices)");

                // Ler entrada do usuário para esta dimensão
                var rawList = new List<object>();
                DA.GetDataList(d + 1, rawList);

                if (rawList.Count == 0)
                {
                    filter.IsWildcard = true;
                }
                else
                {
                    filter.IsWildcard = false;
                    foreach (var item in rawList)
                    {
                        ParseIndexInput(item, distinct, filter.AllowedSet, _wrapNegativeIndices);
                    }

                    // Se o usuário passou apenas filtros inválidos ou vazios, trata como wildcard
                    if (filter.AllowedSet.Count == 0 && rawList.Count > 0)
                    {
                        filter.IsWildcard = true;
                    }
                }

                dimensionFilters.Add(filter);
            }

            // Filtrar ramos
            var outTree = new GH_Structure<IGH_Goo>();
            var remainderTree = new GH_Structure<IGH_Goo>();
            var selectedPathsList = new List<GH_Path>();

            foreach (var path in inTree.Paths)
            {
                bool isMatch = true;

                for (int d = 0; d < path.Length && d < dimensionFilters.Count; d++)
                {
                    var f = dimensionFilters[d];
                    if (!f.IsWildcard && !f.AllowedSet.Contains(path[d]))
                    {
                        isMatch = false;
                        break;
                    }
                }

                var branchData = inTree[path];

                if (isMatch)
                {
                    GH_Path targetPath = path;

                    // Simplificar caminho se ativado: remove dimensões que foram fixadas em um único índice
                    if (_simplifySlicedPaths)
                    {
                        var newIÍndices = new List<int>();
                        for (int d = 0; d < path.Length; d++)
                        {
                            if (d < dimensionFilters.Count && !dimensionFilters[d].IsWildcard && dimensionFilters[d].AllowedSet.Count == 1)
                            {
                                // Dimensão fixada -> suprime para reduzir dimensionalidade (estilo slice multidimensional)
                                continue;
                            }
                            newIÍndices.Add(path[d]);
                        }
                        targetPath = newIÍndices.Count > 0 ? new GH_Path(newIÍndices.ToArray()) : new GH_Path(0);
                    }

                    outTree.AppendRange(branchData, targetPath);
                    selectedPathsList.Add(path);
                }
                else
                {
                    remainderTree.AppendRange(branchData, path);
                }
            }

            int selectedCount = selectedPathsList.Count;
            int totalBranches = inTree.PathCount;
            this.Message = $"{selectedCount}/{totalBranches} Ramos\n({maxDepth}D)";

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, selectedPathsList);
            DA.SetDataTree(2, remainderTree);
            DA.SetDataList(3, summaryList);
            DA.SetData(4, selectedCount);
        }

        private static void ParseIndexInput(object raw, List<int> distinctAvailable, HashSet<int> targetSet, bool wrapNegatives)
        {
            if (raw == null) return;

            object val = raw is IGH_Goo goo ? goo.SafeScriptVariable() : raw;

            // Se for string, suporta números ("2"), ranges ("1..4") ou curinga ("*")
            if (val is string str)
            {
                string s = str.Trim();
                if (s == "*" || s.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var idx in distinctAvailable) targetSet.Add(idx);
                    return;
                }

                if (s.Contains(".."))
                {
                    var parts = s.Split(new string[] { ".." }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int start) && int.TryParse(parts[1].Trim(), out int end))
                    {
                        for (int k = Math.Min(start, end); k <= Math.Max(start, end); k++)
                        {
                            targetSet.Add(k);
                        }
                        return;
                    }
                }

                if (s.Contains(","))
                {
                    var subParts = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sp in subParts)
                    {
                        ParseIndexInput(sp.Trim(), distinctAvailable, targetSet, wrapNegatives);
                    }
                    return;
                }

                if (int.TryParse(s, out int intVal))
                {
                    AddIntegerWithWrap(intVal, distinctAvailable, targetSet, wrapNegatives);
                    return;
                }
            }

            if (val is int i)
            {
                AddIntegerWithWrap(i, distinctAvailable, targetSet, wrapNegatives);
                return;
            }

            if (val is double d)
            {
                AddIntegerWithWrap((int)Math.Round(d), distinctAvailable, targetSet, wrapNegatives);
                return;
            }

            try
            {
                int converted = Convert.ToInt32(val);
                AddIntegerWithWrap(converted, distinctAvailable, targetSet, wrapNegatives);
            }
            catch { }
        }

        private static void AddIntegerWithWrap(int val, List<int> distinctAvailable, HashSet<int> targetSet, bool wrapNegatives)
        {
            if (val >= 0)
            {
                targetSet.Add(val);
                return;
            }

            // Índice negativo (ex: -1 = último índice existente nesta dimensão)
            if (wrapNegatives && distinctAvailable.Count > 0)
            {
                int count = distinctAvailable.Count;
                int wrappedIndex = (count + (val % count)) % count;
                targetSet.Add(distinctAvailable[wrappedIndex]);
            }
            else
            {
                targetSet.Add(val);
            }
        }

        private void ScheduleDimensionSync(int targetDepth)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            var doc = OnPingDocument();
            if (doc == null)
            {
                _isSyncing = false;
                return;
            }

            doc.ScheduleSolution(1, d =>
            {
                try
                {
                    AdjustDimensionInputs(targetDepth);
                }
                finally
                {
                    _isSyncing = false;
                }
            });
        }

        private void AdjustDimensionInputs(int targetDepth)
        {
            if (targetDepth <= 0 || targetDepth > 20) return;

            RecordUndoEvent("Sincronizar Dimensões da Árvore");

            int currentCount = Params.Input.Count - 1;

            // Se precisamos adicionar parâmetros
            while (Params.Input.Count - 1 < targetDepth)
            {
                int dim = Params.Input.Count - 1;
                var p = new Param_GenericObject
                {
                    Name = $"Index {{{dim}}}",
                    NickName = $"i{dim}",
                    Description = $"Índice(s) desejado(s) para a dimensão {dim} do caminho. Vazio = Todos (*). Suporta negativos (-1 = último) e listas.",
                    Access = GH_ParamAccess.list,
                    Optional = true
                };
                Params.RegisterInputParam(p);
            }

            // Se precisamos remover parâmetros excedentes (apenas os que não tiverem conexões)
            while (Params.Input.Count - 1 > targetDepth && Params.Input.Count > 2)
            {
                int lastIdx = Params.Input.Count - 1;
                if (Params.Input[lastIdx].SourceCount == 0)
                {
                    Params.UnregisterInputParameter(Params.Input[lastIdx], true);
                }
                else
                {
                    break;
                }
            }

            Params.OnParametersChanged();
            ExpireSolution(false);
        }

        #region IGH_VariableParameterComponent Implementation

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input && index > 0;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input && index > 1 && Params.Input.Count > 2;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            int dim = index - 1;
            return new Param_GenericObject
            {
                Name = $"Index {{{dim}}}",
                NickName = $"i{dim}",
                Description = $"Índice(s) desejado(s) para a dimensão {dim} do caminho. Vazio = Todos (*).",
                Access = GH_ParamAccess.list,
                Optional = true
            };
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            for (int i = 1; i < Params.Input.Count; i++)
            {
                int dim = i - 1;
                Params.Input[i].Name = $"Index {{{dim}}}";
                Params.Input[i].NickName = $"i{dim}";
                Params.Input[i].Description = $"Índice(s) desejado(s) para a dimensão {dim} do caminho. Vazio = Todos (*). Suporta negativos (-1) e listas.";
                Params.Input[i].Access = GH_ParamAccess.list;
                Params.Input[i].Optional = true;
            }
        }

        #endregion

        #region Menu & Serialization

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var autoSyncItem = new ToolStripMenuItem("Auto-Sincronizar com Profundidade da Árvore")
            {
                Checked = _autoSyncDimensions
            };
            autoSyncItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Auto-Sincronização");
                _autoSyncDimensions = !_autoSyncDimensions;
                ExpireSolution(true);
            };
            menu.Items.Add(autoSyncItem);

            var simplifyItem = new ToolStripMenuItem("Simplificar Caminhos Fatiados (Remover Dimensões Fixadas)")
            {
                Checked = _simplifySlicedPaths
            };
            simplifyItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Simplificação de Caminhos");
                _simplifySlicedPaths = !_simplifySlicedPaths;
                ExpireSolution(true);
            };
            menu.Items.Add(simplifyItem);

            var wrapItem = new ToolStripMenuItem("Suportar Índices Negativos (-1 = Úúltimo)")
            {
                Checked = _wrapNegativeIndices
            };
            wrapItem.Click += (s, e) =>
            {
                RecordUndoEvent("Alternar Índices Negativos");
                _wrapNegativeIndices = !_wrapNegativeIndices;
                ExpireSolution(true);
            };
            menu.Items.Add(wrapItem);

            menu.Items.Add(new ToolStripSeparator());

            var resetParamsItem = new ToolStripMenuItem("Redefinir Entradas de Dimensão...");
            resetParamsItem.Click += (s, e) =>
            {
                RecordUndoEvent("Redefinir Entradas");
                while (Params.Input.Count > 3)
                {
                    Params.UnregisterInputParameter(Params.Input[Params.Input.Count - 1], true);
                }
                Params.OnParametersChanged();
                ExpireSolution(true);
            };
            menu.Items.Add(resetParamsItem);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean("AutoSyncDimensions", _autoSyncDimensions);
            writer.SetBoolean("SimplifySlicedPaths", _simplifySlicedPaths);
            writer.SetBoolean("WrapNegativeIÍndices", _wrapNegativeIndices);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("AutoSyncDimensions"))
                _autoSyncDimensions = reader.GetBoolean("AutoSyncDimensions");
            if (reader.ItemExists("SimplifySlicedPaths"))
                _simplifySlicedPaths = reader.GetBoolean("SimplifySlicedPaths");
            if (reader.ItemExists("WrapNegativeIÍndices"))
                _wrapNegativeIndices = reader.GetBoolean("WrapNegativeIÍndices");

            return base.Read(reader);
        }

        #endregion

        protected override Bitmap Icon => GlauxToolsIcons.TreePathItem;
    }
}
