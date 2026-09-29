using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Buraqueira_Tools.Data;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Xunit;
using Xunit.Abstractions;

namespace Glaux_Tools.Tests
{
    public class DataCoreTests
    {
        private readonly ITestOutputHelper _output;

        public DataCoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> Shapes() => TestTrees.AllShapes();

        public static IEnumerable<object[]> ShapesAndFormats()
        {
            foreach (var shape in TestTrees.AllShapes())
            {
                foreach (TreeFormat f in Enum.GetValues(typeof(TreeFormat)))
                {
                    // O .pilldata usa o leitor nativo do GH_Structure, que resolve os tipos pelo
                    // ComponentServer do Grasshopper (só existe dentro do Rhino): testado à parte
                    if (f == TreeFormat.PillData) continue;
                    yield return new object[] { shape[0], shape[1], f };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void Mapper_RoundTrip_PreservesStructure(string name, GH_Structure<IGH_Goo> tree)
        {
            var warnings = new List<string>();
            var table = TreeMapper.ToTable(tree, warnings);
            var back = TreeMapper.ToTree(table, warnings);

            Assert.Empty(warnings);
            Assert.Equal(tree.DataCount, table.ItemCount); // DataCount do GH já inclui itens nulos
            TestTrees.AssertStructurallyEqual(tree, back);
            _output.WriteLine($"{name}: {table.BranchCount} ramos, {table.ItemCount} itens");
        }

        [Theory]
        [MemberData(nameof(ShapesAndFormats))]
        public void Formats_TextAndFile_RoundTrip(string name, GH_Structure<IGH_Goo> tree, TreeFormat format)
        {
            var warnings = new List<string>();
            var table = TreeMapper.ToTable(tree, warnings);
            table.Metadata["origem"] = "teste, \"aspas\"";

            string text = TreeFormats.ToText(tree, table, format, pretty: true, csvDelimiter: ';');
            var fromText = TreeFormats.FromText(text, format, ';', warnings);
            TestTrees.AssertStructurallyEqual(tree, TreeMapper.ToTree(fromText, warnings));

            byte[] bytes = TreeFormats.ToFileBytes(tree, table, format, pretty: false, csvDelimiter: ',');
            var fromFile = TreeFormats.FromFileBytes(bytes, format, ',', warnings);
            TestTrees.AssertStructurallyEqual(tree, TreeMapper.ToTree(fromFile, warnings));

            Assert.Equal("teste, \"aspas\"", fromText.Metadata["origem"]);
            Assert.Empty(warnings);
            _output.WriteLine($"{name}/{format}: {bytes.Length} bytes");
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void Binary_RoundTrip_IsBitExact(string name, GH_Structure<IGH_Goo> tree)
        {
            var table = TreeMapper.ToTable(tree);
            byte[] once = TreeBinaryCodec.Encode(table);
            byte[] twice = TreeBinaryCodec.Encode(TreeBinaryCodec.Decode(once));
            Assert.Equal(once, twice);
            Assert.Equal(TreeHash.Compute(table), TreeHash.Compute(TreeBinaryCodec.Decode(once)));
            _output.WriteLine($"{name}: {once.Length} bytes");
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void PillData_KeepsPathsAndCounts_CompatibleWithDiskSave(string name, GH_Structure<IGH_Goo> tree)
        {
            // Os valores dependem do registro de tipos do Grasshopper (só dentro do Rhino);
            // aqui validamos caminhos, contagens e a árvore vazia (que o leitor nativo recusa)
            var back = PillDataCodec.Decode(PillDataCodec.Encode(tree));
            Assert.Equal(tree.PathCount, back.PathCount);
            for (int b = 0; b < tree.PathCount; b++)
            {
                Assert.Equal(tree.Paths[b].ToString(), back.Paths[b].ToString());
                Assert.Equal(tree.Branches[b].Count, back.Branches[b].Count);
            }
            _output.WriteLine($"{name}: {back.PathCount} ramos");
        }

        [Fact]
        public void Rows_ExposeRelationalView_WithoutAssumingBranchAsRow()
        {
            var table = TreeMapper.ToTable(TestTrees.Irregular());
            var rows = table.EnumerateRows().ToList();

            Assert.Equal(table.ItemCount, rows.Count);
            Assert.Equal(new[] { 5, 3 }, rows.Last().Path);
            Assert.Equal(6, rows.Last().ItemIndex);
            Assert.Contains(table.Branches, b => b.Items.Count == 0 && b.PathText == "{1}");
            Assert.Contains(table.Branches, b => b.Items.Count == 0 && b.PathText == "{9;9;9}");
            Assert.Equal(3, table.MaxDepth);
            Assert.Equal(1, table.MinDepth);
        }

        [Fact]
        public void FromColumns_PlacesByIndex_FillsGapsWithNull_AndKeepsEmptyBranches()
        {
            var warnings = new List<string>();
            var table = TreeMapper.FromColumns(
                paths: new[] { "{0}", "{0}", "{2;1}" },
                indices: new[] { 3, 0, 0 },
                values: new[] { GlauxValue.FromNumber(30), GlauxValue.FromNumber(0), GlauxValue.FromText("x") },
                branchPaths: new[] { "{0}", "{1}", "{2;1}" },
                warnings: warnings);

            Assert.Empty(warnings);
            Assert.Equal(3, table.BranchCount);
            var b0 = table.Branches.Single(b => b.PathText == "{0}");
            Assert.Equal(4, b0.Items.Count);
            Assert.Equal(0, b0.Items[0].X);
            Assert.True(b0.Items[1].IsNull);
            Assert.True(b0.Items[2].IsNull);
            Assert.Equal(30, b0.Items[3].X);
            Assert.Empty(table.Branches.Single(b => b.PathText == "{1}").Items);
        }

        [Fact]
        public void FromColumns_ReportsDuplicatesAndInvalidPaths()
        {
            var warnings = new List<string>();
            TreeMapper.FromColumns(
                new[] { "{0}", "{0}", "{x}" },
                new[] { 0, 0, 0 },
                new[] { GlauxValue.FromNumber(1), GlauxValue.FromNumber(2), GlauxValue.FromNumber(3) },
                null,
                warnings);
            Assert.Contains(warnings, w => w.Contains("repetido"));
            Assert.Contains(warnings, w => w.Contains("inválido"));
        }

        [Fact]
        public void Hash_IsIdentity_NotFingerprint()
        {
            var a = TreeMapper.ToTable(TestTrees.Large(10, 100));
            var same = TreeMapper.ToTable(TestTrees.Large(10, 100));
            Assert.Equal(TreeHash.Compute(a), TreeHash.Compute(same));

            // Metadados não mudam a identidade dos dados
            same.Metadata["nota"] = "qualquer";
            Assert.Equal(TreeHash.Compute(a), TreeHash.Compute(same));

            // Mudança num item do meio (onde um amostrador não olharia) muda o hash
            var changed = TreeMapper.ToTable(TestTrees.Large(10, 100));
            var mid = changed.Branches[5].Items[57];
            changed.Branches[5].Items[57] = mid.Kind == GlauxValueKind.Number
                ? GlauxValue.FromNumber(mid.X + 1e-12)
                : GlauxValue.FromInteger(mid.Int + 1);
            Assert.NotEqual(TreeHash.Compute(a), TreeHash.Compute(changed));
        }

        [Fact]
        public void Hash_DistinguishesTypePathNullAndEmptyBranch()
        {
            string H(GH_Structure<IGH_Goo> t) => TreeHash.Compute(t);

            var num = new GH_Structure<IGH_Goo>(); num.Append(new GH_Number(1), new GH_Path(0));
            var integer = new GH_Structure<IGH_Goo>(); integer.Append(new GH_Integer(1), new GH_Path(0));
            var otherPath = new GH_Structure<IGH_Goo>(); otherPath.Append(new GH_Number(1), new GH_Path(1));
            var nullItem = new GH_Structure<IGH_Goo>(); nullItem.Append(null, new GH_Path(0));
            var emptyText = new GH_Structure<IGH_Goo>(); emptyText.Append(new GH_String(""), new GH_Path(0));
            var emptyBranch = TestTrees.SingleEmptyBranch();
            var empty = TestTrees.Empty();

            var hashes = new[] { H(num), H(integer), H(otherPath), H(nullItem), H(emptyText), H(emptyBranch), H(empty) };
            Assert.Equal(hashes.Length, hashes.Distinct().Count());
        }

        [Fact]
        public void UnknownType_IsKeptOpaque_AndReencodesWithoutLoss()
        {
            var original = GlauxValue.FromBlob("Plugin.Inexistente.GH_Coisa, PluginInexistente", "Coisa #1", new byte[] { 1, 2, 3, 250 });
            var table = new GlauxTreeTable();
            table.Branches.Add(new GlauxBranch(new[] { 0 }, new List<GlauxValue> { original }));

            var warnings = new List<string>();
            var tree = TreeMapper.ToTree(table, warnings);
            var goo = tree.Branches[0][0];

            Assert.IsType<GH_GlauxOpaqueGoo>(goo);
            Assert.Contains(warnings, w => w.Contains("não está carregado"));
            Assert.Equal(original, TreeMapper.ToTable(tree).Branches[0].Items[0]);
        }

        [Fact]
        public void Integer_OutOfGrasshopperRange_BecomesNumberWithWarning()
        {
            var warnings = new List<string>();
            var goo = GooCodec.Decode(GlauxValue.FromInteger(long.MaxValue), warnings);
            Assert.IsType<GH_Number>(goo);
            Assert.Single(warnings);
        }

        [Fact]
        public void Csv_ParsesQuotesNewlinesAndRejectsTabularCsv()
        {
            string csv = "path,index,type,value,data\r\n{0},0,Text,\"a,\"\"b\"\"\nc\",\r\n{0},1,Number,2.5,\r\n{3},-1,,,\r\n";
            var table = TreeCsvCodec.Deserialize(csv);
            Assert.Equal("a,\"b\"\nc", table.Branches[0].Items[0].Text);
            Assert.Equal(2.5, table.Branches[0].Items[1].X);
            Assert.Empty(table.Branches[1].Items);

            var ex = Assert.Throws<InvalidDataException>(() => TreeCsvCodec.Deserialize("nome,idade\r\nAna,30\r\n"));
            Assert.Contains("Import CSV", ex.Message);
            Assert.Throws<InvalidDataException>(() => TreeCsvCodec.Deserialize("path,index,type,value,data\r\n{0},x,Number,1,\r\n"));
        }

        [Fact]
        public void Json_RejectsOtherFormatsAndNewerVersions()
        {
            Assert.Throws<InvalidDataException>(() => TreeJsonCodec.Deserialize("{\"format\":\"outro\",\"branches\":[]}"));
            Assert.Throws<InvalidDataException>(() => TreeJsonCodec.Deserialize("{\"format\":\"glaux.tree\",\"version\":99,\"branches\":[]}"));
            Assert.Throws<InvalidDataException>(() => TreeJsonCodec.Deserialize("[1,2]"));
            Assert.Throws<InvalidDataException>(() => TreeJsonCodec.Deserialize("{\"format\":\"glaux.tree\""));
        }

        [Fact]
        public void Binary_CorruptOrTruncatedData_FailsCleanly()
        {
            byte[] good = TreeBinaryCodec.Encode(TreeMapper.ToTable(TestTrees.Irregular()));
            Assert.Throws<InvalidDataException>(() => TreeBinaryCodec.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            for (int cut = 8; cut < good.Length; cut += 7)
            {
                var truncated = good.Take(cut).ToArray();
                var ex = Record.Exception(() => TreeBinaryCodec.Decode(truncated));
                Assert.True(ex is InvalidDataException || ex is EndOfStreamException, $"corte em {cut}: {ex?.GetType().Name}");
            }

            // Contagem absurda não pode alocar gigabytes
            var bogus = (byte[])good.Clone();
            BitConverter.GetBytes(int.MaxValue).CopyTo(bogus, 8);
            Assert.Throws<InvalidDataException>(() => TreeBinaryCodec.Decode(bogus));
        }

        [Fact]
        public void NumberFormat_IsExactForHardValues()
        {
            var values = new[] { 0.1, 0.3, 1.0 / 3.0, 2.2250738585072014E-308, 1.7976931348623157E+308, 5e-324, -0.0, 123456789.123456789, 9007199254740993.0 };
            foreach (var v in values)
            {
                string s = GlauxNumberFormat.Format(v);
                Assert.True(GlauxNumberFormat.TryParse(s, out double back));
                Assert.Equal(BitConverter.DoubleToInt64Bits(v), BitConverter.DoubleToInt64Bits(back));
            }
        }

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Benchmark_LargeTree_AllFormats()
        {
            var tree = TestTrees.Large(1000, 200); // 200 000 itens
            var sw = Stopwatch.StartNew();
            var table = TreeMapper.ToTable(tree);
            long mapMs = sw.ElapsedMilliseconds;

            sw.Restart();
            string hash = TreeHash.Compute(table);
            long hashMs = sw.ElapsedMilliseconds;
            _output.WriteLine($"Mapper: {mapMs} ms | Hash: {hashMs} ms ({TreeHash.Short(hash)})");

            foreach (TreeFormat f in new[] { TreeFormat.Binary, TreeFormat.Json, TreeFormat.Csv })
            {
                sw.Restart();
                byte[] bytes = TreeFormats.ToFileBytes(tree, table, f, false, ',');
                long encMs = sw.ElapsedMilliseconds;
                sw.Restart();
                var back = TreeFormats.FromFileBytes(bytes, f, ',', null);
                long decMs = sw.ElapsedMilliseconds;
                Assert.Equal(hash, TreeHash.Compute(back));
                _output.WriteLine($"{f,-8} {bytes.Length / 1024.0 / 1024.0,6:F2} MB | escrita {encMs,5} ms | leitura {decMs,5} ms");
            }

            sw.Restart();
            var rebuilt = TreeMapper.ToTree(table);
            _output.WriteLine($"Tabela → GH_Structure: {sw.ElapsedMilliseconds} ms");
            Assert.Equal(tree.DataCount, rebuilt.DataCount);

            // Limites generosos: só pegam regressões grosseiras (ex.: algoritmo quadrático)
            Assert.True(mapMs < 5000, $"Mapper lento: {mapMs} ms");
            Assert.True(hashMs < 5000, $"Hash lento: {hashMs} ms");
        }

        private static int CountNulls(GH_Structure<IGH_Goo> tree)
        {
            int n = 0;
            foreach (var b in tree.Branches)
                foreach (var g in b)
                    if (g == null) n++;
            return n;
        }
    }
}
