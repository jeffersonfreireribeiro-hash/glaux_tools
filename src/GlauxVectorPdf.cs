using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Buraqueira_Tools
{
    /// <summary>
    /// PDF vetorial a partir de SVG/HTML usando o Edge/Chrome em modo headless (sem rasterizar).
    /// Compartilhado pelo Pill Vector Sheet Layout e pelo Marimekko Chart.
    /// </summary>
    internal static class GlauxVectorPdf
    {
        private static readonly string[] Browsers =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe"
        };

        public static bool IsAvailable => Browsers.Any(File.Exists);

        public static bool TryPrintToPdf(string inputPath, string pdfPath, int timeoutMs = 6000)
        {
            string browserPath = Browsers.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(browserPath)) return false;

            try
            {
                string uri = "file:///" + inputPath.Replace("\\", "/");
                var psi = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = $"--headless --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=2000 --print-to-pdf=\"{pdfPath}\" \"{uri}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(timeoutMs);
                }
                return File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0;
            }
            catch { return false; }
        }

        /// <summary>Imprime um SVG (largura × altura em px) em PDF com página do mesmo tamanho, sem margens.</summary>
        public static bool TryPrintSvgToPdf(string svgText, double widthPx, double heightPx, string pdfPath)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "glaux_pdf_" + Guid.NewGuid().ToString("N") + ".html");
            try
            {
                string w = widthPx.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string h = heightPx.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>@page{size:" + w + "px " + h + "px;margin:0}html,body{margin:0;padding:0}svg{display:block}</style></head><body>" + svgText + "</body></html>";
                File.WriteAllText(tmp, html, new System.Text.UTF8Encoding(false));
                return TryPrintToPdf(tmp, pdfPath, 10000);
            }
            catch { return false; }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }
}
