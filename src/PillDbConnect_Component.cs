using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Buraqueira_Tools.Persistence;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Abre (ou cria) um store local .glauxdb e entrega a conexão compartilhada para os demais componentes Pill DB.
    /// </summary>
    public class PillDbConnect_Component : GlauxCapsuleComponent
    {
        public PillDbConnect_Component()
            : base(
                "Pill DB Connect",
                "PillDB",
                "Abre ou cria um store local de dados paramétricos (.glauxdb): um arquivo único, sem dependências externas, com revisões, consultas e histórico.\n" +
                "- Vazio = store padrão do projeto na pasta PillVault ao lado do .gh.\n" +
                "- A mesma conexão é compartilhada por todos os componentes e documentos (sem arquivos presos abertos).\n" +
                "- 'Compact' reescreve o arquivo mantendo só as últimas N revisões de cada chave.\n" +
                "- Keys/Entries mostram o estado na última execução; para acompanhar gravações ao vivo use o Pill Schema Inspector.",
                "Data",
                "DB",
                ColorDB)
        {
        }

        public override Guid ComponentGuid => new Guid("a662defd-cf3b-4847-ab9e-8ddb8eaf3fe3");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override Bitmap Icon => GlauxToolsIcons.PillDbConnect;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Path", "Path", "Arquivo .glauxdb (relativo = pasta do .gh). Vazio = PillVault/" + StoreInput.DefaultFileName, GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Read Only", "RO", "Somente leitura: componentes de escrita recusam gravar neste store.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Compact", "C", "Reescreve o arquivo mantendo só as últimas 'Keep' revisões de cada chave (conecte um botão).", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("Keep", "K", "Revisões mantidas por chave na compactação (0 = todas; só limpa gravações interrompidas).", GH_ParamAccess.item, 0);
            for (int i = 0; i < 4; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Store", "S", "Conexão com o store para os componentes Pill DB / Vault.", GH_ParamAccess.item);
            pManager.AddTextParameter("Keys", "K", "Chaves existentes (formato 'tipo:chave').", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Entries", "N", "Total de entradas (revisões) no arquivo.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Arquivo, tamanho, versão do formato e integridade.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string rawPath = "";
            DA.GetData(0, ref rawPath);
            bool readOnly = false;
            DA.GetData(1, ref readOnly);
            bool compact = false;
            DA.GetData(2, ref compact);
            int keep = 0;
            DA.GetData(3, ref keep);

            var doc = OnPingDocument();
            if (!StoreInput.TryResolve(rawPath, doc, out var store, out _, out string error))
            {
                SetCapsule("Caminho inválido", false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                return;
            }

            if (!GlauxPaths.IsDocumentSaved(doc) && !Path.IsPathRooted(rawPath ?? ""))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "O .gh ainda não foi salvo: o store fica em %APPDATA%\\Grasshopper\\PillVault.");
            }

            try
            {
                if (readOnly)
                {
                    if (!store.Exists)
                    {
                        SetCapsule(Path.GetFileName(store.Path), false);
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Store não encontrado (somente leitura não cria arquivos): {store.Path}");
                        return;
                    }
                }
                else
                {
                    store.EnsureCreated();
                }

                if (compact)
                {
                    if (readOnly)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Compactação ignorada: conexão somente leitura.");
                    }
                    else
                    {
                        int removed = store.Compact(Math.Max(0, keep));
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Compactado: {removed} revisão(ões) removida(s).");
                    }
                }

                // Sem StoreWatch aqui: expirar o Connect a cada gravação reexecutaria todos os componentes
                // ligados à saída Store (inclusive quem gravou). Leitores observam o store diretamente.
                var stats = store.GetStats();
                var keys = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var e in store.ListEntries()) keys.Add(e.Kind + ":" + e.Key);

                if (stats.CorruptionMessage != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Store com corrupção no meio do arquivo (gravação bloqueada; entradas anteriores continuam legíveis): " + stats.CorruptionMessage);
                }
                if (stats.TornTailBytes > 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{stats.TornTailBytes} byte(s) de uma gravação interrompida serão descartados na próxima escrita.");
                }

                string name = Path.GetFileName(store.Path);
                SetCapsule(name, stats.CorruptionMessage == null, readOnly || stats.TornTailBytes > 0);
                Message = $"{stats.EntryCount} rev · {FormatBytes(stats.FileBytes)}";

                DA.SetData(0, new GH_GlauxStoreGoo(new GlauxStoreRef(store.Path, readOnly)));
                DA.SetDataList(1, keys);
                DA.SetData(2, stats.EntryCount);
                DA.SetData(3, $"{store.Path} | {stats.KeyCount} chave(s), {stats.EntryCount} revisão(ões) | {FormatBytes(stats.FileBytes)} | formato v{stats.FormatVersion}" +
                              (readOnly ? " | somente leitura" : ""));
            }
            catch (Exception ex)
            {
                SetCapsule(Path.GetFileName(store.Path), false);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
