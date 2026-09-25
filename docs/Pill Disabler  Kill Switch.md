---
name: "Pill Disabler / Kill Switch"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillDisabler_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Pill Disabler / Kill Switch (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PillDisabler_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Mute / Disable** (`Mute`) | `Boolean` | Se True (padrao), desativa a pilha de verdade (Locked = true). Se False, mantem ativada.\n |
| **Target Stack (A)** (`StackA`) | `Generic` | Componente(s) ou Pilulas da pilha a ser controlada. Puxe conexoes dos componentes ou arraste o conector. |
| **Stack B (Optional)** (`StackB`) | `Generic` | Segunda pilha para alternancia mutua. Se conectada, quando A estiver ativa, B sera desativada, e vice-versa. |
| **Downstream** (`Down`) | `Boolean` | Se True (padrao), tambem desativa todos os componentes conectados a jusante das pilhas controladas. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Status** (`Status`) | `Text` | Status atual da execucao das pilhas. |
| **Disabled Count** (`Count`) | `Integer` | Quantidade de componentes atualmente desativados de verdade no canvas. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
