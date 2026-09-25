// PillDataFingerprint.cs
// UtilitÃ¡rio central de alta performance para inspeÃ§Ã£o profunda, fingerprint determinÃ­stico e igualdade de dados.
// Detecta alteraÃ§Ãµes exatas em:
// 1. Audio Signals (PachydermGH.Audio_Signal, double[][], float[][], canais, frequÃªncia, direct sample e hash binÃ¡rio de todas as amostras de Ã¡udio).
// 2. Materiais AcÃºsticos (BURAQUEIRA AcousticMaterialData, GH_AcousticMaterial, Pachyderm Material, Basic_Material, coeficientes em todas as 8 bandas, espalhamento, transmissÃ£o e cor).
// 3. Geometrias Rhino (Mesh com vÃ©rtices e BBox, Brep, Curve, Point3d, Vector3d, Plane, Transform).
// 4. Estruturas complexas (PillBundle recursivo, DataTrees com topologia exata, listas e dicionÃ¡rios).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools
{
    public static class PillDataFingerprint
    {
        /// <summary>
        /// Calcula o hash SHA-256 determinÃ­stico de uma Ã¡rvore de dados inteira (GH_Structure de IGH_Goo),
        /// inspecionando em profundidade materiais acÃºsticos, sinais de Ã¡udio, geometrias e valores escalares.
        /// </summary>
        public static string ComputeTreeHash(GH_Structure<IGH_Goo> tree, double numericThreshold = 0.0001)
        {
            if (tree == null || tree.DataCount == 0)
            {
                return "EMPTY";
            }

            var sb = new StringBuilder();
            sb.Append($"Paths:{tree.Paths.Count}|TotalItems:{tree.DataCount}|");

            foreach (var path in tree.Paths)
            {
                sb.Append($"P{path}:[");
                var branch = tree[path];
                if (branch != null)
                {
                    for (int i = 0; i < branch.Count; i++)
                    {
                        var item = branch[i];
                        if (item == null)
                        {
                            sb.Append("null;");
                        }
                        else
                        {
                            AppendObjectFingerprint(sb, item, numericThreshold);
                            sb.Append(";");
                        }
                    }
                }
                sb.Append("];");
            }

            using (var sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hashBytes.Length * 2);
                foreach (byte b in hashBytes) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// Calcula o fingerprint completo de uma Ã¡rvore retornando o hash, a contagem de itens e um resumo legÃ­vel
        /// para diagnÃ³sticos no componente (ex: para PillChangeDetector).
        /// </summary>
        public static string ComputeDataTreeFingerprint(
            GH_Structure<IGH_Goo> tree, 
            double numericThreshold, 
            out int totalItems, 
            out string sampleSummary)
        {
            totalItems = 0;
            sampleSummary = "Vazio";

            if (tree == null || tree.DataCount == 0)
            {
                return "EMPTY";
            }

            totalItems = tree.DataCount;
            string firstSample = "";
            int count = 0;

            var sb = new StringBuilder();
            sb.Append($"Paths:{tree.Paths.Count}|TotalItems:{totalItems}|");

            foreach (var path in tree.Paths)
            {
                sb.Append($"P{path}:[");
                var branch = tree[path];
                if (branch != null)
                {
                    for (int i = 0; i < branch.Count; i++)
                    {
                        var item = branch[i];
                        if (item == null)
                        {
                            sb.Append("null;");
                        }
                        else
                        {
                            AppendObjectFingerprint(sb, item, numericThreshold);
                            sb.Append(";");

                            if (count == 0)
                            {
                                firstSample = GetReadableSummary(item);
                            }
                            count++;
                        }
                    }
                }
                sb.Append("];");
            }

            sampleSummary = count > 1 ? $"{firstSample} (+{count - 1} itens)" : firstSample;

            using (var sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hashBytes.Length * 2);
                foreach (byte b in hashBytes) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// Extrai o fingerprint Ãºnico de um Ãºnico objeto IGH_Goo (usado pelo BatchDistinct).
        /// </summary>
        public static string ComputeGooFingerprint(IGH_Goo goo, double numericThreshold = 0.0001)
        {
            if (goo == null) return "null";
            var sb = new StringBuilder();
            AppendObjectFingerprint(sb, goo, numericThreshold);
            return sb.ToString();
        }

        /// <summary>
        /// Compara profundamente dois objetos IGH_Goo para igualdade exata de valor,
        /// cobrindo Audio Signals, Materiais AcÃºsticos, Geometrias e Primitivos.
        /// </summary>
        public static bool AreGooEqual(IGH_Goo a, IGH_Goo b, double numericThreshold = 0.0001)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Primitivos rÃ¡pidos de Grasshopper
            if (a is GH_Boolean bA && b is GH_Boolean bB)
                return bA.Value == bB.Value;

            if (a is GH_Integer iA && b is GH_Integer iB)
                return iA.Value == iB.Value;

            if (a is GH_Number nA && b is GH_Number nB)
            {
                if (double.IsNaN(nA.Value) && double.IsNaN(nB.Value)) return true;
                if (double.IsInfinity(nA.Value) || double.IsInfinity(nB.Value)) return nA.Value == nB.Value;
                return Math.Abs(nA.Value - nB.Value) <= numericThreshold;
            }

            if ((a is GH_Number || a is GH_Integer) && (b is GH_Number || b is GH_Integer))
            {
                if (a.CastTo(out double da) && b.CastTo(out double db))
                {
                    if (double.IsNaN(da) && double.IsNaN(db)) return true;
                    if (double.IsInfinity(da) || double.IsInfinity(db)) return da == db;
                    return Math.Abs(da - db) <= numericThreshold;
                }
            }

            if (a is GH_String sA && b is GH_String sB)
                return string.Equals(sA.Value, sB.Value, StringComparison.Ordinal);

            if (a is GH_Colour cA && b is GH_Colour cB)
                return cA.Value.ToArgb() == cB.Value.ToArgb();

            if (a is GH_Point ptA && b is GH_Point ptB)
                return ptA.Value.DistanceTo(ptB.Value) <= numericThreshold;

            if (a is GH_Vector vA && b is GH_Vector vB)
                return (vA.Value - vB.Value).Length <= numericThreshold;

            // Para objetos complexos (Audio_Signal, Material, Geometria, Bundle), compara os fingerprints determinÃ­sticos
            string fpA = ComputeGooFingerprint(a, numericThreshold);
            string fpB = ComputeGooFingerprint(b, numericThreshold);
            return string.Equals(fpA, fpB, StringComparison.Ordinal);
        }

        /// <summary>
        /// Inspeciona recursivamente qualquer objeto e anexa sua representaÃ§Ã£o determinÃ­stica ao StringBuilder.
        /// </summary>
        public static void AppendObjectFingerprint(StringBuilder sb, object rawObj, double threshold)
        {
            if (rawObj == null)
            {
                sb.Append("null");
                return;
            }

            // Desembrulha IGH_Goo
            object val = rawObj;
            if (rawObj is IGH_Goo goo)
            {
                val = goo.SafeScriptVariable() ?? rawObj;
            }
            if (val is GH_ObjectWrapper wrapper && wrapper.Value != null)
            {
                val = wrapper.Value;
            }

            // 1. Audio Signal (PachydermGH.Audio_Signal ou double[][])
            if (TryFingerprintAudioSignal(sb, rawObj, val))
            {
                return;
            }

            // 2. Material AcÃºstico (BURAQUEIRA AcousticMaterialData, GH_AcousticMaterial, Pachyderm Material, Basic_Material)
            if (TryFingerprintMaterial(sb, rawObj, val, threshold))
            {
                return;
            }

            // 3. PillBundle
            if (val is PillBundle bundle)
            {
                sb.Append($"BUNDLE:{bundle.Namespace}:{bundle.Entries.Count}:[");
                foreach (var kvp in bundle.Entries.OrderBy(e => e.Key))
                {
                    sb.Append($"{kvp.Key}=");
                    AppendObjectFingerprint(sb, kvp.Value, threshold);
                    sb.Append(";");
                }
                sb.Append("]");
                return;
            }

            // 4. Geometrias Rhino
            if (TryFingerprintGeometry(sb, val, threshold))
            {
                return;
            }

            // 5. Escalares numÃ©ricos e bÃ¡sicos
            if (val is double d)
            {
                sb.Append(FormatNumber(d, threshold));
                return;
            }
            if (val is float f)
            {
                sb.Append(FormatNumber((double)f, threshold));
                return;
            }
            if (val is int || val is long || val is short || val is byte || val is uint || val is ulong)
            {
                sb.Append(val.ToString());
                return;
            }
            if (val is bool b)
            {
                sb.Append(b ? "True" : "False");
                return;
            }
            if (val is string str)
            {
                sb.Append($"str_{str}");
                return;
            }
            if (val is Color col)
            {
                sb.Append($"Color({col.ToArgb()})");
                return;
            }
            if (val is Interval iv)
            {
                sb.Append($"Iv({FormatNumber(iv.T0, threshold)}..{FormatNumber(iv.T1, threshold)})");
                return;
            }
            if (val is DateTime dt)
            {
                sb.Append($"DT({dt:yyyyMMddHHmmssfff})");
                return;
            }
            if (val is Guid gd)
            {
                sb.Append($"Guid({gd})");
                return;
            }

            // 6. ColeÃ§Ãµes (Listas, Arrays, DicionÃ¡rios)
            if (val is IDictionary dict)
            {
                sb.Append("Dict:[");
                foreach (DictionaryEntry entry in dict)
                {
                    sb.Append($"{entry.Key}=");
                    AppendObjectFingerprint(sb, entry.Value, threshold);
                    sb.Append(";");
                }
                sb.Append("]");
                return;
            }
            if (val is IEnumerable list && !(val is string))
            {
                sb.Append("List:[");
                foreach (var item in list)
                {
                    AppendObjectFingerprint(sb, item, threshold);
                    sb.Append(",");
                }
                sb.Append("]");
                return;
            }

            // 7. Fallback seguro com TypeName e ToString
            string fallback = rawObj is IGH_Goo gFallback ? $"{gFallback.TypeName}:{gFallback}" : val.ToString();
            sb.Append(fallback);
        }

        // =========================================================================
        // INSPEÃ‡ÃƒO DE AUDIO SIGNAL
        // =========================================================================
        private static bool TryFingerprintAudioSignal(StringBuilder sb, object rawObj, object val)
        {
            if (rawObj == null && val == null) return false;

            object target = val ?? rawObj;
            var t = target.GetType();
            bool isAudioSigType = t.Name == "Audio_Signal" || t.FullName.Contains("Audio_Signal");

            double[][] jagged = null;
            if (target is double[][] directJagged)
            {
                jagged = directJagged;
            }
            else if (rawObj is GH_Goo<double[][]> gooJagged && gooJagged.Value != null)
            {
                jagged = gooJagged.Value;
            }
            else if (isAudioSigType)
            {
                try
                {
                    var prop = t.GetProperty("Value");
                    if (prop != null) jagged = prop.GetValue(target) as double[][];
                    if (jagged == null)
                    {
                        var fld = t.GetField("Value");
                        if (fld != null) jagged = fld.GetValue(target) as double[][];
                    }
                }
                catch { }
            }

            if (jagged == null && !isAudioSigType)
            {
                return false;
            }

            int fs = 0;
            int[] direct = null;
            try
            {
                var fsProp = t.GetProperty("SampleFrequency") ?? t.GetProperty("SamplingFrequency");
                if (fsProp != null) fs = Convert.ToInt32(fsProp.GetValue(target));
                else
                {
                    var fsFld = t.GetField("SamplingFrequency") ?? t.GetField("SampleFrequency");
                    if (fsFld != null) fs = Convert.ToInt32(fsFld.GetValue(target));
                }

                var dirProp = t.GetProperty("Direct_Sample") ?? t.GetProperty("Sample_of_Direct");
                if (dirProp != null) direct = dirProp.GetValue(target) as int[];
                else
                {
                    var dirFld = t.GetField("Sample_of_Direct") ?? t.GetField("Direct_Sample");
                    if (dirFld != null) direct = dirFld.GetValue(target) as int[];
                }
            }
            catch { }

            sb.Append($"AudioSignal[Fs={fs};Ch={jagged?.Length ?? 0};");
            if (direct != null)
            {
                sb.Append($"Dir=[{string.Join(",", direct)}];");
            }

            if (jagged != null)
            {
                using (var md5 = MD5.Create())
                {
                    for (int c = 0; c < jagged.Length; c++)
                    {
                        var ch = jagged[c];
                        if (ch == null)
                        {
                            sb.Append($"Ch{c}:null;");
                            continue;
                        }

                        sb.Append($"Ch{c}:L={ch.Length}:");
                        if (ch.Length > 0)
                        {
                            byte[] bytes = new byte[ch.Length * sizeof(double)];
                            Buffer.BlockCopy(ch, 0, bytes, 0, bytes.Length);
                            byte[] hash = md5.ComputeHash(bytes);
                            for (int i = 0; i < hash.Length; i++)
                            {
                                sb.Append(hash[i].ToString("x2"));
                            }
                        }
                        sb.Append(";");
                    }
                }
            }
            sb.Append("]");
            return true;
        }

        // =========================================================================
        // INSPEÃ‡ÃƒO DE MATERIAIS ACÃšSTICOS
        // =========================================================================
        private static bool TryFingerprintMaterial(StringBuilder sb, object rawObj, object val, double threshold)
        {
            object target = val ?? rawObj;
            if (target == null) return false;

            var t = target.GetType();
            string typeName = t.Name;
            string typeFullName = t.FullName ?? "";

            // 1. SuperfÃ­cie acÃºstica com material (AcousticSurfaceData)
            if (typeName == "AcousticSurfaceData" || typeFullName.Contains("AcousticSurfaceData"))
            {
                sb.Append("AcousticSurface[");
                try
                {
                    var matProp = t.GetProperty("Material")?.GetValue(target);
                    if (matProp != null)
                    {
                        TryFingerprintMaterial(sb, matProp, matProp, threshold);
                    }
                    var stProp = t.GetProperty("SurfaceType")?.GetValue(target);
                    if (stProp != null) sb.Append($";Type={stProp}");
                    var areaProp = t.GetProperty("AreaM2")?.GetValue(target);
                    if (areaProp != null) sb.Append($";Area={FormatNumber(Convert.ToDouble(areaProp), threshold)}");
                    var geomProp = t.GetProperty("Geometry")?.GetValue(target);
                    if (geomProp != null)
                    {
                        sb.Append(";Geom=");
                        TryFingerprintGeometry(sb, geomProp, threshold);
                    }
                }
                catch { }
                sb.Append("]");
                return true;
            }

            // 2. Verifica se Ã© um objeto de material
            bool isMat = typeName.Contains("Material") || 
                         typeFullName.Contains("Material") || 
                         t.GetProperty("Absorption") != null || 
                         t.GetMethod("Coefficient_A_Broad") != null;

            if (!isMat) return false;

            sb.Append($"Material:{typeName}[");

            // Nome
            try
            {
                var nameProp = t.GetProperty("Name") ?? t.GetProperty("NickName");
                if (nameProp != null)
                {
                    string nameVal = nameProp.GetValue(target) as string;
                    if (!string.IsNullOrEmpty(nameVal)) sb.Append($"Name={nameVal};");
                }
            }
            catch { }

            // AbsorÃ§Ã£o (ABS)
            bool gotAbs = false;
            try
            {
                var absProp = t.GetProperty("Absorption") ?? t.GetProperty("Abs");
                if (absProp != null)
                {
                    var absVal = absProp.GetValue(target);
                    if (absVal is IEnumerable<double> absEnum)
                    {
                        sb.Append("ABS=[");
                        foreach (var v in absEnum) sb.Append(FormatNumber(v, threshold) + ",");
                        sb.Append("];");
                        gotAbs = true;
                    }
                }
            }
            catch { }

            if (!gotAbs)
            {
                try
                {
                    var mth = t.GetMethod("Coefficient_A_Broad", Type.EmptyTypes);
                    if (mth != null)
                    {
                        var absArr = mth.Invoke(target, null) as double[];
                        if (absArr != null)
                        {
                            sb.Append("ABS=[");
                            foreach (var v in absArr) sb.Append(FormatNumber(v, threshold) + ",");
                            sb.Append("];");
                            gotAbs = true;
                        }
                    }
                }
                catch { }
            }

            // Espalhamento (SCT)
            try
            {
                var sctProp = t.GetProperty("Scattering") ?? t.GetProperty("Sct");
                if (sctProp != null)
                {
                    var sctVal = sctProp.GetValue(target);
                    if (sctVal is IEnumerable<double> sctEnum)
                    {
                        sb.Append("SCT=[");
                        foreach (var v in sctEnum) sb.Append(FormatNumber(v, threshold) + ",");
                        sb.Append("];");
                    }
                }
                else
                {
                    var sctMth = t.GetMethod("Coefficient", Type.EmptyTypes);
                    if (sctMth != null)
                    {
                        var sctArr = sctMth.Invoke(target, null) as double[];
                        if (sctArr != null)
                        {
                            sb.Append("SCT=[");
                            foreach (var v in sctArr) sb.Append(FormatNumber(v, threshold) + ",");
                            sb.Append("];");
                        }
                    }
                }
            }
            catch { }

            // TransmissÃ£o (TRN)
            try
            {
                var trnProp = t.GetProperty("Transmission") ?? t.GetProperty("Trn") ?? t.GetProperty("TL");
                if (trnProp != null)
                {
                    var trnVal = trnProp.GetValue(target);
                    if (trnVal is IEnumerable<double> trnEnum)
                    {
                        sb.Append("TRN=[");
                        foreach (var v in trnEnum) sb.Append(FormatNumber(v, threshold) + ",");
                        sb.Append("];");
                    }
                }
            }
            catch { }

            // Cor
            try
            {
                var colProp = t.GetProperty("DisplayColor") ?? t.GetProperty("DiffuseColor") ?? t.GetProperty("Color");
                if (colProp != null)
                {
                    var colVal = colProp.GetValue(target);
                    if (colVal is Color c)
                    {
                        sb.Append($"Color={c.ToArgb()};");
                    }
                }
            }
            catch { }

            sb.Append("]");
            return true;
        }

        // =========================================================================
        // INSPEÃ‡ÃƒO DE GEOMETRIAS
        // =========================================================================
        private static bool TryFingerprintGeometry(StringBuilder sb, object val, double threshold)
        {
            if (val is Mesh m)
            {
                sb.Append($"Mesh[V={m.Vertices.Count};F={m.Faces.Count};");
                var bbox = m.GetBoundingBox(true);
                sb.Append($"BB=({FormatNumber(bbox.Min.X, threshold)},{FormatNumber(bbox.Min.Y, threshold)},{FormatNumber(bbox.Min.Z, threshold)})-({FormatNumber(bbox.Max.X, threshold)},{FormatNumber(bbox.Max.Y, threshold)},{FormatNumber(bbox.Max.Z, threshold)});");
                int vCount = m.Vertices.Count;
                if (vCount > 0)
                {
                    sb.Append("Vpts=[");
                    int step = Math.Max(1, vCount / 16);
                    for (int i = 0; i < vCount; i += step)
                    {
                        var pt = m.Vertices.Point3dAt(i);
                        sb.Append($"({FormatNumber(pt.X, threshold)},{FormatNumber(pt.Y, threshold)},{FormatNumber(pt.Z, threshold)})");
                    }
                    sb.Append("];");
                }
                sb.Append("]");
                return true;
            }

            if (val is Brep b)
            {
                sb.Append($"Brep[F={b.Faces.Count};E={b.Edges.Count};V={b.Vertices.Count};");
                var bbox = b.GetBoundingBox(true);
                sb.Append($"BB=({FormatNumber(bbox.Min.X, threshold)},{FormatNumber(bbox.Min.Y, threshold)},{FormatNumber(bbox.Min.Z, threshold)})-({FormatNumber(bbox.Max.X, threshold)},{FormatNumber(bbox.Max.Y, threshold)},{FormatNumber(bbox.Max.Z, threshold)});");
                sb.Append("]");
                return true;
            }

            if (val is Curve crv)
            {
                sb.Append($"Curve[L={FormatNumber(crv.GetLength(), threshold)};Closed={crv.IsClosed};");
                var ptS = crv.PointAtStart;
                var ptE = crv.PointAtEnd;
                var ptM = crv.PointAt(0.5 * (crv.Domain.T0 + crv.Domain.T1));
                sb.Append($"S=({FormatNumber(ptS.X, threshold)},{FormatNumber(ptS.Y, threshold)},{FormatNumber(ptS.Z, threshold)});");
                sb.Append($"M=({FormatNumber(ptM.X, threshold)},{FormatNumber(ptM.Y, threshold)},{FormatNumber(ptM.Z, threshold)});");
                sb.Append($"E=({FormatNumber(ptE.X, threshold)},{FormatNumber(ptE.Y, threshold)},{FormatNumber(ptE.Z, threshold)})];");
                return true;
            }

            if (val is Point3d pt3)
            {
                sb.Append($"Pt({FormatNumber(pt3.X, threshold)},{FormatNumber(pt3.Y, threshold)},{FormatNumber(pt3.Z, threshold)})");
                return true;
            }

            if (val is Vector3d vec)
            {
                sb.Append($"Vec({FormatNumber(vec.X, threshold)},{FormatNumber(vec.Y, threshold)},{FormatNumber(vec.Z, threshold)})");
                return true;
            }

            if (val is Plane pl)
            {
                sb.Append($"Pl[O=({FormatNumber(pl.OriginX, threshold)},{FormatNumber(pl.OriginY, threshold)},{FormatNumber(pl.OriginZ, threshold)});Z=({FormatNumber(pl.ZAxis.X, threshold)},{FormatNumber(pl.ZAxis.Y, threshold)},{FormatNumber(pl.ZAxis.Z, threshold)})]");
                return true;
            }

            if (val is Transform xform)
            {
                sb.Append($"Xform[{FormatNumber(xform.M00, threshold)},{FormatNumber(xform.M01, threshold)},{FormatNumber(xform.M02, threshold)},{FormatNumber(xform.M03, threshold)}]");
                return true;
            }

            return false;
        }

        // =========================================================================
        // RESUMO LEGÃVEL PARA DIAGNÃ“STICO
        // =========================================================================
        public static string GetReadableSummary(object rawObj)
        {
            if (rawObj == null) return "null";
            object val = rawObj;
            if (rawObj is IGH_Goo goo) val = goo.SafeScriptVariable() ?? rawObj;
            if (val is GH_ObjectWrapper ow && ow.Value != null) val = ow.Value;

            var t = val.GetType();
            if (t.Name == "Audio_Signal" || t.FullName.Contains("Audio_Signal") || val is double[][])
            {
                int fs = 0;
                int chCount = 1;
                int samples = 0;
                try
                {
                    var fsP = t.GetProperty("SampleFrequency") ?? t.GetProperty("SamplingFrequency");
                    if (fsP != null) fs = Convert.ToInt32(fsP.GetValue(val));
                    var chP = t.GetProperty("ChannelCount");
                    if (chP != null) chCount = Convert.ToInt32(chP.GetValue(val));
                    var cP = t.GetProperty("Count");
                    if (cP != null) samples = Convert.ToInt32(cP.GetValue(val));
                    else if (val is double[][] darr && darr.Length > 0 && darr[0] != null) samples = darr[0].Length;
                }
                catch { }
                return $"Audio Signal ({chCount} ch, {samples} smp @ {fs} Hz)";
            }

            if (t.Name.Contains("Material") || t.FullName.Contains("Material"))
            {
                string name = "Material";
                try
                {
                    var nP = t.GetProperty("Name");
                    if (nP != null) name = nP.GetValue(val)?.ToString() ?? name;
                    var absP = t.GetProperty("Absorption") ?? t.GetProperty("Abs");
                    if (absP != null && absP.GetValue(val) is double[] arr && arr.Length >= 5)
                    {
                        return $"Material '{name}' [Î±: {arr[4]:F2} @ 1kHz]";
                    }
                }
                catch { }
                return $"Material '{name}'";
            }

            if (val is Mesh m) return $"Malha ({m.Vertices.Count}V, {m.Faces.Count}F)";
            if (val is Brep b) return $"Brep ({b.Faces.Count} faces)";
            if (val is Curve c) return $"Curva (comp: {c.GetLength():F2})";
            if (val is Point3d pt) return $"Ponto ({pt.X:F2}, {pt.Y:F2}, {pt.Z:F2})";
            if (val is PillBundle pbun) return $"Bundle [{pbun.Namespace}] ({pbun.Entries.Count} itens)";

            return rawObj.ToString();
        }

        public static string FormatNumber(double val, double threshold)
        {
            if (threshold > 0.0)
            {
                double rounded = Math.Round(val / threshold) * threshold;
                return rounded.ToString("G9", CultureInfo.InvariantCulture);
            }
            return val.ToString("G9", CultureInfo.InvariantCulture);
        }
    }
}