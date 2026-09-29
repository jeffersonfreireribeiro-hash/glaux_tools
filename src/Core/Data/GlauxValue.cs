using System;
using System.Globalization;

namespace Buraqueira_Tools.Data
{
    /// <summary>
    /// Tipo lógico de um item no modelo canônico de DataTree.
    /// Os valores numéricos do enum fazem parte do formato binário: não reordenar.
    /// </summary>
    public enum GlauxValueKind : byte
    {
        Null = 0,
        Number = 1,
        Integer = 2,
        Boolean = 3,
        Text = 4,
        Point = 5,
        Vector = 6,
        Interval = 7,
        Colour = 8,
        Time = 9,
        Guid = 10,
        Blob = 11
    }

    /// <summary>
    /// Tags de tipo estáveis gravadas no modelo canônico. Para <see cref="GlauxValueKind.Blob"/>
    /// a tag é a identidade do tipo Goo ("NomeCompleto, Assembly"), sem versão.
    /// </summary>
    public static class GlauxTypeTags
    {
        public const string Null = "Null";
        public const string Number = "Number";
        public const string Integer = "Integer";
        public const string Boolean = "Boolean";
        public const string Text = "Text";
        public const string Point = "Point";
        public const string Vector = "Vector";
        public const string Interval = "Interval";
        public const string Colour = "Colour";
        public const string Time = "Time";
        public const string Guid = "Guid";

        public static string ForKind(GlauxValueKind kind)
        {
            switch (kind)
            {
                case GlauxValueKind.Null: return Null;
                case GlauxValueKind.Number: return Number;
                case GlauxValueKind.Integer: return Integer;
                case GlauxValueKind.Boolean: return Boolean;
                case GlauxValueKind.Text: return Text;
                case GlauxValueKind.Point: return Point;
                case GlauxValueKind.Vector: return Vector;
                case GlauxValueKind.Interval: return Interval;
                case GlauxValueKind.Colour: return Colour;
                case GlauxValueKind.Time: return Time;
                case GlauxValueKind.Guid: return Guid;
                default: return null;
            }
        }

        public static bool TryParseKind(string tag, out GlauxValueKind kind)
        {
            switch (tag)
            {
                case Null: kind = GlauxValueKind.Null; return true;
                case Number: kind = GlauxValueKind.Number; return true;
                case Integer: kind = GlauxValueKind.Integer; return true;
                case Boolean: kind = GlauxValueKind.Boolean; return true;
                case Text: kind = GlauxValueKind.Text; return true;
                case Point: kind = GlauxValueKind.Point; return true;
                case Vector: kind = GlauxValueKind.Vector; return true;
                case Interval: kind = GlauxValueKind.Interval; return true;
                case Colour: kind = GlauxValueKind.Colour; return true;
                case Time: kind = GlauxValueKind.Time; return true;
                case Guid: kind = GlauxValueKind.Guid; return true;
                default: kind = GlauxValueKind.Blob; return false;
            }
        }
    }

    /// <summary>
    /// Valor imutável de um item da DataTree no modelo canônico.
    /// Tipos primitivos ficam legíveis e consultáveis; qualquer outro Goo é guardado como blob GH_IO
    /// do próprio objeto (round-trip sem perda enquanto o tipo existir no Grasshopper).
    /// </summary>
    public sealed class GlauxValue : IEquatable<GlauxValue>
    {
        public static readonly GlauxValue Null = new GlauxValue(GlauxValueKind.Null, GlauxTypeTags.Null, 0, 0, 0, 0, null, null);

        private GlauxValue(GlauxValueKind kind, string typeTag, double x, double y, double z, long integer, string text, byte[] blob)
        {
            Kind = kind;
            TypeTag = typeTag;
            X = x;
            Y = y;
            Z = z;
            Int = integer;
            Text = text;
            Blob = blob;
        }

        public GlauxValueKind Kind { get; }

        /// <summary>Tag estável do tipo (ver <see cref="GlauxTypeTags"/>) ou identidade do Goo para blobs.</summary>
        public string TypeTag { get; }

        /// <summary>Number (X), Point/Vector (X, Y, Z), Interval (X = T0, Y = T1).</summary>
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        /// <summary>Integer, Boolean (0/1), Colour (ARGB) e Time (<see cref="DateTime.ToBinary"/>, preserva o Kind).</summary>
        public long Int { get; }

