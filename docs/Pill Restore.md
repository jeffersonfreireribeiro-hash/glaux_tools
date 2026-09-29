---
name: "Pill Restore"
nickname: "PillRestore"
category: "Glaux Tools"
subcategory: "Vault"
class: "PillRestore_Component"
file: "PillRestore_Component.cs"
guid: "f64e9ecf-3e22-4d10-ad8b-23dc2fc9c842"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, vault]
---

# 🧩 Pill Restore (`PillRestore`)

**Categoria:** `Glaux Tools` ➔ `Vault`  
**Arquivo C#:** `PillRestore_Component.cs`  
**Classe:** `PillRestore_Component`  
**Pilha:** [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md)

---

## 📝 Descrição
Recupera um estado gravado pelo Pill Snapshot (ou uma execução do Experiment Logger):
- saídas: parâmetros (PillBundle, use Pill Bundle Unpack), árvores de entrada e saída, canais do PillHub registrados;
- 'Apply Controls' reaplica sliders, toggles, value lists, panels e Pill Slider Pools numa única solução, só nos controles compatíveis (existem, mesmo tipo, valor dentro da faixa atual, opção ainda existe); os demais aparecem em 'Compatibility'. Referências: 'Nome' (última), 'Nome@3', 'Nome@-1'.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Ref** (`R`) | `Text` | Estado a recuperar ('Nome', 'Nome@3', 'Nome@-1'). |
| **Kind** (`T`) | `Text` | Tipo da entrada: snapshot (padrão) ou experiment. |
| **Apply Controls** (`Go`) | `Boolean` | Reaplica os controles compatíveis no canvas (na borda False → True; use um botão). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Parameters** (`P`) | `Generic` | Parâmetros gravados como PillBundle. |
| **Inputs** (`In`) | `Generic` | Árvore de entradas gravada. |
| **Outputs** (`Out`) | `Generic` | Árvore de saídas/resultados gravada. |
| **Hub Keys** (`HK`) | `Text` | Canais do PillHub registrados. |
| **Hub Data** (`HD`) | `Generic` | Dados dos canais (ramo {i; caminho original} por canal). |
| **Controls** (`Ctl`) | `Text` | Controles registrados e seus valores. |
| **Compatibility** (`Cmp`) | `Text` | Para cada controle: OK ou o motivo de não poder ser restaurado. |
| **Compatible** (`N`) | `Integer` | Quantidade de controles restauráveis no documento atual. |
| **Info** (`I`) | `Text` | Referência, data, notas e ambiente do estado. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
