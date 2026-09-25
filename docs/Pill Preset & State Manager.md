---
name: "Pill Preset & State Manager"
nickname: "PillPresets"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillPresetManager_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Preset & State Manager (`PillPresets`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillPresetManager_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Gerencia cenários e alternativas de projeto (ex: 'sala vazia' vs 'com plateia'). Salva e restaura snapshots de parâmetros em memória ou arquivo JSON externo com histórico (logger). Suporta salvamento automático por detecção de modificações.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Bundle** (`B`) | `Generic` | Pacote PillBundle atual a ser registrado ou comparado. |
| **PresetName** (`N`) | `Text` | Nome do cenário/preset a salvar (ex: 'Sala_Vazia', 'Plateia_100pct'). |
| **Save** (`S`) | `Boolean` | Pulso para gravar o snapshot do bundle atual nos presets. |
| **LoadName** (`L`) | `Text` | Nome do preset a ser ativado e carregado. |
| **FilePath** (`FP`) | `Text` | Caminho opcional de arquivo JSON externo para salvar/carregar presets entre arquivos do Rhino. |
| **AutoSave** (`AS`) | `Boolean` | Se True, salva automaticamente um novo preset sempre que detectar alteração nos parâmetros do PillBundle. |
| **AutoPrefix** (`AP`) | `Text` | Prefixo para os nomes dos presets gerados automaticamente (ex: 'Cenario', 'Iteracao'). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **ActiveBundle** (`AB`) | `Generic` | Pacote PillBundle correspondente ao preset ativo carregado. |
| **AvailablePresets** (`P`) | `Text` | Lista com os nomes de todos os cenários disponíveis. |
| **LogHistory** (`LOG`) | `Text` | Histórico de mudanças e trocas de parâmetros com timestamp (CSV/Log). |
| **ActiveJSON** (`J`) | `Text` | String JSON do preset atualmente selecionado. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
