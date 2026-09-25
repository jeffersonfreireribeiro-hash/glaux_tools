---
name: "Pill Tree Table Exporter"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillTreeTableExport_Component.cs"
plugin: "Buraqueira Tools"
status: "Descontinuado / Removido"
tags: [componente, grasshopper, glaux_tools, descontinuado]
---

# 🧩 Pill Tree Table Exporter (`Pill_TableOut`)

> [!WARNING] Componente Descontinuado
> Este componente foi descontinuado e removido do build ativo do `Glaux_Tools`. A manipulação de listas e matrizes canônicas (`CSV Export`, listas estruturadas normais do Grasshopper) supre a necessidade sem complexidade extra de DataTrees específicas de exportação.

**Arquivo C#:** `PillTreeTableExport_Component.cs.bak` (preservado em backup)  
**Status:** Descontinuado  

---

## 📝 Descrição (Histórico)


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore principal de dados N-dimensional (ex: TRs {Gen; Ind; Ponto}). |
| **Fitness** (`Fit`) | `Generic` | Árvore de fitness/objetivos (ex: {Gen; Ind}). Acoplada e usada para ranking de minimização. |
| **DimNames** (`Dim`) | `Text` | Nomes das dimensões do caminho (ex: ['Gen', 'Ind', 'Ponto']). |
| **ColHeaders** (`Col`) | `Text` | Nomes dos itens da folha / frequências (ex: ['125', '250', '500', '1000', '2000', '4000']). |
| **SheetDim** (`SheetDim`) | `Integer` | Índice da dimensão do caminho que define a quebra de abas no Excel (Padrão: 0 = uma aba por Geração. -1 = aba única). |
| **TopCount** (`Top`) | `Integer` | Filtro dos N melhores indivíduos classificados (0 = todos). |
| **FilePath** (`P`) | `Text` | Caminho de destino (.xls ou .xml para Excel com abas; .csv para delimitado). |
| **Write** (`W`) | `Boolean` | Gatilho booleano para executar a gravação. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **FilePath** (`P`) | `Text` | Caminho gravado no disco. |
| **Success** (`OK`) | `Boolean` | True se os dados foram exportados com sucesso. |
| **Info** (`Info`) | `Text` | Relatório completo de linhas, abas, tamanho de arquivo e memória. |
| **BestRank** (`Rank`) | `Text` | Lista de pré-visualização dos Top 20 indivíduos classificados no Canvas. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
