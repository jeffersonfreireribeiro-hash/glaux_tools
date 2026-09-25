using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public class PillChangeDetector_Component : GH_Component
    {
        private string _lastHash = "";
        private string _referenceHash = "";
        private bool _isFirstRun = true;
        private bool _toggleState = false;
        private int _changeCount = 0;
        private DateTime _lastChangeTime = DateTime.Now;
        private string _lastDeltaSummary = "Iniciando monitoramento...";
        private bool _isResettingPulse = false;

        public PillChangeDetector_Component()
            : base(
                "Pill Change Detector & Trigger",
                "PillChange",
                "Monitora dados, listas, arvores ou pacotes (PillBundle). Quando detecta qualquer modificacao nos valores, dispara um sinal booleano True (Data Gate / Pulse Trigger) para acionar salvamento de presets, simulacoes ou automacoes.",
                "Glaux Tools",
                "Pills")
        {
        }

        public override Guid ComponentGuid => new Guid("a1100009-e1ef-4000-8000-000000000009");
        protected override Bitmap Icon => GlauxToolsIcons.PillChangeDetector;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "D", "Dados, listas, arvores ou PillBundle a monitorar continuamente.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Threshold", "T", "Tolerancia para variacoes numericas (padrao: 0.0001). Variacoes menores que esta tolerancia sao ignoradas.", GH_ParamAccess.item, 0.0001);
            pManager.AddIntegerParameter("Mode", "M", "Modo de disparo:\n0 = Pulso (True apenas no ciclo de alteracao, False quando estavel)\n1 = Latch (True enquanto for diferente da referencia inicial)\n2 = Toggle (Inverte o booleano a cada mudanca detectada)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reset", "R", "Pulso para resetar a referencia memorizada para o estado atual.", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Changed", "C", "Sinal booleano: True se os dados foram modificados nesta solucao (ou conforme o Modo selecionado); False se inalterados.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Pass", "D", "Pass-through direto dos dados recebidos para encadeamento de fluxo.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Hash", "H", "Assinatura digital (SHA-256) do estado atual dos dados monitorados.", GH_ParamAccess.item);
            pManager.AddTextParameter("Delta", "Δ", "Diagnostico e resumo da alteracao detectada (delta de contagem, valores e timestamp).", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> dataTree) || dataTree == null)
            {
                dataTree = new GH_Structure<IGH_Goo>();
            }

            double threshold = 0.0001;
            DA.GetData(1, ref threshold);
            if (threshold < 0) threshold = 0.0;

            int mode = 0;
            DA.GetData(2, ref mode);
            if (mode < 0 || mode > 2) mode = 0;

            bool reset = false;
            DA.GetData(3, ref reset);

            if (_isResettingPulse)
            {
                _isResettingPulse = false;
                DA.SetData(0, false);
                DA.SetDataTree(1, dataTree);
                DA.SetData(2, _lastHash);
                DA.SetData(3, _lastDeltaSummary);
                Message = "Estavel\n(False)";
                return;
            }

            string currentHash = ComputeDataTreeFingerprint(dataTree, threshold, out int itemCount, out string sampleSummary);

            if (reset || _isFirstRun)
            {
                _referenceHash = currentHash;
                _lastHash = currentHash;
                _isFirstRun = false;
                _lastChangeTime = DateTime.Now;
                _lastDeltaSummary = $"Linha de base inicial fixada: {itemCount} itens | Hash: {ShortHash(currentHash)}";

                DA.SetData(0, false);
                DA.SetDataTree(1, dataTree);
                DA.SetData(2, currentHash);
                DA.SetData(3, _lastDeltaSummary);

                Message = "Inicializado";
                return;
            }

            bool isModified = !string.Equals(currentHash, _lastHash, StringComparison.Ordinal);

            if (isModified)
            {
                _changeCount++;
                _lastChangeTime = DateTime.Now;
                _toggleState = !_toggleState;
                _lastDeltaSummary = $"MODIFICADO #{_changeCount} [{_lastChangeTime:HH:mm:ss}]: {itemCount} itens | {sampleSummary}\nDe {ShortHash(_lastHash)} para {ShortHash(currentHash)}";
            }
            else
            {
                _lastDeltaSummary = $"ESTAVEL: {itemCount} itens inalterados desde {_lastChangeTime:HH:mm:ss} (Hash: {ShortHash(currentHash)})";
            }

            _lastHash = currentHash;

            bool signalOutput;
            switch (mode)
            {
                case 1:
                    signalOutput = !string.Equals(currentHash, _referenceHash, StringComparison.Ordinal);
                    break;
                case 2:
                    signalOutput = _toggleState;
                    break;
                default:
                    signalOutput = isModified;
                    break;
            }

            DA.SetData(0, signalOutput);
            DA.SetDataTree(1, dataTree);
            DA.SetData(2, currentHash);
            DA.SetData(3, _lastDeltaSummary);

            if (isModified)
            {
                Message = $"ALTERADO #{_changeCount}\n(True)";

                // Modo 0 (Pulso): emite True e agenda retorno automático para False após 80ms (comportamento de Botão)
                if (mode == 0)
                {
                    var doc = OnPingDocument();
                    if (doc != null)
                    {
                        doc.ScheduleSolution(80, d =>
                        {
                            _isResettingPulse = true;
                            ExpireSolution(false);
                        });
                    }
                }
            }
            else
            {
                Message = signalOutput ? $"Latch ON\n(True)" : "Estavel\n(False)";
            }
        }

        private static string ShortHash(string h)
        {
            if (string.IsNullOrEmpty(h)) return "null";
            return h.Length > 8 ? h.Substring(0, 8) : h;
        }

        private static string ComputeDataTreeFingerprint(GH_Structure<IGH_Goo> tree, double threshold, out int totalItems, out string sampleSummary)
        {
            return PillDataFingerprint.ComputeDataTreeFingerprint(tree, threshold, out totalItems, out sampleSummary);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            var resetItem = new ToolStripMenuItem("Resetar Linha de Base (Fixar Estado Atual)");
            resetItem.Click += (s, e) =>
            {
                _isFirstRun = true;
                ExpireSolution(true);
            };
            menu.Items.Add(resetItem);

            menu.Items.Add(new ToolStripSeparator());
            var hiddenMenu = new ToolStripMenuItem("⚡ Conexao Oculta (Modo Wallacei / Hidden Wire)")
            {
                ToolTipText = "Conecta/desconecta cabos fisicos ocultos (Hidden Wire) preservando a sincronia DAG sequencial para Wallacei e Galapagos."
            };

            var connectAllItem = new ToolStripMenuItem("⚡ CONECTAR TODOS os Pills do Canvas com Cabos Ocultos (Recomendado para Wallacei)");
            connectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.ConnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas foram conectados com cabos ocultos (hidden wire)!\nA sequencia DAG esta garantida para Wallacei e Galapagos.",
                    "Pill System - Modo Wallacei Ativado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(connectAllItem);

            var disconnectAllItem = new ToolStripMenuItem("Desconectar TODOS os Pills do Canvas (Voltar ao Modo 100% Sem Fio)");
            disconnectAllItem.Click += (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc == null) return;
                int c = PillHub.DisconnectAllDocumentPillsHidden(doc);
                doc.NewSolution(false);
                MessageBox.Show($"Todos os {c} receptores do Canvas voltaram ao modo 100% sem fio em memoria.",
                    "Pill System - Modo Sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            hiddenMenu.DropDownItems.Add(disconnectAllItem);

            menu.Items.Add(hiddenMenu);
        }
    }
}
