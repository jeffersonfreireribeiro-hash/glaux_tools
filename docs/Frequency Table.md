---
name: "Frequency Table"
nickname: "FreqTab"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "FrequencyTable_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Frequency Table (`FreqTab`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `FrequencyTable_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Gera a tabela completa de distribuição de frequências (absoluta, relativa %, acumulada) e identifica modas simples e multimodais.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos de entrada. |
| **Bin Count** (`B`) | `Integer` | Quantidade de classes/faixas para dados contínuos (use 0 ou deixe em branco para valores únicos discretos exatos). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Modes** (`Mo`) | `Number` | Moda(s) do conjunto (detecta distribuições unimodais, bimodais e multimodais). |
| **Bins / Values** (`Val`) | `Text` | Rótulos das classes/faixas ou valores únicos analisados. |
| **Frequency** (`f`) | `Integer` | Frequência absoluta (contagem de ocorrências em cada classe). |
| **Relative Freq %** (`f%`) | `Number` | Frequência relativa percentual (f / N * 100%). |
| **Cumulative Freq** (`F`) | `Integer` | Frequência acumulada (soma progressiva das contagens). |
| **Cumulative %** (`F%`) | `Number` | Frequência relativa acumulada percentual (0 a 100%). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
