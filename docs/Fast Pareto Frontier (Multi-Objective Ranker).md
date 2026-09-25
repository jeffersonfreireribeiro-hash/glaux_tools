---
name: "Fast Pareto Frontier (Multi-Objective Ranker)"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "FastPareto_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Fast Pareto Frontier (Multi-Objective Ranker) (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `FastPareto_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Fitness / Data Tree** (`F`) | `Generic` | Árvore de dados contendo os vetores de fitness. Suporta árvore por ramos {indivíduo} OU tabela por colunas {sheet; col} onde cada item [i] é uma linha de dados (ex: CSV/Excel com N linhas). |
| **Directions** (`Dir`) | `Generic` | Direção para cada objetivo:\n0 ou 'min' = Minimizar (Padrão: menor é melhor)\n1 ou 'max' = Maximizar (maior é melhor).\nSe omitido, minimiza todos os objetivos. |
| **Top N** (`N`) | `Integer` | Quantidade de indivíduos/linhas a extrair (ex: 10). Se 0, extrai todos ordenados pelo ranking de Pareto. |
| **Only Rank 1** (`R1`) | `Boolean` | Se True, extrai estritamente apenas as soluções da Fronteira de Pareto Rank 1 (não-dominadas absolutas). Padrão: False. |
| **Columns / Objectives** (`Cols`) | `Generic` | Filtro opcional de índices ou nomes de colunas a avaliar como objetivos (ex: [0, 1, 2] ou lista de índices). Se vazio ou '*', avalia todas as colunas numéricas disponíveis. |
| **By Rows** (`Row`) | `Boolean` | Modo de avaliação:\n- True = Linhas são os indivíduos (cada ramo {col} é uma coluna e cada item [i] é uma linha, ex: CSV/Excel com 100 linhas).\n- False = Ramos são os indivíduos (cada ramo {i} é um indivíduo).\n- Desconectado = Auto-detecta automaticamente com base na topologia da árvore. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Pareto Tree** (`P`) | `Generic` | Árvore com os valores de fitness dos indivíduos/linhas selecionados, ordenados pelo ranking de Pareto. |
| **Selected Paths** (`Paths`) | `Generic` | Lista dos caminhos GH_Path correspondentes aos indivíduos/linhas selecionados (no modo Linhas, caminhos {row}). |
| **Pareto Ranks** (`R`) | `Integer` | Rank de Pareto de cada indivíduo/linha selecionada (1 = Fronteira de Pareto não-dominada, 2 = Segunda fronteira, etc.). |
| **Crowding Distance** (`CD`) | `Number` | Distância de aglomeração (métrica de diversidade espacial das soluções NSGA-II). |
| **Frontier 1 Count** (`N_F1`) | `Integer` | Quantidade total de soluções não-dominadas da Fronteira 1. |
| **Row Indices** (`iRow`) | `Integer` | Lista com os índices das LINHAS originais (0, 1, 2... N-1) ordenadas da melhor para a pior! Ligue no 'List Item' para extrair os modelos 3D, geometrias ou dados das melhores linhas. |
| **Best Row** (`Top1`) | `Integer` | Índice da linha correspondente ao MELHOR resultado absoluto (1º lugar da Fronteira de Pareto Rank 1 com maior diversidade). |
| **Report** (`Rep`) | `Text` | Diagnóstico e resumo da classificação de Pareto (quantidade de linhas avaliadas, objetivos, distribuição dos ranks e Top 10). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
