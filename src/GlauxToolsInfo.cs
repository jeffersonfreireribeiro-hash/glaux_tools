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
        public override string Description =>
            "Dados, análise, controle e visualização para definições paramétricas no Grasshopper (Rhino 8): barramento sem fios Pill, " +
            "DataTrees, álgebra linear, estatística e aprendizado de máquina, gráficos e desenho técnico, persistência e proveniência de dados, " +
            "diagnóstico de desempenho e dashboards no canvas.\n\n" +
            "Data, analysis, control and visualization for parametric definitions in Grasshopper (Rhino 8): Pill wireless bus, " +
            "DataTrees, linear algebra, statistics and machine learning, charts and technical drawing, data persistence and provenance, " +
            "performance diagnostics and on-canvas dashboards.";
        public override string AuthorName => "Jefferson Freire Ribeiro";
        public override string AuthorContact => "https://github.com/jeffersonfreireribeiro-hash/glaux_tools";
        // Lida do próprio assembly (definida em Glaux_Tools.csproj): não diverge mais da versão compilada
        public override string Version => s_version;
        private static readonly string s_version = ReadVersion();
        public override Bitmap Icon => GlauxToolsIcons.PluginTabIcon;
        public override Bitmap AssemblyIcon => GlauxToolsIcons.PluginTabIcon;
        public override Guid Id => new Guid("7c9a1b2e-3d4f-5a6b-7c8d-9e0f1a2b3c4d");

        private static string ReadVersion()
        {
            var v = typeof(BuraqueiraToolsAssemblyInfo).Assembly.GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
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
