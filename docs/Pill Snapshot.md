---
name: "Pill Snapshot"
nickname: "PillSnap"
category: "Glaux Tools"
subcategory: "Vault"
class: "PillSnapshot_Component"
file: "PillSnapshot_Component.cs"
guid: "e3ec4408-12ab-4a13-97c0-a530572893d3"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, vault]
---

# 🧩 Pill Snapshot (`PillSnap`)

**Categoria:** `Glaux Tools` ➔ `Vault`  
**Arquivo C#:** `PillSnapshot_Component.cs`  
**Classe:** `PillSnapshot_Component`  
**Pilha:** [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md)

---

## 📝 Descrição
Grava um estado reproduzível do projeto como nova revisão no store (.glauxdb):
- parâmetros nomeados (PillBundle), árvores de entrada e de saída;
- opcionalmente os controles do canvas (sliders, toggles, value lists, panels de entrada, Pill Slider Pool) e canais do PillHub;
- versões do Glaux, Rhino e Grasshopper, sistema, documento, data, notas, tags e tempo de execução;
- hashes de identidade das entradas e das saídas (mesmo hash de entradas = mesmos parâmetros). Evolui o Pill Preset Vault: o histórico fica fora do .gh, consultável, comparável e restaurável.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Name** (`N`) | `Text` | Nome do estado (chave do snapshot; cada captura cria uma revisão). |
| **Parameters** (`P`) | `Generic` | PillBundle(s) com os parâmetros nomeados (Pill Bundle Pack). |
| **Inputs** (`In`) | `Generic` | Árvore de entradas relevantes. |
| **Outputs** (`Out`) | `Generic` | Árvore de saídas/resultados relevantes. |
| **Controls** (`Ctl`) | `Boolean` | Registra sliders, toggles, value lists, panels de entrada e Pill Slider Pools do documento. |
| **Hub** (`Hub`) | `Text` | Canais do PillHub a registrar: chave, grupo (ex: 'GEO') ou 'ALL'. |
| **Notes** (`Nt`) | `Text` | Anotações livres. |
| **Tags** (`Tg`) | `Text` | Etiquetas (ex: 'aprovado', 'cliente'). |
| **Runtime** (`ms`) | `Number` | Tempo de execução em ms (ex: do Pill Runtime Profiler ou Data Timer). |
| **Capture** (`C`) | `Boolean` | Grava o snapshot quando True (conecte um botão). |
| **Skip Unchanged** (`SU`) | `Boolean` | Não cria revisão se tudo for idêntico à última. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Ref** (`R`) | `Text` | Referência do snapshot gravado ('Nome@revisão'). |
| **Revision** (`Rev`) | `Integer` | Revisão gravada (ou a última existente). |
| **Inputs Hash** (`IH`) | `Text` | Identidade combinada das entradas (parâmetros, entradas, controles, canais). |
| **Outputs Hash** (`OH`) | `Text` | Identidade das saídas. |
| **Info** (`I`) | `Text` | Resumo do que foi registrado. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
