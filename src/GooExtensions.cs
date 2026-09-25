using System;
using System.Reflection;
using Grasshopper.Kernel.Types;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Extensões seguras para manipulação de IGH_Goo imunes a falhas de plugins externos (ex: Wallacei, Octopus, Karamba).
    /// </summary>
    public static class GooExtensions
    {
        public static object SafeScriptVariable(this IGH_Goo goo)
        {
            if (goo == null) return null;
            try
            {
                return goo.ScriptVariable();
            }
            catch (NotImplementedException)
            {
                // Tratamento especial para Wallacei e plugins que não implementam ScriptVariable()
                try
                {
                    var prop = goo.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var v = prop.GetValue(goo, null);
                        if (v != null) return v;
                    }
                }
                catch { }

                try
                {
                    return goo.ToString();
                }
                catch
                {
                    return goo.TypeName ?? "Goo";
                }
            }
            catch (Exception)
            {
                try
                {
                    return goo.ToString();
                }
                catch
                {
                    return goo.TypeName ?? "Goo";
                }
            }
        }
    }
}
