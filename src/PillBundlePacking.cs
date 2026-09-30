using System;
using System.Collections.Generic;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Regras do Pill Bundle Pack que não dependem do canvas: o nome de cada entrada e a deduplicação entre o caminho
    /// por fio (Wires ⚡) e o caminho pelo barramento (Keys).
    /// </summary>
    internal static class PillBundlePacking
    {
        // Apelidos que não descrevem o dado (nome e apelido padrão do Pill Transmitter e abreviações): a entrada fica com a chave.
        private static readonly HashSet<string> s_genericNickNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PillTx", "Pill Transmitter", "Transmitter", "Tx"
        };

        internal static bool IsGenericNickName(string nickName) =>
            string.IsNullOrWhiteSpace(nickName) || s_genericNickNames.Contains(nickName);

        /// <summary>
        /// Nome da entrada de um canal colhido do barramento. Usa a mesma lista de apelidos genéricos do caminho por fio,
        /// para que ligar ou desligar os Wires ⚡ não renomeie as entradas (até a v1.2.0, "Pill Transmitter" e "Tx"
        /// viravam sufixo: "SRF_Paredes_Pill Transmitter").
        /// </summary>
        internal static string HubEntryKey(PillChannel channel)
        {
            string itemKey = channel.CleanKey ?? "";
            string nick = channel.SourceNickName;
            if (IsGenericNickName(nick) || nick.Equals(itemKey, StringComparison.OrdinalIgnoreCase)) return itemKey;

            if (itemKey.Equals("GEN", StringComparison.OrdinalIgnoreCase) || itemKey.Equals(channel.Category ?? "", StringComparison.OrdinalIgnoreCase))
                return nick;
            return itemKey.Contains(nick) ? itemKey : $"{itemKey}_{nick}";
        }

        /// <summary>
        /// Um canal do barramento já está no pacote quando o transmissor que o publica está ligado em Wires ⚡: os dados
        /// dele entraram pelo fio. A identidade é a instância do transmissor, não o nome da entrada, que os dois caminhos
        /// podem montar de formas diferentes (a causa da duplicação até a v1.2.0).
        /// </summary>
        internal static bool IsPackedByWire(PillChannel channel, ICollection<Guid> wiredTransmitters) =>
            channel != null && channel.SourceComponentGuid != Guid.Empty && wiredTransmitters != null &&
            wiredTransmitters.Contains(channel.SourceComponentGuid);
    }
}
