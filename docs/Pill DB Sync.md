---
name: "Pill DB Sync"
nickname: "PillSync"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDbSync_Component"
file: "PillDbSync_Component.cs"
guid: "6585b5d8-f0a8-47ef-8c42-d7c5b665626c"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill DB Sync (`PillSync`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDbSync_Component.cs`  
**Classe:** `PillDbSync_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Sincroniza uma DataTree com uma chave do store (.glauxdb) de forma controlada:
- Direction: 0 = Push (Grasshopper → store), 1 = Pull (store → Grasshopper), 2 = Two-Way.
- Estado: Clean, LocalDirty (o dado do GH mudou), StoreAhead (o store tem revisão nova), Conflict (os dois mudaram).
- Conflito (só em Two-Way): 0 = parar e avisar, 1 = prevalece o Grasshopper, 2 = prevalece o store.
- Auto = sincroniza sozinho; senão espera o gatilho 'Sync'. Sem laços: depois de um Pull o dado local antigo não é reenviado, e dados iguais aos do store não geram revisão.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Key** (`K`) | `Text` | Chave sincronizada. |
| **Local Data** (`D`) | `Generic` | Dado do Grasshopper (vazio = só receber do store). |
| **Direction** (`Dir`) | `Integer` | 0 = Push, 1 = Pull, 2 = Two-Way. |
| **Auto** (`A`) | `Boolean` | Sincroniza automaticamente a cada mudança (sem gatilho). |
| **Conflict** (`C`) | `Integer` | Conflito em Two-Way: 0 = parar, 1 = Grasshopper prevalece, 2 = store prevalece. |
| **Sync** (`Go`) | `Boolean` | Executa a ação pendente (modo manual). |
| **Kind** (`T`) | `Text` | Tipo da entrada (padrão 'dataset'). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dado efetivo: o local, ou o do store quando ele é a referência (após Pull). |
| **State** (`St`) | `Text` | Clean, LocalDirty, StoreAhead, Conflict ou Empty. |
| **Action** (`Ac`) | `Text` | Ação executada nesta solução (Push, Pull, None) ou pendente. |
| **Store Revision** (`R`) | `Integer` | Revisão do store após a sincronização. |
| **Local Hash** (`H`) | `Text` | SHA-256 do dado local. |
| **Log** (`L`) | `Text` | Motivo da decisão. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
