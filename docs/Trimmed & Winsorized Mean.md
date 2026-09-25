---
name: "Trimmed & Winsorized Mean"
nickname: "TrimWin"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "TrimmedWinsorizedMean_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Trimmed & Winsorized Mean (`TrimWin`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `TrimmedWinsorizedMean_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Calcula medidas robustas de tendência central (Média Truncada / Trimmed Mean e Winsorização) para mitigar o impacto de valores extremos e caudas pesadas.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos. |
| **Cutoff %** (`p`) | `Number` | Porcentagem de corte em cada cauda (ex.: 0.10 ou 10 para 10% na cauda inferior e 10% na superior). Padrão: 0.10 (10%). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Trimmed Mean** (`x̄_trim`) | `Number` | Média truncada (calculada após descartar p% dos menores e maiores valores). |
| **Winsorized Mean** (`x̄_win`) | `Number` | Média winsorizada (calculada substituindo p% dos extremos pelos valores de corte). |
| **Trimmed Data** (`D_trim`) | `Number` | Lista de valores restantes após a remoção dos extremos. |
| **Winsorized Data** (`D_win`) | `Number` | Lista de dados com os extremos substituídos pelos limites de corte. |
| **Lower Cutoff** (`L`) | `Number` | Valor do limite inferior de corte. |
| **Upper Cutoff** (`U`) | `Number` | Valor do limite superior de corte. |
| **Summary** (`Desc`) | `Text` | Resumo das estatísticas robustas calculadas. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
