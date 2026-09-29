---
name: "Pill DB Query"
nickname: "PillQuery"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDbQuery_Component"
file: "PillDbQuery_Component.cs"
guid: "b7cfcebc-275a-4470-bcfd-50050bac121e"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill DB Query (`PillQuery`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDbQuery_Component.cs`  
**Classe:** `PillDbQuery_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Consulta o store (.glauxdb) com filtros tipados, sem montar consultas a partir de texto:
- Entradas: tipo, padrão de chave (* e ?), revisões (última ou todas), metadados 'chave=valor'.
- Itens: máscara de caminho ('{0;*}', '{*;2}', '{1;**}', '{0..3;*}'), tipo, faixa numérica e texto. Responde perguntas como 'quais revisões têm T60 acima de 2 s?' ou 'em que ramos aparece este valor?'.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Kind** (`T`) | `Text` | Tipo das entradas (dataset, snapshot, experiment, metrics). Vazio = todos. |
| **Key Pattern** (`K`) | `Text` | Padrão da chave com * e ? (ex: 'ACU_*'). Vazio = todas. |
| **All Revisions** (`All`) | `Boolean` | False = só a última revisão de cada chave; True = todas. |
| **Metadata** (`M`) | `Text` | Filtros de metadados 'chave=valor' (todos precisam bater). |
| **Path Mask** (`P`) | `Text` | Máscara de caminho dos itens (ex: '{0;*}'). Vazio = qualquer. |
| **Type** (`Ty`) | `Text` | Tipo dos itens (Number, Integer, Text, Point, Curve...). Vazio = qualquer. |
| **Range** (`Rg`) | `Interval` | Faixa numérica dos itens (Number, Integer, Boolean). |
| **Contains** (`C`) | `Text` | Texto contido no valor (sem diferenciar maiúsculas). |
| **Tree** (`Tr`) | `Text` | Árvore consultada dentro de cada entrada (padrão 'data'). |
| **Limit** (`L`) | `Integer` | Máximo de entradas retornadas. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Keys** (`K`) | `Text` | Chave de cada entrada encontrada. |
| **Revisions** (`R`) | `Integer` | Revisão de cada entrada encontrada. |
| **Timestamps** (`TS`) | `Text` | Data/hora local de cada entrada. |
| **Values** (`V`) | `Generic` | Itens que passaram nos filtros (um ramo por entrada; vazio se não houver filtro de item). |
| **Paths** (`P`) | `Text` | Caminho original de cada item encontrado. |
| **Indices** (`i`) | `Integer` | Índice original de cada item encontrado. |
| **Count** (`N`) | `Integer` | Quantidade de itens encontrados. |
| **Info** (`I`) | `Text` | Entradas e itens examinados. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
