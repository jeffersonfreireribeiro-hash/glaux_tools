using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    public class PillCatalog_Component : GH_Component
    {
        public PillCatalog_Component()
            : base(
                "Pill Catalog & Inspector",
                "PillCatalog",
                "Varre o documento ativo e o barramento global, catalogando todos os transmitters e receivers. Detecta canais órfãos, transmissões duplicadas, tipos de dados e mapa de dependências.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100005-e1ef-4000-8000-000000000005");
        protected override Bitmap Icon => GlauxToolsIcons.PillCatalog;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Refresh", "R", "Pulso opcional para forçar nova auditoria do canvas.", GH_ParamAccess.item, false);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Channels", "C", "Lista com o nome limpo de todos os canais ativos no documento/barramento.", GH_ParamAccess.list);
            pManager.AddTextParameter("Status", "S", "Status de integridade: 'Conectado (Tx+Rx)', 'Órfão (sem Rx)', 'Órfão (sem Tx)' ou 'Duplicado (Tx Múltiplo)'.", GH_ParamAccess.list);
            pManager.AddTextParameter("Categories", "CAT", "Categoria de cada canal (ACU, GEO, MAT, SIM, etc.).", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "T", "Tipo de dado transmitido em cada canal.", GH_ParamAccess.list);
            pManager.AddTextParameter("AuditReport", "RPT", "Relatório consolidado de auditoria com estatísticas e integridade de conexões.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var doc = OnPingDocument();
            var allChannels = PillHub.GetAllChannels();

            // Dicionários para contagem de Transmitters e Receivers presentes no canvas
            var txCountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rxCountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (doc != null)
            {
                foreach (var obj in doc.Objects)
                {
                    if (obj is PillTransmitter_Component tx && !string.IsNullOrWhiteSpace(tx.CurrentCleanKey))
                    {
                        txCountMap[tx.CurrentCleanKey] = txCountMap.TryGetValue(tx.CurrentCleanKey, out int c) ? c + 1 : 1;
                    }
                    else if (obj is PillCache_Component cache && !string.IsNullOrWhiteSpace(cache.CurrentCleanKey))
                    {
                        txCountMap[cache.CurrentCleanKey] = txCountMap.TryGetValue(cache.CurrentCleanKey, out int c) ? c + 1 : 1;
                    }
                    else if (obj is PillLayerPipeline_Component pipe && !string.IsNullOrWhiteSpace(pipe.CurrentCleanKey))
                    {
                        txCountMap[pipe.CurrentCleanKey] = txCountMap.TryGetValue(pipe.CurrentCleanKey, out int c) ? c + 1 : 1;
                    }
                    else if (obj is PillReceiver_Component rx && !string.IsNullOrWhiteSpace(rx.CurrentCleanKey))
                    {
                        rxCountMap[rx.CurrentCleanKey] = rxCountMap.TryGetValue(rx.CurrentCleanKey, out int c) ? c + 1 : 1;
                    }
                }
            }

            // Coleta todas as chaves únicas (do barramento ou dos componentes do canvas)
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in allChannels) allKeys.Add(ch.CleanKey);
            foreach (var k in txCountMap.Keys) allKeys.Add(k);
            foreach (var k in rxCountMap.Keys) allKeys.Add(k);

            var sortedKeys = allKeys.OrderBy(k => k).ToList();

            var outChannels = new List<string>();
            var outStatus = new List<string>();
            var outCategories = new List<string>();
            var outTypes = new List<string>();

            var sb = new StringBuilder();
            sb.AppendLine("=========================================================================");
            sb.AppendLine("           BURAQUEIRA TOOLS :: CATÁLOGO & INSPECTOR DE PILLS            ");
            sb.AppendLine("=========================================================================");
            sb.AppendLine($"Total de Canais Mapeados: {sortedKeys.Count} | Documento: {doc?.DisplayName ?? "Global"}");
            sb.AppendLine("-------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-20} | {1,-6} | {2,-16} | {3,-5} | {4,-5} | {5}", "CANAL", "CAT", "STATUS", "TX", "RX", "TIPO"));
            sb.AppendLine("-------------------------------------------------------------------------");

            int connectedCount = 0;
            int orphanCount = 0;
            int duplicateCount = 0;

            foreach (var key in sortedKeys)
            {
                txCountMap.TryGetValue(key, out int txCount);
                rxCountMap.TryGetValue(key, out int rxCount);

                PillHub.TryGetChannel(key, out PillChannel ch);

                string cat = ch?.Category ?? "GEN";
                string type = ch?.DataTypeName ?? "Desconhecido";
                string unit = ch?.Unit ?? "";

                string status;
                if (txCount > 1)
                {
                    status = "DUPLICADO (Múltiplos Tx)";
                    duplicateCount++;
                }
                else if (txCount == 1 && rxCount >= 1)
                {
                    status = "CONECTADO";
                    connectedCount++;
                }
                else if (txCount == 1 && rxCount == 0)
                {
                    status = "ÓRFÃO (Sem Rx)";
                    orphanCount++;
                }
                else if (txCount == 0 && rxCount >= 1)
                {
                    status = "ÓRFÃO (Sem Tx)";
                    orphanCount++;
                }
                else
                {
                    status = "MEMÓRIA (Inativo)";
                }

                outChannels.Add(key);
                outStatus.Add(status);
                outCategories.Add(cat);
                outTypes.Add(type);

                string labelWithUnit = string.IsNullOrEmpty(unit) ? key : $"{key} [{unit}]";
                sb.AppendLine(string.Format("{0,-20} | {1,-6} | {2,-16} | {3,-5} | {4,-5} | {5}", 
                    labelWithUnit.Length > 20 ? labelWithUnit.Substring(0, 17) + "..." : labelWithUnit,
                    cat, 
                    status, 
                    txCount, 
                    rxCount, 
                    type));
            }

            sb.AppendLine("-------------------------------------------------------------------------");
            sb.AppendLine($"Resumo: {connectedCount} Conectados | {orphanCount} Órfãos | {duplicateCount} Duplicados");
            sb.AppendLine("=========================================================================");

            DA.SetDataList(0, outChannels);
            DA.SetDataList(1, outStatus);
            DA.SetDataList(2, outCategories);
            DA.SetDataList(3, outTypes);
            DA.SetData(4, sb.ToString());

            Message = $"{sortedKeys.Count} Canais ({connectedCount} OK)";
        }
    }
}
