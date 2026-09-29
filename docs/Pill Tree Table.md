---
name: "Pill Tree Table"
nickname: "PillTable"
category: "Glaux Tools"
subcategory: "I/O"
class: "PillTreeTable_Component"
file: "PillTreeTable_Component.cs"
guid: "8c2a194f-b728-482b-bf33-6e0d91dd5b98"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, io]
---

# 🧩 Pill Tree Table (`PillTable`)

**Categoria:** `Glaux Tools` ➔ `I/O`  
**Arquivo C#:** `PillTreeTable_Component.cs`  
**Classe:** `PillTreeTable_Component`  
**Pilha:** [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md)

---

## 📝 Descrição
Explode uma DataTree numa tabela longa (uma linha por item): Paths, Indices, Types e Values, sem assumir 'ramo = linha'.
- Branch Paths/Counts listam todos os ramos, inclusive os vazios, para reconstrução exata no Pill Table To Tree.
- Útil para filtrar, ordenar e visualizar árvores irregulares no Data Table Visualizer ou exportar para CSV.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados a explodir. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Paths** (`P`) | `Text` | Caminho de cada item (ex: '{0;2}'). |
| **Indices** (`i`) | `Integer` | Índice de cada item dentro do seu ramo. |
| **Types** (`T`) | `Text` | Tipo de cada item (Number, Integer, Text, Point... ou o tipo Goo completo). |
| **Values** (`V`) | `Generic` | Valor original de cada item (nulos preservados). |
| **Text Values** (`TV`) | `Text` | Valor de cada item como texto invariante (ponto decimal, datas ISO 8601). |
| **Branch Paths** (`BP`) | `Text` | Todos os ramos, inclusive vazios. |
| **Branch Counts** (`BC`) | `Integer` | Quantidade de itens de cada ramo. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
