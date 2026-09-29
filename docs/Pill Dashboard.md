---
name: "Pill Dashboard"
nickname: "PillDash"
category: "Glaux Tools"
subcategory: "Dashboard"
class: "PillDashboard_Component"
file: "PillDashboard_Component.cs"
guid: "49ee50b1-5842-466b-89ee-cd9ff6df435d"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, dashboard]
---

# 🧩 Pill Dashboard (`PillDash`)

**Categoria:** `Glaux Tools` ➔ `Dashboard`  
**Arquivo C#:** `PillDashboard_Component.cs`  
**Classe:** `PillDashboard_Component`  
**Pilha:** [Pilha 6 — Dashboard & Controls](stacks/06_Dashboard.md)

---

## 📝 Descrição
Painel interativo no canvas: reúne controles e indicadores num só componente, sem espalhar sliders, toggles e panels.
- W: uma linha por widget, ex: 'slider Largura | min=0 | max=10 | step=0.5 | key=[GEO] Largura', ou widgets do Pill Dashboard Builder.   Tipos: label, number, slider, toggle, button, dropdown, progress, chart. Painel: 'title=', 'layout=stack|row|grid', 'columns=', 'width='.
- Controles saem em V (um ramo por controle) e, com 'key=', no PillHub (leia com Pill Receiver em qualquer lugar).
- Indicadores com 'key=' mostram canais do PillHub sem fios e sem recalcular nada; ou use D (PillBundle / 'nome=valor').
- Arrasto: commit=auto (padrão) entrega durante o arrasto enquanto a solução é leve e só ao soltar quando é pesada; commit=live|release força o modo.
- S/LS: estado dos controles em linhas 'id=valor' para presets, Pill DB Write, Pill Snapshot e Pill Restore.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Widgets** (`W`) | `Generic` | Definições: linhas de texto ('slider Largura \| min=0 \| max=10') e/ou widgets do Pill Dashboard Builder. |
| **Data** (`D`) | `Generic` | Dados dos indicadores: PillBundle(s) ou textos 'nome=valor' (casados por 'source=', id ou rótulo). Resultados que dependem dos controles deste painel devem vir por 'key=' (PillHub), não por fio: fio criaria um ciclo. |
| **Load State** (`LS`) | `Text` | Estado a aplicar ('id=valor' por linha), ex: saída S de outro painel, Pill Restore ou Pill DB Read. Aplicado só quando muda. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Generic` | Valores dos controles: um ramo por controle, na ordem da definição (slider = número, toggle/botão = booleano, dropdown = texto ou índice com output=index). |
| **Names** (`N`) | `Text` | Rótulo de cada controle (mesma ordem dos ramos de V). |
| **State** (`S`) | `Text` | Estado dos controles persistentes em linhas 'id=valor' (para presets e snapshots). |
| **Changed** (`C`) | `Text` | Id do controle que o usuário mudou e disparou esta solução (vazio se a solução veio de outro lugar). |
| **Info** (`I`) | `Text` | Resumo: widgets, ligações com o PillHub, política de commit, última solução e avisos. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, definição textual (schema), tipos de widget, interatividade, PillHub, presets, limitações, desempenho e testes:** ver [Pilha 6 — Dashboard & Controls](stacks/06_Dashboard.md).
* **Testes automatizados:** núcleo (configuração, estado, layout, commit, formatação) e adaptadores do Grasshopper cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); renderização em `tests/render/DashboardGallery.cs`; a interação no canvas precisa ser validada dentro do Grasshopper.
