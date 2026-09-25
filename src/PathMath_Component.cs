using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class PathMath_Component : GH_Component
    {
        public PathMath_Component()
            : base(
                "Path Math (Index Arithmetic)",
                "PathMath",
                "Modifica diretamente a estrutura numérica dos caminhos GH_Path: soma offsets a níveis específicos ({0; i+1}), inverte hierarquias, rotaciona níveis ou colapsa profundidades intermediárias.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("4e5f6a7b-8c9d-0e1f-2a3b-4c5d6e7f8a9b");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Árvore de dados a ter seus caminhos manipulados.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Operation", "Op", "Operação aritmética no Path:\n0 = Somar Offset no nível L ({..., i + V, ...})\n1 = Inverter Hierarquia ({A; B; C} -> {C; B; A})\n2 = Rotacionar Níveis / Shift ({A; B; C} -> {B; C; A})\n3 = Colapsar / Remover Nível L (remove o índice do nível)\n4 = Inserir Nível (insere novo índice de valor V na posição L)\n5 = Truncar Profundidade (manter apenas os primeiros V níveis)", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Target Level", "L", "Profundidade / Nível alvo (0 = primeiro nível, -1 = último nível, -2 = penúltimo nível). Padrão: -1.", GH_ParamAccess.item, -1);
            pManager.AddIntegerParameter("Value / Offset", "V", "Valor numérico para soma, inserção ou contagem de níveis (padrão: 1).", GH_ParamAccess.item, 1);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Modified Tree", "T", "Árvore de dados com os novos caminhos calculados.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Old Paths", "P_old", "Lista dos caminhos originais da árvore.", GH_ParamAccess.list);
            pManager.AddTextParameter("New Paths", "P_new", "Lista dos novos caminhos gerados.", GH_ParamAccess.list);
            pManager.AddTextParameter("Path Map", "Map", "Mapeamento das transformações {antigo} -> {novo}.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) || inTree == null)
            {
                this.Message = "Sem Árvore";
                return;
            }

            int op = 0;
            DA.GetData(1, ref op);

            int targetLevel = -1;
            DA.GetData(2, ref targetLevel);

            int val = 1;
            DA.GetData(3, ref val);

            var outTree = new GH_Structure<IGH_Goo>();
            var oldPaths = new List<string>();
            var newPaths = new List<string>();
            var pathMap = new List<string>();

            foreach (GH_Path path in inTree.Paths)
            {
                int[] indices = path.Indices;
                int len = indices.Length;
                if (len == 0) indices = new int[] { 0 };

                int resolvedLevel = targetLevel;
                if (resolvedLevel < 0) resolvedLevel = len + resolvedLevel;
                if (resolvedLevel < 0) resolvedLevel = 0;
                if (resolvedLevel >= len && op != 4) resolvedLevel = len - 1;

                int[] newIndices;

                switch (op)
                {
                    case 0: // Offset Index
                        {
                            newIndices = (int[])indices.Clone();
                            newIndices[resolvedLevel] = Math.Max(0, newIndices[resolvedLevel] + val);
                        }
                        break;

                    case 1: // Inverter Hierarquia
                        {
                            newIndices = indices.Reverse().ToArray();
                        }
                        break;

                    case 2: // Shift / Rotacionar Níveis
                        {
                            newIndices = new int[len];
                            int shift = ((val % len) + len) % len;
                            for (int i = 0; i < len; i++)
                            {
                                newIndices[(i + shift) % len] = indices[i];
                            }
                        }
                        break;

                    case 3: // Colapsar / Remover Nível L
                        {
                            if (len <= 1)
                            {
                                newIndices = new int[] { 0 };
                            }
                            else
                            {
                                var list = indices.ToList();
                                list.RemoveAt(resolvedLevel);
                                newIndices = list.ToArray();
                            }
                        }
                        break;

                    case 4: // Inserir Nível
                        {
                            var list = indices.ToList();
                            int insertPos = Math.Max(0, Math.Min(list.Count, resolvedLevel));
                            list.Insert(insertPos, val);
                            newIndices = list.ToArray();
                        }
                        break;

                    case 5: // Truncar Profundidade
                        {
                            int take = Math.Max(1, Math.Min(len, val));
                            newIndices = indices.Take(take).ToArray();
                        }
                        break;

                    default:
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Operação inválida: {op}. Escolha entre 0 e 5.");
                        return;
                }

                GH_Path targetPath = new GH_Path(newIndices);

                var branch = inTree.get_Branch(path);
                outTree.EnsurePath(targetPath);
                for (int i = 0; i < branch.Count; i++)
                {
                    outTree.Append((IGH_Goo)branch[i], targetPath);
                }

                oldPaths.Add(path.ToString());
                newPaths.Add(targetPath.ToString());
                pathMap.Add($"{path} -> {targetPath}");
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, oldPaths);
            DA.SetDataList(2, newPaths);
            DA.SetDataList(3, pathMap);

            string opName = op switch
            {
                0 => $"Offset L{targetLevel} (+{val})",
                1 => "Invert Hierarchy",
                2 => $"Shift ({val})",
                3 => $"Collapse L{targetLevel}",
                4 => $"Insert L{targetLevel}",
                5 => $"Truncate ({val})",
                _ => "PathMath"
            };

            this.Message = $"{opName}\n{outTree.PathCount} ramos";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.PathMath;
    }
}