        /// <summary>Text, Guid (formato "D") e texto de exibição dos blobs.</summary>
        public string Text { get; }

        /// <summary>Bytes GH_IO do Goo (só para Blob). Nulo quando o tipo não pôde ser serializado.</summary>
        public byte[] Blob { get; }

        public bool IsNull => Kind == GlauxValueKind.Null;

        /// <summary>Blob sem bytes: o tipo original não pôde ser serializado e só o texto foi preservado.</summary>
        public bool IsLossy => Kind == GlauxValueKind.Blob && Blob == null;

        public static GlauxValue FromNumber(double value) => new GlauxValue(GlauxValueKind.Number, GlauxTypeTags.Number, value, 0, 0, 0, null, null);
        public static GlauxValue FromInteger(long value) => new GlauxValue(GlauxValueKind.Integer, GlauxTypeTags.Integer, 0, 0, 0, value, null, null);
        public static GlauxValue FromBoolean(bool value) => new GlauxValue(GlauxValueKind.Boolean, GlauxTypeTags.Boolean, 0, 0, 0, value ? 1 : 0, null, null);
        public static GlauxValue FromText(string value) => new GlauxValue(GlauxValueKind.Text, GlauxTypeTags.Text, 0, 0, 0, 0, value, null);
        public static GlauxValue FromPoint(double x, double y, double z) => new GlauxValue(GlauxValueKind.Point, GlauxTypeTags.Point, x, y, z, 0, null, null);
        public static GlauxValue FromVector(double x, double y, double z) => new GlauxValue(GlauxValueKind.Vector, GlauxTypeTags.Vector, x, y, z, 0, null, null);
        public static GlauxValue FromInterval(double t0, double t1) => new GlauxValue(GlauxValueKind.Interval, GlauxTypeTags.Interval, t0, t1, 0, 0, null, null);
        public static GlauxValue FromColour(int argb) => new GlauxValue(GlauxValueKind.Colour, GlauxTypeTags.Colour, 0, 0, 0, argb, null, null);
        public static GlauxValue FromTime(DateTime value) => new GlauxValue(GlauxValueKind.Time, GlauxTypeTags.Time, 0, 0, 0, value.ToBinary(), null, null);
        public static GlauxValue FromGuid(Guid value) => new GlauxValue(GlauxValueKind.Guid, GlauxTypeTags.Guid, 0, 0, 0, 0, value.ToString("D"), null);

        public static GlauxValue FromBlob(string typeIdentity, string displayText, byte[] blob)
        {
            if (string.IsNullOrEmpty(typeIdentity)) throw new ArgumentException("A identidade do tipo é obrigatória para blobs.", nameof(typeIdentity));
            return new GlauxValue(GlauxValueKind.Blob, typeIdentity, 0, 0, 0, 0, displayText, blob);
        }

        /// <summary>Recria um valor a partir dos campos brutos (usado pelos codecs).</summary>
        internal static GlauxValue FromRaw(GlauxValueKind kind, string typeTag, double x, double y, double z, long integer, string text, byte[] blob)
        {
            if (kind == GlauxValueKind.Null) return Null;
            return new GlauxValue(kind, kind == GlauxValueKind.Blob ? typeTag : GlauxTypeTags.ForKind(kind), x, y, z, integer, text, blob);
        }

        public bool BooleanValue => Int != 0;
        public DateTime TimeValue => DateTime.FromBinary(Int);
        public Guid GuidValue => System.Guid.Parse(Text);

        /// <summary>Leitura numérica para consultas e validação (Number, Integer, Boolean); nulo para os demais.</summary>
        public double? NumericValue
        {
            get
            {
                switch (Kind)
                {
                    case GlauxValueKind.Number: return X;
                    case GlauxValueKind.Integer: return Int;
                    case GlauxValueKind.Boolean: return Int;
                    default: return null;
                }
            }
        }

        /// <summary>Nome curto do tipo para exibição (para blobs, o nome da classe sem namespace).</summary>
        public string DisplayType
        {
            get
            {
                if (Kind != GlauxValueKind.Blob) return TypeTag;
                string full = TypeTag;
                int comma = full.IndexOf(',');
                if (comma >= 0) full = full.Substring(0, comma);
                int dot = full.LastIndexOf('.');
                return dot >= 0 ? full.Substring(dot + 1) : full;
            }
        }

