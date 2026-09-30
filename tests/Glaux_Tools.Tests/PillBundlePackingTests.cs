using System;
using System.Collections.Generic;
using Buraqueira_Tools;
using Xunit;

namespace Glaux_Tools.Tests
{
    /// <summary>
    /// Regras do Pill Bundle Pack com Keys e Wires ⚡ apontando para os mesmos transmissores. Até a v1.2.0, o canal
    /// colhido pela chave recebia outro nome ("SRF_Paredes_Pill Transmitter") e entrava de novo, ao lado da entrada do fio
    /// ("SRF_Paredes"). O documento completo, com transmissores e o componente, é testado no Rhino em
    /// tests/rhino/Test-PillBundlePack-KeysWires.ps1.
    /// </summary>
    public class PillBundlePackingTests
    {
        private static PillChannel Channel(string cleanKey, string category, string nick, Guid source) =>
            new PillChannel { CleanKey = cleanKey, RawKey = cleanKey, Category = category, SourceNickName = nick, SourceComponentGuid = source };

        [Theory]
        [InlineData("Pill Transmitter")]
        [InlineData("PillTx")]
        [InlineData("Transmitter")]
        [InlineData("Tx")]
        [InlineData("tx")]
        [InlineData("")]
        [InlineData(null)]
        public void GenericNickNames_DoNotNameTheEntry(string nick)
        {
            Assert.True(PillBundlePacking.IsGenericNickName(nick));
            Assert.Equal("SRF_Paredes", PillBundlePacking.HubEntryKey(Channel("SRF_Paredes", "SRF", nick, Guid.NewGuid())));
        }

        [Fact]
        public void DescriptiveNickNames_KeepTheirRule()
        {
            Assert.False(PillBundlePacking.IsGenericNickName("Paredes"));
            // Apelido já contido na chave: fica a chave
            Assert.Equal("SRF_Paredes", PillBundlePacking.HubEntryKey(Channel("SRF_Paredes", "SRF", "Paredes", Guid.NewGuid())));
            // Apelido diferente: sufixo
            Assert.Equal("SRF_Paredes_Laterais", PillBundlePacking.HubEntryKey(Channel("SRF_Paredes", "SRF", "Laterais", Guid.NewGuid())));
            // Chave genérica ou igual à categoria: o apelido vira o nome
            Assert.Equal("Laterais", PillBundlePacking.HubEntryKey(Channel("GEN", "GEN", "Laterais", Guid.NewGuid())));
            Assert.Equal("Laterais", PillBundlePacking.HubEntryKey(Channel("SRF", "SRF", "Laterais", Guid.NewGuid())));
            // Apelido igual à chave
            Assert.Equal("SRF_Paredes", PillBundlePacking.HubEntryKey(Channel("SRF_Paredes", "SRF", "srf_paredes", Guid.NewGuid())));
        }

        [Fact]
        public void ChannelOfAWiredTransmitter_IsAlreadyPacked()
        {
            Guid wired = Guid.NewGuid(), other = Guid.NewGuid();
            var wiredSet = new HashSet<Guid> { wired };

            Assert.True(PillBundlePacking.IsPackedByWire(Channel("SRF_Paredes", "SRF", "Pill Transmitter", wired), wiredSet));
            // Mesmo nome, outro transmissor: não é o mesmo dado
            Assert.False(PillBundlePacking.IsPackedByWire(Channel("SRF_Paredes", "SRF", "Pill Transmitter", other), wiredSet));
            // Canal sem transmissor conhecido, conjunto vazio ou canal nulo
            Assert.False(PillBundlePacking.IsPackedByWire(Channel("SRF_Paredes", "SRF", "", Guid.Empty), new HashSet<Guid> { Guid.Empty }));
            Assert.False(PillBundlePacking.IsPackedByWire(Channel("SRF_Paredes", "SRF", "", wired), new HashSet<Guid>()));
            Assert.False(PillBundlePacking.IsPackedByWire(null, wiredSet));
            Assert.False(PillBundlePacking.IsPackedByWire(Channel("SRF_Paredes", "SRF", "", wired), null));
        }
    }
}
