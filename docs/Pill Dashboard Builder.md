---
name: "Pill Dashboard Builder"
nickname: "DashBuild"
category: "Glaux Tools"
subcategory: "Dashboard"
class: "PillDashboardBuilder_Component"
file: "PillDashboardBuilder_Component.cs"
guid: "d7eae36c-a08f-4120-bce0-bce93f4c80d9"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, dashboard]
---

# 🧩 Pill Dashboard Builder (`DashBuild`)

**Categoria:** `Glaux Tools` ➔ `Dashboard`  
**Arquivo C#:** `PillDashboardBuilder_Component.cs`  
**Classe:** `PillDashboardBuilder_Component`  
**Pilha:** [Pilha 6 — Dashboard & Controls](stacks/06_Dashboard.md)

---

## 📝 Descrição
Gera widgets para o Pill Dashboard a partir de listas paralelas (lista mais longa, como no Grasshopper):
- Kind: label, number, slider, toggle, button, dropdown, progress, chart.
- Settings: 'min=0 | max=10 | step=0.5 | key=[GEO] Raio' (mesmas chaves das linhas de texto).
- Values: ramo i → widget i. Indicadores mostram o valor; controles o usam como valor padrão.
- Hub Group: cria um indicador por canal do PillHub do grupo (ex: 'ACU'), lido ao vivo pelo painel. A lista de canais é lida quando o Builder calcula; recalcule-o se surgirem canais novos.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Kind** (`K`) | `Text` | Tipo de cada widget (label, number, slider, toggle, button, dropdown, progress, chart). |
| **Label** (`L`) | `Text` | Rótulo de cada widget. |
| **Settings** (`S`) | `Text` | Configurações de cada widget: 'chave=valor \| chave=valor'. |
| **Values** (`V`) | `Generic` | Valores: ramo i para o widget i (ou uma lista simples com um valor por widget). |
| **Hub Group** (`H`) | `Text` | Grupo ou chave do PillHub (ex: 'ACU', 'SALA_'): um indicador por canal. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Widgets** (`W`) | `Generic` | Widgets para a entrada W do Pill Dashboard. |
| **Definition** (`Def`) | `Text` | Definição textual equivalente (uma linha por widget), para salvar ou editar num Panel. |
| **Info** (`I`) | `Text` | Resumo e avisos. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, definição textual (schema), tipos de widget, interatividade, PillHub, presets, limitações, desempenho e testes:** ver [Pilha 6 — Dashboard & Controls](stacks/06_Dashboard.md).
* **Testes automatizados:** núcleo (configuração, estado, layout, commit, formatação) e adaptadores do Grasshopper cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); renderização em `tests/render/DashboardGallery.cs`; a interação no canvas precisa ser validada dentro do Grasshopper.
