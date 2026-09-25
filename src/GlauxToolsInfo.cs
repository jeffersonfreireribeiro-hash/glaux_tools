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
        public override string Description => "Suíte modular de alta performance para Grasshopper (Rhino 8): arquitetura sem fio Pill, manipulação avançada de DataTrees, álgebra linear, estatística descritiva e mapas de calor espaciais.";
        public override string AuthorName => "Jefferson Freire Ribeiro";
        public override string AuthorContact => "https://github.com/jeffersonfreireribeiro-hash/glaux_tools";
        public override string Version => "1.0.1.0";
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
