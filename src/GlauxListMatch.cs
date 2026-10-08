using System;
using System.Collections.Generic;

namespace Buraqueira_Tools
{
    /// <summary>
    /// Modo de correspondência (list matching) entre uma lista de parâmetros e os itens que ela alimenta.
    /// </summary>
    public enum GlauxListMatchMode
    {
        /// <summary>
        /// Regra padrão do Grasshopper e dos componentes Glaux que aplicam um valor por item
        /// (Road CSV Standardizer, Pill Vector Sheet Layout, SumIf, Loss Functions, Ideal RT60...):
        /// item único vale para todos; a lista casa por índice; se acabar, repete o ÚLTIMO valor.
        /// </summary>
        RepeatLast = 0,

        /// <summary>
        /// Opt-in explícito (menu do componente): quando a lista acaba, recomeça do primeiro valor
        /// (<c>lista[i % lista.Count]</c>). Mesma semântica do "Repeat Pattern" do Tree Partition.
        /// </summary>
        Cycle = 1
    }

    /// <summary>
    /// Helper único de list matching dos parâmetros "item ou lista" do Glaux.
    /// Este arquivo existe com o mesmo conteúdo (namespace diferente) em Glaux_Urb e Glaux_Tools:
    /// são assemblies separados e o repositório do Tools é independente. Mantenha os dois idênticos.
    ///
    /// Regras:
    ///   • lista vazia          → nenhum item (sem override / valor default do componente);
    ///   • 1 item               → vale para todos os índices;
    ///   • N itens, N == alvo   → correspondência 1:1 por índice;
    ///   • N itens, N &lt; alvo → RepeatLast (padrão) ou Cycle (opt-in);
    ///   • N itens, N &gt; alvo → os itens excedentes são simplesmente ignorados;
    ///   • índice negativo      → nenhum item (nunca lança IndexOutOfRangeException).
    /// Um item <c>null</c> dentro da lista continua sendo "sem valor" naquela posição (não herda do vizinho).
    /// </summary>
    public static class GlauxListMatch
    {
        /// <summary>Índice real dentro da lista para o item <paramref name="index"/>, ou -1 se não houver.</summary>
        public static int Resolve(int index, int count, GlauxListMatchMode mode = GlauxListMatchMode.RepeatLast)
        {
            if (count <= 0 || index < 0) return -1;
            if (index < count) return index;
            return mode == GlauxListMatchMode.Cycle ? index % count : count - 1;
        }

        /// <summary>Obtém o item correspondente ao índice; devolve default(T) se a lista for nula/vazia.</summary>
        public static T Get<T>(IList<T> list, int index, GlauxListMatchMode mode = GlauxListMatchMode.RepeatLast)
        {
            if (list == null) return default(T);
            int i = Resolve(index, list.Count, mode);
            return i < 0 ? default(T) : list[i];
        }

        /// <summary>
        /// Descreve a política aplicada quando o tamanho da lista difere da quantidade de alvos
        /// (para relatórios). Devolve null quando não há nada a comentar (vazia, 1 item ou igual).
        /// </summary>
        public static string Describe(string paramName, int itemCount, int targetCount, GlauxListMatchMode mode)
        {
            if (itemCount <= 1 || targetCount <= 0 || itemCount == targetCount) return null;
            if (itemCount > targetCount)
                return $"{paramName}: {itemCount} valores para {targetCount} item(ns) — {itemCount - targetCount} excedente(s) ignorado(s).";
            return mode == GlauxListMatchMode.Cycle
                ? $"{paramName}: {itemCount} valores para {targetCount} item(ns) — lista repetida ciclicamente (i % {itemCount})."
                : $"{paramName}: {itemCount} valores para {targetCount} item(ns) — repete o último valor (padrão Grasshopper).";
        }
    }
}
