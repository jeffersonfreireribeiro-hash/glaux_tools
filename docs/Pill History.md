---
name: "Pill History"
nickname: "PillHistory"
category: "Glaux Tools"
subcategory: "Vault"
class: "PillHistory_Component"
file: "PillHistory_Component.cs"
guid: "13e0a590-d4ff-46a1-845a-f0c5eb4d205c"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, vault]
---

# 🧩 Pill History (`PillHistory`)

**Categoria:** `Glaux Tools` ➔ `Vault`  
**Arquivo C#:** `PillHistory_Component.cs`  
**Classe:** `PillHistory_Component`  
**Pilha:** [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md)

---

## 📝 Descrição
Lista o histórico de estados gravados no store: snapshots, experimentos, métricas ou datasets.
- Uma linha por revisão: referência, data, notas, hashes de entradas e saídas.
- 'Tree' extrai uma árvore de cada revisão (ex: 'out', 'result', 'param:Largura') numa só árvore {revisão; caminho original}, pronta para gráficos (Chart Line) e tabelas.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Kind** (`T`) | `Text` | Tipo das entradas: snapshot (padrão), experiment, metrics, dataset. |
| **Key Pattern** (`K`) | `Text` | Padrão de chave com * e ?. |
| **Tree** (`Tr`) | `Text` | Árvore a extrair de cada revisão (opcional). |
| **Limit** (`L`) | `Integer` | Máximo de revisões listadas. |
| **Newest First** (`NF`) | `Boolean` | Mais recentes primeiro. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Refs** (`R`) | `Text` | Referências 'chave@revisão' (entrada do Compare/Restore). |
| **Keys** (`K`) | `Text` | Chave de cada revisão. |
| **Revisions** (`Rev`) | `Integer` | Número de cada revisão. |
| **Timestamps** (`TS`) | `Text` | Data/hora local. |
| **Notes** (`Nt`) | `Text` | Notas e tags. |
| **Inputs Hash** (`IH`) | `Text` | Hash (curto) das entradas de cada revisão. |
| **Outputs Hash** (`OH`) | `Text` | Hash (curto) das saídas de cada revisão. |
| **Metadata** (`M`) | `Text` | Metadados 'chave=valor' (um ramo por revisão). |
| **Tree Data** (`D`) | `Generic` | Árvore extraída de cada revisão, com caminho {i; caminho original}. |
| **Info** (`I`) | `Text` | Resumo. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
