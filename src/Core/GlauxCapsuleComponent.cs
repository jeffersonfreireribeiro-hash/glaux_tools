using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using Grasshopper.Kernel;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Base dos componentes das pilhas Data/Vault/Diagnostics: cápsula Pill via <see cref="IPillCapsule"/>,
    /// avisos agregados e utilitários comuns. Componentes derivados só leem entradas, chamam os serviços
    /// de <c>Buraqueira_Tools.Data/Persistence/ProjectState/Diagnostics</c> e escrevem as saídas.
    /// </summary>
    public abstract class GlauxCapsuleComponent : GH_Component, IPillCapsule
    {
        public const string CategoryName = "Glaux Tools";

        public static readonly Color ColorIO = Color.FromArgb(14, 165, 233);
        public static readonly Color ColorDB = Color.FromArgb(99, 102, 241);
        public static readonly Color ColorVault = Color.FromArgb(245, 158, 11);
        public static readonly Color ColorDiagnostics = Color.FromArgb(236, 72, 153);
        public static readonly Color ColorDashboard = Color.FromArgb(139, 92, 246);

        private const int MaxWarningsShown = 4;

        protected GlauxCapsuleComponent(string name, string nickname, string description, string subCategory, string capsuleCategory, Color capsuleColor)
            : base(name, nickname, description, CategoryName, subCategory)
        {
            CapsuleCategory = capsuleCategory;
            CapsuleColor = capsuleColor;
        }

        public string CapsuleKey { get; protected set; } = "";
        public string CapsuleCategory { get; protected set; }
        public string CapsuleUnit { get; protected set; } = "";
        public Color CapsuleColor { get; protected set; }
        public bool CapsuleOk { get; protected set; }
        public bool CapsuleWarning { get; protected set; }

        public override void CreateAttributes()
        {
            m_attributes = new Pill_Attributes(this);
        }

        protected void SetCapsule(string key, bool ok, bool warning = false, string unit = "")
        {
            CapsuleKey = key ?? "";
            CapsuleOk = ok;
            CapsuleWarning = warning;
            CapsuleUnit = unit ?? "";
        }

        /// <summary>Mostra os primeiros avisos e resume o restante, sem inundar o balão do componente.</summary>
        protected void ReportWarnings(ICollection<string> warnings)
        {
            if (warnings == null || warnings.Count == 0) return;
            var distinct = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in warnings)
            {
                if (seen.Add(w)) distinct.Add(w);
            }
            for (int i = 0; i < distinct.Count && i < MaxWarningsShown; i++)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, distinct[i]);
            }
            if (distinct.Count > MaxWarningsShown)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"... e mais {distinct.Count - MaxWarningsShown} aviso(s) diferente(s).");
            }
        }

        /// <summary>Converte linhas "chave=valor" em dicionário (linhas sem '=' viram chave com valor vazio).</summary>
        public static Dictionary<string, string> ParseKeyValues(IEnumerable<string> lines)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines == null) return result;
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int eq = raw.IndexOf('=');
                string k = (eq >= 0 ? raw.Substring(0, eq) : raw).Trim();
                string v = eq >= 0 ? raw.Substring(eq + 1).Trim() : "";
                if (k.Length > 0) result[k] = v;
            }
            return result;
        }

        public static List<string> FormatKeyValues(IDictionary<string, string> values)
        {
            var list = new List<string>();
            if (values == null) return list;
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys) list.Add($"{k}={values[k]}");
            return list;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("F1", CultureInfo.InvariantCulture) + " KB";
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }
    }

    /// <summary>
    /// Resolução de caminhos de arquivo com a mesma convenção do Pill Disk Save:
    /// relativo → pasta do .gh; vazio → subpasta "PillVault" do .gh; .gh não salvo → %APPDATA%\Grasshopper\PillVault.
    /// </summary>
    public static class GlauxPaths
    {
        public const string VaultFolderName = "PillVault";

        public static string DefaultDirectory(GH_Document doc)
        {
            string docPath = doc?.FilePath;
            if (!string.IsNullOrEmpty(docPath))
            {
                return Path.Combine(Path.GetDirectoryName(docPath), VaultFolderName);
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grasshopper", VaultFolderName);
        }

        /// <summary>Resolve <paramref name="rawPath"/>; se vazio, usa <paramref name="defaultFileName"/> na pasta padrão.</summary>
        public static string Resolve(GH_Document doc, string rawPath, string defaultFileName, string defaultExtension = null)
        {
            string p = (rawPath ?? "").Trim().Trim('"', '\'');
            if (p.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { p = new Uri(p).LocalPath; } catch { }
            }

            if (string.IsNullOrEmpty(p))
            {
                p = Path.Combine(DefaultDirectory(doc), defaultFileName);
            }
            else if (!Path.IsPathRooted(p))
            {
                string docPath = doc?.FilePath;
                string baseDir = !string.IsNullOrEmpty(docPath) ? Path.GetDirectoryName(docPath) : DefaultDirectory(doc);
                p = Path.Combine(baseDir, p);
            }

            if (!string.IsNullOrEmpty(defaultExtension) && string.IsNullOrEmpty(Path.GetExtension(p)))
            {
                p += defaultExtension;
            }
            return Path.GetFullPath(p);
        }

        public static bool IsDocumentSaved(GH_Document doc) => !string.IsNullOrEmpty(doc?.FilePath);
    }
}