        /// <summary>Texto legível do valor (invariante), usado em tabelas e no CSV.</summary>
        public string ToDisplayString()
        {
            switch (Kind)
            {
                case GlauxValueKind.Null: return "";
                case GlauxValueKind.Number: return GlauxNumberFormat.Format(X);
                case GlauxValueKind.Integer: return Int.ToString(CultureInfo.InvariantCulture);
                case GlauxValueKind.Boolean: return BooleanValue ? "true" : "false";
                case GlauxValueKind.Text: return Text ?? "";
                case GlauxValueKind.Point:
                case GlauxValueKind.Vector:
                    return GlauxNumberFormat.Format(X) + ";" + GlauxNumberFormat.Format(Y) + ";" + GlauxNumberFormat.Format(Z);
                case GlauxValueKind.Interval: return GlauxNumberFormat.Format(X) + ";" + GlauxNumberFormat.Format(Y);
                case GlauxValueKind.Colour: return "#" + ((int)Int).ToString("X8", CultureInfo.InvariantCulture);
                case GlauxValueKind.Time: return TimeValue.ToString("o", CultureInfo.InvariantCulture);
                case GlauxValueKind.Guid: return Text ?? "";
                case GlauxValueKind.Blob: return Text ?? "";
                default: return "";
            }
        }

        public override string ToString() => IsNull ? "<null>" : $"{DisplayType}: {ToDisplayString()}";

        /// <summary>Igualdade exata (números comparados bit a bit, NaN igual a NaN; blobs byte a byte).</summary>
        public bool Equals(GlauxValue other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            if (Kind != other.Kind) return false;
            if (!string.Equals(TypeTag, other.TypeTag, StringComparison.Ordinal)) return false;
            if (BitConverter.DoubleToInt64Bits(X) != BitConverter.DoubleToInt64Bits(other.X)) return false;
            if (BitConverter.DoubleToInt64Bits(Y) != BitConverter.DoubleToInt64Bits(other.Y)) return false;
            if (BitConverter.DoubleToInt64Bits(Z) != BitConverter.DoubleToInt64Bits(other.Z)) return false;
            if (Int != other.Int) return false;
            if (!string.Equals(Text, other.Text, StringComparison.Ordinal)) return false;
            if (Blob == null || other.Blob == null) return Blob == null && other.Blob == null;
            if (Blob.Length != other.Blob.Length) return false;
            for (int i = 0; i < Blob.Length; i++)
            {
                if (Blob[i] != other.Blob[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is GlauxValue v && Equals(v);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind;
                h = h * 31 + (TypeTag?.GetHashCode() ?? 0);
                h = h * 31 + BitConverter.DoubleToInt64Bits(X).GetHashCode();
                h = h * 31 + BitConverter.DoubleToInt64Bits(Y).GetHashCode();
                h = h * 31 + BitConverter.DoubleToInt64Bits(Z).GetHashCode();
                h = h * 31 + Int.GetHashCode();
                h = h * 31 + (Text?.GetHashCode() ?? 0);
                h = h * 31 + (Blob?.Length ?? -1);
                return h;
            }
        }
    }

    /// <summary>
    /// Formatação numérica invariante com round-trip exato: tenta "R" (curto) e cai para "G17"
    /// quando "R" não reproduz o mesmo double (acontece no .NET Framework para alguns valores).
    /// </summary>
    public static class GlauxNumberFormat
    {
        public const string NaNToken = "NaN";
        public const string PositiveInfinityToken = "Infinity";
        public const string NegativeInfinityToken = "-Infinity";

        public static string Format(double value)
        {
            if (double.IsNaN(value)) return NaNToken;
            if (double.IsPositiveInfinity(value)) return PositiveInfinityToken;
            if (double.IsNegativeInfinity(value)) return NegativeInfinityToken;

            string r = value.ToString("R", CultureInfo.InvariantCulture);
            if (double.TryParse(r, NumberStyles.Float, CultureInfo.InvariantCulture, out double back) &&
                BitConverter.DoubleToInt64Bits(back) == BitConverter.DoubleToInt64Bits(value))
            {
                return r;
            }
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string text, out double value)
        {
            text = (text ?? "").Trim();
            switch (text)
            {
                case NaNToken: value = double.NaN; return true;
                case PositiveInfinityToken: value = double.PositiveInfinity; return true;
                case NegativeInfinityToken: value = double.NegativeInfinity; return true;
            }
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
