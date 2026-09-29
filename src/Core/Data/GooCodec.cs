using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// Conversão sem perda entre <see cref="IGH_Goo"/> e <see cref="GlauxValue"/>.
    /// Primitivos do Grasshopper viram valores legíveis; qualquer outro Goo é gravado com o próprio
    /// Write/Read do GH_IO (o mesmo mecanismo do arquivo .gh), identificado pelo nome do tipo.
    /// </summary>
    public static class GooCodec
    {
        private const string ChunkName = "GlauxGoo";
        private const int MaxDisplayTextLength = 256;

        private static readonly ConcurrentDictionary<string, Type> s_typeCache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>Identidade estável de um tipo Goo: "Namespace.Tipo, Assembly" (sem versão, para sobreviver a atualizações).</summary>
        public static string TypeIdentity(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        public static GlauxValue Encode(IGH_Goo goo, ICollection<string> warnings = null)
        {
            if (goo == null) return GlauxValue.Null;

            // Tipos exatos: subclasses de plugins seguem pelo caminho genérico para não perder o tipo real
            Type t = goo.GetType();
            if (t == typeof(GH_Number)) return GlauxValue.FromNumber(((GH_Number)goo).Value);
            if (t == typeof(GH_Integer)) return GlauxValue.FromInteger(((GH_Integer)goo).Value);
            if (t == typeof(GH_Boolean)) return GlauxValue.FromBoolean(((GH_Boolean)goo).Value);
            if (t == typeof(GH_String)) return GlauxValue.FromText(((GH_String)goo).Value);
            if (t == typeof(GH_Point))
            {
                var p = ((GH_Point)goo).Value;
                return GlauxValue.FromPoint(p.X, p.Y, p.Z);
            }
            if (t == typeof(GH_Vector))
            {
                var v = ((GH_Vector)goo).Value;
                return GlauxValue.FromVector(v.X, v.Y, v.Z);
            }
            if (t == typeof(GH_Interval))
            {
                var iv = ((GH_Interval)goo).Value;
                return GlauxValue.FromInterval(iv.T0, iv.T1);
            }
            if (t == typeof(GH_Colour)) return GlauxValue.FromColour(((GH_Colour)goo).Value.ToArgb());
            if (t == typeof(GH_Time)) return GlauxValue.FromTime(((GH_Time)goo).Value);
            if (t == typeof(GH_Guid)) return GlauxValue.FromGuid(((GH_Guid)goo).Value);
            if (goo is GH_GlauxOpaqueGoo opaque && opaque.Value != null) return opaque.Value;

            return EncodeBlob(goo, warnings);
        }

        private static GlauxValue EncodeBlob(IGH_Goo goo, ICollection<string> warnings)
        {
            string identity = TypeIdentity(goo.GetType());
            string display = SafeDisplay(goo);
            try
            {
                var chunk = new GH_LooseChunk(ChunkName);
                if (goo.Write(chunk))
                {
                    return GlauxValue.FromBlob(identity, display, chunk.Serialize_Binary());
                }
                warnings?.Add($"Tipo '{goo.TypeName}' não suporta serialização; só o texto foi preservado.");
            }
            catch (Exception ex)
            {
                warnings?.Add($"Falha ao serializar '{goo.TypeName}': {ex.Message}. Só o texto foi preservado.");
            }
            return GlauxValue.FromBlob(identity, display, null);
        }

        public static IGH_Goo Decode(GlauxValue value, ICollection<string> warnings = null)
        {
            if (value == null) return null;
            switch (value.Kind)
            {
                case GlauxValueKind.Null: return null;
                case GlauxValueKind.Number: return new GH_Number(value.X);
                case GlauxValueKind.Integer:
                    if (value.Int < int.MinValue || value.Int > int.MaxValue)
                    {
                        warnings?.Add($"Inteiro {value.Int} fora da faixa de 32 bits do Grasshopper; convertido para Number.");
                        return new GH_Number(value.Int);
                    }
                    return new GH_Integer((int)value.Int);
                case GlauxValueKind.Boolean: return new GH_Boolean(value.BooleanValue);
                case GlauxValueKind.Text: return new GH_String(value.Text);
                case GlauxValueKind.Point: return new GH_Point(new Point3d(value.X, value.Y, value.Z));
                case GlauxValueKind.Vector: return new GH_Vector(new Vector3d(value.X, value.Y, value.Z));
                case GlauxValueKind.Interval: return new GH_Interval(new Interval(value.X, value.Y));
                case GlauxValueKind.Colour: return new GH_Colour(Color.FromArgb((int)value.Int));
                case GlauxValueKind.Time: return new GH_Time(value.TimeValue);
                case GlauxValueKind.Guid: return new GH_Guid(value.GuidValue);
                case GlauxValueKind.Blob: return DecodeBlob(value, warnings);
                default: return null;
            }
        }

        private static IGH_Goo DecodeBlob(GlauxValue value, ICollection<string> warnings)
        {
            if (value.Blob == null)
            {
                warnings?.Add($"Item '{value.DisplayType}' foi salvo sem dados serializáveis; mantido como valor opaco.");
                return new GH_GlauxOpaqueGoo(value);
            }

            Type type = ResolveGooType(value.TypeTag);
            if (type == null)
            {
                warnings?.Add($"Tipo '{value.TypeTag}' não está carregado no Grasshopper; mantido como valor opaco (sem perda).");
                return new GH_GlauxOpaqueGoo(value);
            }

            try
            {
                var goo = (IGH_Goo)Activator.CreateInstance(type);
                var chunk = new GH_LooseChunk(ChunkName);
                chunk.Deserialize_Binary(value.Blob);
                if (goo.Read(chunk)) return goo;
                warnings?.Add($"Tipo '{value.DisplayType}' recusou os dados gravados; mantido como valor opaco.");
            }
            catch (Exception ex)
            {
                warnings?.Add($"Falha ao restaurar '{value.DisplayType}': {ex.Message}. Mantido como valor opaco.");
            }
            return new GH_GlauxOpaqueGoo(value);
        }

        /// <summary>Resolve "Namespace.Tipo, Assembly" entre os assemblies carregados (ignora versão).</summary>
        public static Type ResolveGooType(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            if (s_typeCache.TryGetValue(identity, out Type cached)) return cached;

            Type found = null;
            string fullName = identity;
            string assemblyName = null;
            int comma = identity.IndexOf(',');
            if (comma >= 0)
            {
                fullName = identity.Substring(0, comma).Trim();
                assemblyName = identity.Substring(comma + 1).Trim();
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName != null && !string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                Type candidate = null;
                try { candidate = asm.GetType(fullName, false); } catch { }
                if (candidate != null && typeof(IGH_Goo).IsAssignableFrom(candidate) && !candidate.IsAbstract &&
                    candidate.GetConstructor(Type.EmptyTypes) != null)
                {
                    found = candidate;
                    break;
                }
            }

            // Só guarda acertos: um plugin pode ser carregado depois
            if (found != null) s_typeCache[identity] = found;
            return found;
        }

        private static string SafeDisplay(IGH_Goo goo)
        {
            string text;
            try { text = goo.ToString(); }
            catch { text = goo.TypeName; }
            if (text != null && text.Length > MaxDisplayTextLength) text = text.Substring(0, MaxDisplayTextLength);
            return text;
        }
    }

    /// <summary>
    /// Guarda um valor cujo tipo original não está disponível (plugin não carregado) sem perder os bytes:
    /// reexportar ou regravar este item devolve exatamente o que foi lido.
    /// </summary>
    public class GH_GlauxOpaqueGoo : GH_Goo<GlauxValue>
    {
        public GH_GlauxOpaqueGoo()
        {
            Value = GlauxValue.Null;
        }

        public GH_GlauxOpaqueGoo(GlauxValue value)
        {
            Value = value ?? GlauxValue.Null;
        }

        public override bool IsValid => Value != null && !Value.IsNull;
        public override string TypeName => "Glaux Opaque";
        public override string TypeDescription => "Valor preservado de um tipo indisponível no Grasshopper atual (Glaux Tools)";

        public override IGH_Goo Duplicate() => new GH_GlauxOpaqueGoo(Value);

        public override string ToString()
        {
            if (Value == null || Value.IsNull) return "Glaux Opaque (vazio)";
            return $"[{Value.DisplayType} indisponível] {Value.Text}";
        }

        public override bool Write(GH_IWriter writer)
        {
            var v = Value ?? GlauxValue.Null;
            writer.SetInt32("Kind", (int)v.Kind);
            writer.SetString("TypeTag", v.TypeTag ?? "");
            writer.SetString("Text", v.Text ?? "");
            writer.SetBoolean("HasBlob", v.Blob != null);
            if (v.Blob != null) writer.SetByteArray("Blob", v.Blob);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            var kind = (GlauxValueKind)reader.GetInt32("Kind");
            string tag = reader.GetString("TypeTag");
            string text = reader.GetString("Text");
            byte[] blob = reader.GetBoolean("HasBlob") ? reader.GetByteArray("Blob") : null;
            Value = kind == GlauxValueKind.Blob && !string.IsNullOrEmpty(tag)
                ? GlauxValue.FromBlob(tag, text, blob)
                : GlauxValue.Null;
            return true;
        }
    }
}
