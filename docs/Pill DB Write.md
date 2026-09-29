---
name: "Pill DB Write"
nickname: "PillDBWrite"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDbWrite_Component"
file: "PillDbWrite_Component.cs"
guid: "2d0dee42-91cb-41b6-88c9-4c850eeae40a"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill DB Write (`PillDBWrite`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDbWrite_Component.cs`  
**Classe:** `PillDbWrite_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Grava a DataTree como nova revisão da chave no store (.glauxdb), sem perda de estrutura ou tipos.
- Cada gravação cria a revisão N+1; revisões anteriores continuam consultáveis.
- 'Skip Unchanged' (padrão) não cria revisão quando dados e metadados são idênticos à última (identidade por SHA-256).
- Conecte um botão em 'Write' para gravar sob demanda, ou um toggle para gravar a cada mudança.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Key** (`K`) | `Text` | Chave do conjunto de dados (ex: 'ACU_T60', 'Resultados/Sala01'). |
| **Data** (`D`) | `Generic` | Árvore a gravar. |
| **Metadata** (`M`) | `Text` | Metadados 'chave=valor' (ex: 'autor=Ana', 'fonte=Pachyderm'). |
| **Kind** (`T`) | `Text` | Tipo da entrada (padrão 'dataset'). |
| **Write** (`W`) | `Boolean` | Grava quando True. |
| **Skip Unchanged** (`SU`) | `Boolean` | Não cria revisão se os dados e metadados forem idênticos à última. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Revision** (`R`) | `Integer` | Revisão gravada (ou a última existente). |
| **Hash** (`H`) | `Text` | SHA-256 de identidade dos dados. |
| **Written** (`OK`) | `Boolean` | True se uma nova revisão foi criada nesta solução. |
| **Info** (`I`) | `Text` | Resumo da gravação. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
