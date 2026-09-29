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
    /// Reconstrói uma DataTree a partir de texto ou arquivo (JSON, CSV longo, binário Glaux ou .pilldata),
    /// com detecção automática de formato e cache por arquivo (só relê quando o arquivo muda).
    /// </summary>
    public class PillTreeImport_Component : GlauxCapsuleComponent
    {
        private string _cacheKey = "";
        private GlauxTreeTable _cachedTable;
        private List<string> _cachedWarnings = new List<string>();

        public PillTreeImport_Component()
            : base(
                "Pill Tree Import",
                "PillImport",
                "Reconstrói uma DataTree exportada pelo Pill Tree Export (JSON, CSV longo, binário .glxt) ou gravada pelo Pill Disk Save (.pilldata), preservando caminhos, ramos vazios, nulos e tipos.\n" +
                "- Aceita texto (T) ou arquivo (Path); o formato é detectado pelo conteúdo quando 'Format' fica vazio.\n" +
                "- Arquivos só são relidos quando mudam (tamanho/data), evitando reprocessar a cada solução.\n" +
                "- Tipos de plugins não carregados são mantidos como valores opacos, sem perda ao reexportar.",
                "I/O",
                "IO",
                ColorIO)
        {
        }

        public override Guid ComponentGuid => new Guid("9886c29f-7dd5-430f-bd38-2c74b8caef41");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override Bitmap Icon => GlauxToolsIcons.PillTreeImport;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Texto serializado (JSON, CSV longo ou Base64 de binário). Ignorado se File Path for informado.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("File Path", "Path", "Arquivo a importar (.json, .csv, .glxt, .pilldata). Relativo = pasta do .gh.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "F", "Vazio = detectar automaticamente. Ou: json, csv, binary, pilldata.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Delimiter", "Del", "Separador do CSV: ',' (padrão), ';' ou 'tab'.", GH_ParamAccess.item, ",");
            pManager.AddBooleanParameter("Reload", "R", "Força a releitura do arquivo mesmo sem mudança detectada.", GH_ParamAccess.item, false);
            for (int i = 0; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Árvore reconstruída.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Hash", "H", "SHA-256 de identidade dos dados importados (igual ao do Export quando nada mudou).", GH_ParamAccess.item);
            pManager.AddTextParameter("Metadata", "M", "Metadados 'chave=valor' gravados no arquivo.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Resumo: formato, ramos, itens, tipos e tempo de leitura.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string text = "";
            DA.GetData(0, ref text);
            string rawPath = "";
            DA.GetData(1, ref rawPath);
            string formatText = "";
            DA.GetData(2, ref formatText);
            string delimText = ",";
            DA.GetData(3, ref delimText);
            bool reload = false;
            DA.GetData(4, ref reload);
            char delimiter = PillTreeExport_Component.ParseDelimiter(delimText);

            bool fromFile = !string.IsNullOrWhiteSpace(rawPath);
            if (!fromFile && string.IsNullOrWhiteSpace(text))
            {
                SetCapsule("Sem entrada", false);
                Message = "";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Conecte um texto (T) ou um arquivo (Path).");
                return;
            }

            bool explicitFormat = TreeFormats.TryParse(formatText, out TreeFormat format) && !string.IsNullOrWhiteSpace(formatText);
            if (!string.IsNullOrWhiteSpace(formatText) && !explicitFormat)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Formato desconhecido '{formatText}'.");
                return;
            }

            var sw = Stopwatch.StartNew();
            GlauxTreeTable table;
            List<string> warnings;
            string source;
            try
            {
                if (fromFile)
                {
                    string fullPath = GlauxPaths.Resolve(OnPingDocument(), rawPath, "");
                    source = Path.GetFileName(fullPath);
                    if (!File.Exists(fullPath))
                    {
                        SetCapsule(source, false);
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Arquivo não encontrado: {fullPath}");
                        return;
                    }

                    var fi = new FileInfo(fullPath);
                    string cacheKey = $"{fullPath}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}|{formatText}|{delimiter}";
                    if (!reload && cacheKey == _cacheKey && _cachedTable != null)
                    {
                        table = _cachedTable;
                        warnings = _cachedWarnings;
                    }
                    else
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        if (!explicitFormat && !TreeFormats.TryFromExtension(fullPath, out format)) format = TreeFormats.DetectBytes(bytes);
                        warnings = new List<string>();
                        table = TreeFormats.FromFileBytes(bytes, format, delimiter, warnings);
                        _cacheKey = cacheKey;
                        _cachedTable = table;
                        _cachedWarnings = warnings;
                    }
                }
                else
                {
                    source = "texto";
                    if (!explicitFormat) format = TreeFormats.DetectText(text);
                    warnings = new List<string>();
                    table = TreeFormats.FromText(text, format, delimiter, warnings);
                }
            }
            catch (Exception ex)
            {
                _cacheKey = "";
                _cachedTable = null;
                SetCapsule("Erro de leitura", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Falha ao importar: {ex.Message}");
                return;
            }

            var decodeWarnings = new List<string>(warnings);
            var tree = TreeMapper.ToTree(table, decodeWarnings);
            sw.Stop();
            ReportWarnings(decodeWarnings);

            string label = format.ToString().ToUpperInvariant();
            SetCapsule(source, true, decodeWarnings.Count > 0, label);
            Message = label;

            DA.SetDataTree(0, tree);
            DA.SetData(1, TreeHash.Compute(table));
            DA.SetDataList(2, FormatKeyValues(table.Metadata));
            DA.SetData(3, $"{label} | {table.BranchCount} ramo(s), {table.ItemCount} item(ns) | tipos: {PillTreeExport_Component.DescribeTypes(table)} | {sw.ElapsedMilliseconds} ms");
        }
    }
}
