---
name: "Pill Compare"
nickname: "PillCompare"
category: "Glaux Tools"
subcategory: "Vault"
class: "PillCompare_Component"
file: "PillCompare_Component.cs"
guid: "162fb1d0-b437-4854-b48c-8d93caa87b48"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, vault]
---

# 🧩 Pill Compare (`PillCompare`)

**Categoria:** `Glaux Tools` ➔ `Vault`  
**Arquivo C#:** `PillCompare_Component.cs`  
**Classe:** `PillCompare_Component`  
**Pilha:** [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md)

---

## 📝 Descrição
Compara duas revisões gravadas (snapshots, experimentos ou datasets):
- quais árvores/parâmetros mudaram, surgiram ou sumiram (identidade por hash, sem carregar o que é igual);
- para parâmetros, 'valor antigo → novo'; para árvores, itens diferentes, ramos novos/removidos e maior variação numérica;
- mudanças de ambiente (versões do Glaux/Rhino/Grasshopper, documento) e se entradas/saídas são idênticas. Referências: 'Nome' (última), 'Nome@3' (revisão 3), 'Nome@-1' (penúltima). Sem B, compara A com a revisão anterior.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **A** (`A`) | `Text` | Referência do primeiro estado ('Nome', 'Nome@3', 'Nome@-1'). |
| **B** (`B`) | `Text` | Referência do segundo estado. Vazio = revisão anterior à de A. |
| **Kind** (`T`) | `Text` | Tipo das entradas (padrão 'snapshot'). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Same Data** (`=`) | `Boolean` | True se todas as árvores são idênticas. |
| **Same Inputs** (`=In`) | `Boolean` | True se as entradas (parâmetros, entradas, controles, canais) são idênticas. |
| **Same Outputs** (`=Out`) | `Boolean` | True se as saídas são idênticas. |
| **Changed** (`Ch`) | `Text` | Árvores alteradas. |
| **Added** (`Ad`) | `Text` | Árvores só em B. |
| **Removed** (`Rm`) | `Text` | Árvores só em A. |
| **Report** (`Rp`) | `Text` | Uma linha por árvore alterada, com o detalhe da diferença. |
| **Metadata Changes** (`MC`) | `Text` | Mudanças de ambiente/metadados. |
| **Pair** (`AB`) | `Text` | Referências efetivamente comparadas. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
