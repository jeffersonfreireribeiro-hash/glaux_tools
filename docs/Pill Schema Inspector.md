---
name: "Pill Schema Inspector"
nickname: "PillSchema"
category: "Glaux Tools"
subcategory: "Data"
class: "PillSchemaInspector_Component"
file: "PillSchemaInspector_Component.cs"
guid: "e81fefb2-a9fe-4c83-bfc2-2654e4cfed57"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill Schema Inspector (`PillSchema`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillSchemaInspector_Component.cs`  
**Classe:** `PillSchemaInspector_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Mostra o que há dentro de um store (.glauxdb): chaves por tipo, número de revisões, última gravação, árvores de cada entrada, tipos de dados, profundidade, campos de metadados e revisões que compartilham exatamente os mesmos dados (mesmo hash). Não há tabelas SQL: cada chave guarda árvores tipadas; o 'schema' é inferido do conteúdo.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Key Pattern** (`K`) | `Text` | Padrão de chave com * e ? (vazio = todas). |
| **Scan Types** (`ST`) | `Boolean` | Lê a última revisão de cada chave para listar tipos e profundidade (mais lento em stores grandes). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Keys** (`K`) | `Text` | Chaves (uma por tipo+chave). |
| **Kinds** (`T`) | `Text` | Tipo de cada chave. |
| **Revisions** (`R`) | `Integer` | Quantidade de revisões de cada chave. |
| **Latest** (`L`) | `Text` | Última revisão e data de cada chave. |
| **Trees** (`Tr`) | `Text` | Árvores da última revisão (ramo por chave): nome, ramos, itens. |
| **Types** (`Ty`) | `Text` | Tipos da última revisão (ramo por chave), com contagem e profundidade. |
| **Metadata Fields** (`MF`) | `Text` | Nomes de metadados usados no store e em quantas entradas. |
| **Shared Data** (`SD`) | `Text` | Revisões com dados idênticos (mesmo hash): relação implícita entre entradas. |
| **Info** (`I`) | `Text` | Arquivo, tamanho, versão e integridade. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
