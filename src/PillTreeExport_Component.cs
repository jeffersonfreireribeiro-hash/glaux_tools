using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Buraqueira_Tools.Data;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Exporta uma DataTree (caminhos, índices, tipos, nulos e ramos vazios) para JSON, CSV longo,
    /// binário Glaux (.glxt) ou .pilldata, em texto ou arquivo, com hash de identidade exato.
    /// </summary>
    public class PillTreeExport_Component : GlauxCapsuleComponent
    {
        public PillTreeExport_Component()
            : base(
                "Pill Tree Export",
                "PillExport",
                "Serializa qualquer DataTree sem perda estrutural (caminhos de qualquer profundidade, ramos vazios, nulos e tipos mistos) em JSON tipado, CSV longo (path,index,type,value), binário Glaux (.glxt) ou .pilldata (compatível com Pill Disk Save/Load).\n" +
                "- Sem 'File Path': devolve o texto (formatos binários em Base64).\n" +
                "- Com 'File Path': grava o arquivo de forma atômica quando 'Write' = True.\n" +
                "- Emite o hash SHA-256 de identidade exata dos dados (diferente do fingerprint tolerante do Pill Cache).",
                "I/O",
                "IO",
                ColorIO)
        {
        }

        public override Guid ComponentGuid => new Guid("d34afde2-5f9f-44bc-a885-8d407e5885ea");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillTreeExport;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore de dados (DataTree) a exportar.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Format", "F", "Formato: 'json' (padrão), 'csv' (longo: path,index,type,value,data), 'binary' (.glxt compacto) ou 'pilldata' (Pill Disk Save). Com File Path, a extensão do arquivo é usada se este campo estiver vazio.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("File Path", "Path", "Arquivo de destino opcional. Relativo = pasta do .gh. Sem arquivo, o resultado sai como texto.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Write", "W", "Grava o arquivo quando True (conecte um botão). Ignorado sem File Path.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Metadata", "M", "Metadados opcionais no formato 'chave=valor' (gravados no JSON/CSV/binário; não alteram o hash dos dados).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Pretty", "P", "JSON indentado (legível). Não afeta os dados.", GH_ParamAccess.item, true);
            pManager.AddTextParameter("Delimiter", "Del", "Separador do CSV: ',' (padrão), ';' (Excel pt-BR) ou 'tab'.", GH_ParamAccess.item, ",");
            for (int i = 1; i < 7; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Árvore serializada (somente quando não há File Path). Formatos binários em Base64.", GH_ParamAccess.item);
            pManager.AddTextParameter("Hash", "H", "SHA-256 de identidade exata dos dados (ignora metadados).", GH_ParamAccess.item);
            pManager.AddTextParameter("File Path", "Path", "Arquivo gravado nesta solução (vazio se nada foi gravado).", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Resumo: ramos, itens, tipos, profundidade, tamanho e tempo.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree)) tree = new GH_Structure<IGH_Goo>();

            string formatText = "";
            DA.GetData(1, ref formatText);
            string rawPath = "";
            DA.GetData(2, ref rawPath);
            bool write = false;
            DA.GetData(3, ref write);
            var metaLines = new List<string>();
            DA.GetDataList(4, metaLines);
            bool pretty = true;
            DA.GetData(5, ref pretty);
            string delimText = ",";
            DA.GetData(6, ref delimText);
            char delimiter = ParseDelimiter(delimText);

            bool toFile = !string.IsNullOrWhiteSpace(rawPath);
            TreeFormat format;
            if (!string.IsNullOrWhiteSpace(formatText))
            {
                if (!TreeFormats.TryParse(formatText, out format))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Formato desconhecido '{formatText}'. Use json, csv, binary ou pilldata.");
                    SetCapsule("Formato inválido", false);
                    return;
                }
            }
            else if (!toFile || !TreeFormats.TryFromExtension(rawPath, out format))
            {
                format = TreeFormat.Json;
            }

            var warnings = new List<string>();
            var sw = Stopwatch.StartNew();
            var table = TreeMapper.ToTable(tree, warnings);
            foreach (var kv in ParseKeyValues(metaLines)) table.Metadata[kv.Key] = kv.Value;
            string hash = TreeHash.Compute(table);

            string text = "";
            string writtenPath = "";
            long sizeBytes;
            try
            {
                if (!toFile)
                {
                    text = TreeFormats.ToText(tree, table, format, pretty, delimiter);
                    sizeBytes = text.Length;
                }
                else
                {
                    string fullPath = GlauxPaths.Resolve(OnPingDocument(), rawPath, "", TreeFormats.ExtensionFor(format));
                    CapsuleKey = Path.GetFileName(fullPath);
                    if (write)
                    {
                        byte[] bytes = TreeFormats.ToFileBytes(tree, table, format, pretty, delimiter);
                        AtomicFile.WriteAllBytes(fullPath, bytes);
                        writtenPath = fullPath;
                        sizeBytes = bytes.Length;
                    }
                    else
                    {
                        sizeBytes = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Aguardando 'Write' = True para gravar o arquivo.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao exportar: {ex.Message}");
                SetCapsule("Erro", false);
                DA.SetData(1, hash);
                return;
            }
            sw.Stop();

            ReportWarnings(warnings);
            int lossy = CountLossy(table);
            if (lossy > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{lossy} item(ns) de tipos sem serialização foram gravados só como texto.");
            }

            string formatLabel = format.ToString().ToUpperInvariant();
            if (toFile)
            {
                SetCapsule(CapsuleKey, writtenPath.Length > 0 || sizeBytes > 0, lossy > 0 || writtenPath.Length == 0, formatLabel);
            }
            else
            {
                SetCapsule($"{table.ItemCount} itens", true, lossy > 0, formatLabel);
            }
            Message = formatLabel;

            string info = $"{table.BranchCount} ramo(s), {table.ItemCount} item(ns) ({table.NullCount} nulo(s)) | profundidade {table.MinDepth}–{table.MaxDepth} | " +
                          $"tipos: {DescribeTypes(table)} | {FormatBytes(sizeBytes)} | {sw.ElapsedMilliseconds} ms";
            if (writtenPath.Length > 0) info += $" | gravado em {writtenPath}";

            DA.SetData(0, text);
            DA.SetData(1, hash);
            DA.SetData(2, writtenPath);
            DA.SetData(3, info);
        }

        internal static char ParseDelimiter(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            if (t == "tab" || t == "\\t" || text == "\t") return '\t';
            if (t.Length == 1) return t[0];
            return ',';
        }

        internal static string DescribeTypes(GlauxTreeTable table)
        {
            var counts = table.CountByType();
            if (counts.Count == 0) return "—";
            var parts = new List<string>();
            foreach (var kv in counts)
            {
                string name = kv.Key;
                int comma = name.IndexOf(',');
                if (comma >= 0) name = name.Substring(0, comma);
                int dot = name.LastIndexOf('.');
                if (dot >= 0) name = name.Substring(dot + 1);
                parts.Add($"{name}×{kv.Value}");
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(", ", parts);
        }

        private static int CountLossy(GlauxTreeTable table)
        {
            int n = 0;
            foreach (var b in table.Branches)
                foreach (var v in b.Items)
                    if (v.IsLossy) n++;
            return n;
        }
    }
}
