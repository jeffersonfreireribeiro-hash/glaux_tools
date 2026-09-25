---
name: "Pill Tree Pivot & Join"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillTreePivot_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Pill Tree Pivot & Join (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PillTreePivot_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore principal de dados N-dimensional (ex: TRs do Wallacei com caminhos {Gen; Ind; Ponto}). |
| **AuxData** (`Aux`) | `Generic` | Árvore auxiliar de metadados (ex: Fitness {Gen; Ind}). Acoplada automaticamente por prefixo de caminho. |
| **DimNames** (`Dim`) | `Text` | Nomes opcionais para as dimensões do caminho (ex: ['Gen', 'Ind', 'Ponto']). Se omitido, usa Dim_0, Dim_1, etc. |
| **ColHeaders** (`Col`) | `Text` | Nomes opcionais para os itens da folha (ex: ['125', '250', '500', '1000', '2000', '4000']). |
| **TopCount** (`Top`) | `Integer` | Filtrar apenas os N melhores indivíduos por score (menor fitness primeiro). 0 ou vazio = todos. |
| **ScoreIndex** (`ScIdx`) | `Integer` | Índice do objetivo em AuxData para o ranking de minimização (-1 = soma de todos os objetivos). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Table** (`Tbl`) | `Generic` | Tabela resultante onde cada ramo {i} representa uma linha completa de dados. |
| **Headers** (`H`) | `Text` | Lista ordenada com os nomes de todas as colunas da tabela. |
| **Preview** (`Prev`) | `Text` | Amostra tabular formatada (até 30 linhas) para visualização rápida no Canvas. |
| **Rank** (`Rank`) | `Text` | Ranking dos melhores indivíduos classificados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
