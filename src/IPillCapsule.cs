using System.Drawing;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Contrato da cápsula visual Pill (badge de categoria, rótulo e LED de status) desenhada por
    /// <see cref="Pill_Attributes"/>. Componentes novos implementam esta interface em vez de exigir
    /// um ramo específico no renderizador.
    /// </summary>
    public interface IPillCapsule
    {
        /// <summary>Texto principal da cápsula (chave, arquivo, nome do conjunto).</summary>
        string CapsuleKey { get; }

        /// <summary>Texto curto do badge (ex.: "DB", "VAULT", "IO").</summary>
        string CapsuleCategory { get; }

        /// <summary>Complemento exibido entre colchetes após a chave (opcional).</summary>
        string CapsuleUnit { get; }

        Color CapsuleColor { get; }

        /// <summary>LED verde (ou âmbar, se <see cref="CapsuleWarning"/>); falso = LED vermelho.</summary>
        bool CapsuleOk { get; }

        bool CapsuleWarning { get; }
    }
}
