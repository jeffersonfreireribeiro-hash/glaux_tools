---
name: "Pill Table To Tree"
nickname: "PillToTree"
category: "Glaux Tools"
subcategory: "I/O"
class: "PillTableToTree_Component"
file: "PillTableToTree_Component.cs"
guid: "9865fde4-306b-43a5-805c-924e3170a4a0"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, io]
---

# 🧩 Pill Table To Tree (`PillToTree`)

**Categoria:** `Glaux Tools` ➔ `I/O`  
**Arquivo C#:** `PillTableToTree_Component.cs`  
**Classe:** `PillTableToTree_Component`  
**Pilha:** [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md)

---

## 📝 Descrição
Monta uma DataTree a partir de uma tabela longa: Paths (um por valor ou um único para todos), Indices opcionais (posicionam os itens; lacunas viram nulos), Values e Types opcionais.
- Com Types, valores em texto são convertidos ao tipo informado (Number, Integer, Boolean, Point 'x;y;z', Interval 't0;t1', Colour '#AARRGGBB', Time ISO 8601, Guid).
- Branch Paths recria ramos vazios. Inverso exato do Pill Tree Table.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Paths** (`P`) | `Text` | Caminho de cada valor ('{0;1}' ou '0;1'). Um único caminho vale para todos. |
| **Indices** (`i`) | `Integer` | Índice de cada valor no ramo (opcional). Negativo = linha que só declara o ramo. |
| **Values** (`V`) | `Generic` | Valores (nulos permitidos). |
| **Types** (`T`) | `Text` | Tipo de cada valor (opcional). Converte valores em texto para o tipo informado. |
| **Branch Paths** (`BP`) | `Text` | Ramos que devem existir mesmo sem itens (opcional). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore reconstruída. |
| **Info** (`I`) | `Text` | Resumo e avisos da reconstrução. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
