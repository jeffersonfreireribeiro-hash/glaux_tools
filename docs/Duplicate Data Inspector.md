---
name: "Duplicate Data Inspector"
nickname: "DupInspector"
category: "Buraqueira Tools"
subcategory: "Tree"
class: "DuplicateDataInspector_Component"
file: "DuplicateDataInspector_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree, table, deduplication]
---

# 🧩 Duplicate Data Inspector (`DupInspector`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `DuplicateDataInspector_Component.cs`  
**Classe:** `DuplicateDataInspector_Component`  
**GUID:** `c8220014-e1ef-4000-8000-000000000015`

---

## 📝 Descrição

Inspeciona, detecta e extrai dados e linhas repetidas em DataTrees e Tabelas. Suporta múltiplos modos de inspeção e deduplicação, com destaque para o **MODO TABELA (Table Mode)**:
* **MODO TABELA (`Table Mode`):** Interpreta a DataTree como uma tabela de dados relacional onde **cada ramo (branch) é uma Coluna** e **cada índice de item é uma Linha da tabela**. Compara as tuplas completas de cada linha para detectar registros idênticos duplicados.
* **MODO POR RAMO (`Per Branch`):** Detecta repetições locais de itens dentro de cada ramo individual.
* **MODO GLOBAL (`Global`):** Compara todos os itens de toda a árvore globalmente, agrupando valores idênticos.
* **MODO RAMO CONTRA RAMO (`Branch vs Branch`):** Compara os ramos inteiros entre si como vetores/conjuntos para identificar ramos 100% idênticos.

Retorna os valores e linhas repetidas, total de grupos repetidos, frequências de repetição, índices originais, dados limpos desduplicados (mantendo a estrutura original de ramos), máscara booleana 1:1 para `Cull Pattern` e relatório estruturado de diagnóstico.

---

## 📥 Entradas (Inputs)

| Parâmetro | Nick | Tipo | Descrição |
| :--- | :---: | :---: | :--- |
| **Data** | `D` | `Generic` (Tree) | Árvore ou lista de dados a ser inspecionada para encontrar repetições. |
| **Table Mode** | `Table` | `Boolean` | Ativação direta do **MODO TABELA** (Ramos = Colunas, Índices = Linhas da tabela). Se `True`, compara registros/linhas inteiras. |
| **Mode** | `M` | `Generic` | Modo de comparação alternativo:<br>`0` = Por Ramo (`Per Branch`)<br>`1` = Global (`Toda a Árvore`)<br>`2` = Ramo contra Ramo (`Branch vs Branch`)<br>`3` = Modo Tabela (`Table Mode`: Ramos = Colunas, Índices = Linhas)<br>Aceita números (0 a 3) ou texto (`'PerBranch'`, `'Global'`, `'BranchVsBranch'`, `'Table'`, `'Tabela'`, `'Ultra'`). |
| **Tolerance** | `Tol` | `Number` | Tolerância para igualdade numérica de números de ponto flutuante e coordenadas 3D de pontos/vetores (padrão: `1e-6`). |
| **Case Sensitive** | `Case` | `Boolean` | Diferenciar maiúsculas e minúsculas na comparação de textos (padrão: `True`). |
| **Skip Nulls** | `SkipNull` | `Boolean` | Ignorar valores nulos ou vazios para não agrupá-los como repetições falsas (padrão: `True`). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Nick | Tipo | Descrição |
| :--- | :---: | :---: | :--- |
| **Repeated** | `Rep` | `Generic` (Tree) | Valores ou linhas repetidas. No **Modo Tabela**: cada ramo `{grupo}` contém os valores da linha repetida; nos demais modos: valores repetidos agrupados por ramo ou grupo. |
| **Group Count** | `NumGroups` | `Integer` | Quantidade total de grupos repetidos distintos encontrados (linhas ou valores com 2 ou mais ocorrências). |
| **Repetition Counts** | `Counts` | `Integer` (Tree) | Quantidade de vezes que cada grupo repetido apareceu na árvore ou tabela (frequência). |
| **Repeated Indices** | `iRep` | `Integer` (Tree) | Índices onde ocorrem as repetições (no Modo Tabela: índices de linha da tabela; nos outros modos: índices dos itens ou ramos). |
| **Clean Data** | `Clean` | `Generic` (Tree) | Dados desduplicados (preserva a estrutura de ramos original da árvore, mantendo apenas 1 ocorrência de cada linha ou valor). |
| **Unique Pattern** | `Pattern` | `Boolean` (Tree) | Máscara booleana 1:1 (`True` = mantido/único, `False` = repetição redundante) pronta para uso no componente `Cull Pattern`. |
| **Report** | `R` | `Text` | Relatório estruturado de diagnóstico com estatísticas de linhas/itens, grupos repetidos, porcentagens de redução e frequências. |

---

## ⚙️ Menu de Contexto (Right-Click)

O componente permite selecionar os modos de operação diretamente no menu de clique com o botão direito sobre o componente no Canvas do Grasshopper:
* **★ MODO TABELA (Ramos=Colunas, Linhas=Linhas)**: Ativa o modo tabular persistente.
* **Modo Por Ramo (Per Branch)**: Ativa o modo local em cada ramo.
* **Modo Global (Toda a Árvore)**: Ativa o modo global.
* **Modo Ramo contra Ramo (Branch vs Branch)**: Ativa a comparação de vetores de ramo.
* **Diferenciar Maiúsculas/Minúsculas (Case Sensitive)**: Toggle booleano.
* **Ignorar Itens Nulos/Vazios**: Toggle booleano.

---

## 💡 Casos de Uso Práticos

1. **Deduplicação de Tabelas Paramétricas:** Quando dados vêm de múltiplos componentes (ex.: coordenadas X, Y, Z em ramos separados ou dados de sensores), ative o `Table Mode` para identificar registros completos duplicados e filtrar a tabela com o `Clean Data` ou `Unique Pattern` + `Cull Pattern`.
2. **Remoção de Pontos ou Vetores Coincidentes em Ramos:** No modo `Per Branch` ou `Global`, com tolerância configurada (ex.: `0.001` m), remove pontos redundantes sem perder a hierarquia da árvore.
3. **Auditoria de Geometrias Repetidas:** Identificação rápida de ramos de malhas ou polilinhas com as mesmas propriedades.
