using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Metadados oficiais do plugin Buraqueira Tools para o Grasshopper.
    /// </summary>
    public class BuraqueiraToolsAssemblyInfo : GH_AssemblyInfo
    {
        public override string Name => "Glaux Tools";
        public override string Description => "Suíte de Ferramentas de Manipulação de Dados, I/O (CSV, TSV, TXT) e Estatística Descritiva para o Grasshopper.";
        public override string AuthorName => "Buraqueira Team";
        public override string AuthorContact => "";
        public override string Version => "1.0.0.0";
        public override Bitmap Icon => GlauxToolsIcons.PluginTabIcon;
        public override Bitmap AssemblyIcon => GlauxToolsIcons.PluginTabIcon;
        public override Guid Id => new Guid("7c9a1b2e-3d4f-5a6b-7c8d-9e0f1a2b3c4d");
    }

    /// <summary>
    /// Registra o ícone oficial na aba/categoria 'Buraqueira Tools' no topo da Ribbon do Grasshopper.
    /// </summary>
    public class BuraqueiraToolsPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            try
            {
                Grasshopper.Instances.ComponentServer.AddCategoryIcon("Glaux Tools", GlauxToolsIcons.PluginTabIcon);
                Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Glaux Tools", 'G');
            }
            catch { }
            return GH_LoadingInstruction.Proceed;
        }
    }
}
