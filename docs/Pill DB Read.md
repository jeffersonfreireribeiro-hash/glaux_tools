---
name: "Pill DB Read"
nickname: "PillDBRead"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDbRead_Component"
file: "PillDbRead_Component.cs"
guid: "09ddcfcb-9325-403d-8a80-02db78a40596"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill DB Read (`PillDBRead`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDbRead_Component.cs`  
**Classe:** `PillDbRead_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Lê uma revisão de uma chave do store (.glauxdb) e reconstrói a DataTree original (caminhos, ramos vazios, nulos e tipos).
- Revision 0 = última; N = revisão N; -1 = penúltima, -2 = antepenúltima...
- Atualiza sozinho quando outro componente grava no mesmo store.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Key** (`K`) | `Text` | Chave a ler. |
| **Revision** (`R`) | `Integer` | 0 = última; N > 0 = revisão N; N < 0 = N revisões antes da última. |
| **Kind** (`T`) | `Text` | Tipo da entrada (padrão 'dataset'). |
| **Tree** (`Tr`) | `Text` | Nome da árvore dentro da entrada (padrão 'data'; snapshots têm várias). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore reconstruída. |
| **Revision** (`R`) | `Integer` | Revisão lida. |
| **Timestamp** (`TS`) | `Text` | Data/hora local da gravação. |
| **Metadata** (`M`) | `Text` | Metadados 'chave=valor'. |
| **Hash** (`H`) | `Text` | SHA-256 de identidade dos dados. |
| **Revisions** (`Rs`) | `Integer` | Todas as revisões disponíveis da chave. |
| **Trees** (`Tn`) | `Text` | Nomes das árvores da entrada. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
