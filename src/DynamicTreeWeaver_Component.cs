using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class DynamicTreeWeaver_Component : GH_Component
    {
        public DynamicTreeWeaver_Component()
            : base(
                "Dynamic Tree Weaver",
                "TreeWeave",
                "Intercala dinamicamente fluxos e ramos de árvores assimétricas respeitando padrões e chaves personalizadas sem a rigidez do Weave nativo.",
                "Glaux Tools",
                "Tree")
        {
        }

        public override Guid ComponentGuid => new Guid("2c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Stream 0", "S0", "Primeiro fluxo de dados / árvore (Stream 0).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Stream 1", "S1", "Segundo fluxo de dados / árvore (Stream 1).", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Stream 2", "S2", "Terceiro fluxo opcional de dados / árvore (Stream 2).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Pattern", "P", "Padrão de intercalação (ex.: 0, 1, 0, 2).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Mode", "M", "Modo de intercalação:\n0 = Por item dentro de cada caminho coincidente\n1 = Por ramo completo\n2 = Aplanar fluxos e tecer globalmente", GH_ParamAccess.item, 0);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Weaved Tree", "T", "Árvore de dados tecida e combinada dinamicamente.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Paths", "P", "Lista dos caminhos gerados.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var s0 = new GH_Structure<IGH_Goo>();
            var s1 = new GH_Structure<IGH_Goo>();
            var s2 = new GH_Structure<IGH_Goo>();

            DA.GetDataTree(0, out s0);
            DA.GetDataTree(1, out s1);
            DA.GetDataTree(2, out s2);

            var streams = new List<GH_Structure<IGH_Goo>>();
            if (s0 != null && s0.PathCount > 0) streams.Add(s0);
            if (s1 != null && s1.PathCount > 0) streams.Add(s1);
            if (s2 != null && s2.PathCount > 0) streams.Add(s2);

            if (streams.Count == 0)
            {
                this.Message = "Sem Fluxos";
                return;
            }

            var pattern = new List<int>();
            if (!DA.GetDataList(3, pattern) || pattern == null || pattern.Count == 0)
            {
                for (int i = 0; i < streams.Count; i++) pattern.Add(i);
            }

            int mode = 0;
            DA.GetData(4, ref mode);

            var outTree = new GH_Structure<IGH_Goo>();
            var pathList = new List<string>();

            if (mode == 0) // Por Item dentro de caminhos coincidentes
            {
                var allPaths = new HashSet<GH_Path>();
                foreach (var st in streams)
                {
                    foreach (var p in st.Paths) allPaths.Add(p);
                }

                var sortedPaths = allPaths.OrderBy(p => p).ToList();

                foreach (GH_Path path in sortedPaths)
                {
                    var branchCursors = new int[streams.Count];
                    int maxLen = 0;

                    for (int s = 0; s < streams.Count; s++)
                    {
                        if (streams[s].PathExists(path))
                        {
                            int len = streams[s].get_Branch(path).Count;
                            if (len > maxLen) maxLen = len;
                        }
                    }

                    int patIdx = 0;
                    int totalExpected = maxLen * streams.Count;

                    outTree.EnsurePath(path);

                    for (int step = 0; step < totalExpected; step++)
                    {
                        int targetStream = pattern[patIdx % pattern.Count];
                        patIdx++;

                        if (targetStream >= 0 && targetStream < streams.Count)
                        {
                            var st = streams[targetStream];
                            if (st.PathExists(path))
                            {
                                var branch = st.get_Branch(path);
                                int c = branchCursors[targetStream];
                                if (c < branch.Count)
                                {
                                    outTree.Append((IGH_Goo)branch[c], path);
                                    branchCursors[targetStream]++;
                                }
                            }
                        }

                        bool allDone = true;
                        for (int s = 0; s < streams.Count; s++)
                        {
                            if (streams[s].PathExists(path))
                            {
                                if (branchCursors[s] < streams[s].get_Branch(path).Count)
                                {
                                    allDone = false;
                                    break;
                                }
                            }
                        }
                        if (allDone) break;
                    }

                    pathList.Add(path.ToString());
                }
            }
            else if (mode == 1) // Por Ramo completo
            {
                int maxPaths = streams.Max(st => st.PathCount);
                int outPathIdx = 0;

                for (int pIdx = 0; pIdx < maxPaths; pIdx++)
                {
                    for (int pat = 0; pat < pattern.Count; pat++)
                    {
                        int sIdx = pattern[pat];
                        if (sIdx >= 0 && sIdx < streams.Count)
                        {
                            var st = streams[sIdx];
                            if (pIdx < st.PathCount)
                            {
                                var srcPath = st.Paths[pIdx];
                                var branch = st.get_Branch(srcPath);

                                GH_Path targetPath = new GH_Path(outPathIdx);
                                outTree.EnsurePath(targetPath);
                                for (int i = 0; i < branch.Count; i++) outTree.Append((IGH_Goo)branch[i], targetPath);

                                pathList.Add(targetPath.ToString());
                                outPathIdx++;
                            }
                        }
                    }
                }
            }
            else // Modo 2: Global Flatten & Weave
            {
                var flatStreams = new List<List<IGH_Goo>>();
                for (int s = 0; s < streams.Count; s++)
                {
                    flatStreams.Add(streams[s].AllData(true).ToList());
                }

                int totalItems = flatStreams.Sum(l => l.Count);
                var cursors = new int[streams.Count];
                int patIdx = 0;
                GH_Path p0 = new GH_Path(0);
                outTree.EnsurePath(p0);

                while (outTree.DataCount < totalItems)
                {
                    int targetStream = pattern[patIdx % pattern.Count];
                    patIdx++;

                    if (targetStream >= 0 && targetStream < streams.Count)
                    {
                        var list = flatStreams[targetStream];
                        int c = cursors[targetStream];
                        if (c < list.Count)
                        {
                            outTree.Append(list[c], p0);
                            cursors[targetStream]++;
                        }
                    }

                    bool allDone = true;
                    for (int s = 0; s < streams.Count; s++)
                    {
                        if (cursors[s] < flatStreams[s].Count) { allDone = false; break; }
                    }
                    if (allDone) break;
                }
                pathList.Add(p0.ToString());
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, pathList);

            this.Message = $"{outTree.DataCount} itens tecidos\n{outTree.PathCount} ramos";
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.DynamicTreeWeaver;
    }
}
