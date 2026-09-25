using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    public class IterativeAccumulator_Component : GH_Component
    {
        private readonly List<GH_Structure<IGH_Goo>> _historyBuffer = new List<GH_Structure<IGH_Goo>>();
        private readonly List<DateTime> _timestamps = new List<DateTime>();
        private bool _lastRecordSignal = false;
        private bool _lastExportSignal = false;

        public IterativeAccumulator_Component()
            : base(
                "Iterative Accumulator (Data Logger)",
                "Accumulator",
                "Buffer circular de histórico para processos iterativos e loops. Grava os últimos K estados de uma árvore sem perda de caminhos e com suporte a Pause, Reset e Exportação JSON/CSV.",
                "Glaux Tools",
                "Automation")
        {
        }

        public override Guid ComponentGuid => new Guid("3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados a ser gravada no histórico a cada iteração.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Record / Step", "Rec", "Pulso ou sinal booleano para gravar o estado atual.", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Buffer Size K", "K", "Capacidade máxima do buffer circular (ex.: 50, 100, 500). Use 0 para ilimitado.", GH_ParamAccess.item, 100);
            pManager.AddBooleanParameter("Pause", "P", "Se True, pausa a gravação mantendo os dados no buffer.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Reset", "R", "Limpa todo o histórico gravado.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Export Path", "Path", "Caminho opcional de arquivo para exportar o histórico (.json ou .csv).", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Export Now", "Exp", "Gatilho para disparar a exportação do arquivo.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("History Tree", "H", "Árvore cronológica completa onde cada ramo raiz {iter; ...} representa um estado gravado.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Count", "N", "Total de iterações armazenadas atualmente no buffer.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status / Log", "Log", "Relatório de ocupação do buffer e status de exportação.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool reset = false;
            DA.GetData(4, ref reset);
            if (reset)
            {
                _historyBuffer.Clear();
                _timestamps.Clear();
            }

            bool record = true;
            DA.GetData(1, ref record);

            int bufferSize = 100;
            DA.GetData(2, ref bufferSize);

            bool pause = false;
            DA.GetData(3, ref pause);

            string exportPath = "";
            DA.GetData(5, ref exportPath);

            bool exportNow = false;
            DA.GetData(6, ref exportNow);

            bool hasData = DA.GetDataTree(0, out GH_Structure<IGH_Goo> inTree) && inTree != null;

            // Gravação em Borda de Subida ou Estado Ativo
            bool triggerRecord = record && !_lastRecordSignal;
            if (triggerRecord && !pause && hasData)
            {
                _historyBuffer.Add(inTree.Duplicate());
                _timestamps.Add(DateTime.Now);

                // Manter limite circular K
                if (bufferSize > 0 && _historyBuffer.Count > bufferSize)
                {
                    _historyBuffer.RemoveAt(0);
                    _timestamps.RemoveAt(0);
                }
            }
            _lastRecordSignal = record;

            // Exportação para JSON/CSV
            string exportMsg = "Nenhum arquivo exportado";
            bool triggerExport = exportNow && !_lastExportSignal;
            if (triggerExport && !string.IsNullOrWhiteSpace(exportPath) && _historyBuffer.Count > 0)
            {
                try
                {
                    ExportHistory(exportPath, _historyBuffer, _timestamps);
                    exportMsg = $"Exportado com sucesso para '{Path.GetFileName(exportPath)}' ({_historyBuffer.Count} estados)";
                }
                catch (Exception ex)
                {
                    exportMsg = $"Erro ao exportar: {ex.Message}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, exportMsg);
                }
            }
            _lastExportSignal = exportNow;

            // Construir Árvore Cronológica {iter; path}
            var historyTree = new GH_Structure<IGH_Goo>();
            for (int t = 0; t < _historyBuffer.Count; t++)
            {
                var stateTree = _historyBuffer[t];
                foreach (GH_Path p in stateTree.Paths)
                {
                    int[] origIndices = p.Indices;
                    int[] newIndices = new int[origIndices.Length + 1];
                    newIndices[0] = t;
                    Array.Copy(origIndices, 0, newIndices, 1, origIndices.Length);

                    GH_Path timelinePath = new GH_Path(newIndices);
                    var branch = stateTree.get_Branch(p);
                    historyTree.EnsurePath(timelinePath);
                    for (int i = 0; i < branch.Count; i++)
                    {
                        historyTree.Append((IGH_Goo)branch[i], timelinePath);
                    }
                }
            }

            string log = $"Iterative Logger:\nBuffer: {_historyBuffer.Count} / {(bufferSize > 0 ? bufferSize.ToString() : "∞")} estados\nStatus: {(pause ? "PAUSADO" : "GRAVANDO")}\n{exportMsg}";

            DA.SetDataTree(0, historyTree);
            DA.SetData(1, _historyBuffer.Count);
            DA.SetData(2, log);

            this.Message = $"{_historyBuffer.Count} estados\n{(pause ? "⏸️ Pause" : "🔴 Gravando")}";
        }

        private static void ExportHistory(string filePath, List<GH_Structure<IGH_Goo>> buffer, List<DateTime> timestamps)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (ext == ".json")
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"iterations\": [");

                for (int t = 0; t < buffer.Count; t++)
                {
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"step\": {t},");
                    sb.AppendLine($"      \"time\": \"{timestamps[t]:yyyy-MM-dd HH:mm:ss.fff}\",");
                    sb.AppendLine("      \"branches\": {");

                    var tree = buffer[t];
                    int pCount = 0;
                    foreach (GH_Path p in tree.Paths)
                    {
                        var branch = tree.get_Branch(p);
                        var itemsJson = new List<string>();
                        for (int i = 0; i < branch.Count; i++)
                        {
                            var goo = (IGH_Goo)branch[i];
                            itemsJson.Add(goo != null ? $"\"{goo.ToString().Replace("\"", "\\\"")}\"" : "null");
                        }
                        string comma = (++pCount < tree.PathCount) ? "," : "";
                        sb.AppendLine($"        \"{p}\": [{string.Join(", ", itemsJson)}]{comma}");
                    }

                    sb.AppendLine("      }");
                    string iterComma = (t < buffer.Count - 1) ? "," : "";
                    sb.AppendLine($"    }}{iterComma}");
                }

                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            else // CSV
            {
                var sb = new StringBuilder();
                sb.AppendLine("Step,Timestamp,Path,Index,Value");

                for (int t = 0; t < buffer.Count; t++)
                {
                    var tree = buffer[t];
                    string time = timestamps[t].ToString("yyyy-MM-dd HH:mm:ss.fff");
                    foreach (GH_Path p in tree.Paths)
                    {
                        var branch = tree.get_Branch(p);
                        for (int i = 0; i < branch.Count; i++)
                        {
                            var goo = (IGH_Goo)branch[i];
                            string val = goo != null ? goo.ToString().Replace("\"", "\"\"") : "";
                            sb.AppendLine($"{t},\"{time}\",\"{p}\",{i},\"{val}\"");
                        }
                    }
                }
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
        }

        protected override System.Drawing.Bitmap Icon => GlauxToolsIcons.IterativeAccumulator;
    }
}
