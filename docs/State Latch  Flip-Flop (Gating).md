---
name: "State Latch / Flip-Flop (Gating)"
nickname: "StateLatch"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "StateLatch_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 State Latch / Flip-Flop (Gating) (`StateLatch`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `StateLatch_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Portão de retenção de dados que 'trava' o último estado válido. Quando o portão fecha ou erros/geometrias nulas chegam, a saída congela no valor anterior, impedindo que falhas propaguem.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Fluxo de dados de entrada. |
| **Gate / Pass** (`G`) | `Boolean` | Portão de dados (True = Aberto/Atualiza estado ao vivo, False = Fechado/Trava no último valor válido). |
| **Protect Nulls/Errors** (`Err`) | `Boolean` | Se True, trava automaticamente o último estado válido caso os dados de entrada cheguem vazios ou nulos. |
| **Reset** (`R`) | `Boolean` | Reseta o estado travado em memória. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Latched Data** (`D`) | `Generic` | Dados transmitidos ou travados. |
| **Is Latched** (`L`) | `Boolean` | True se a saída estiver congelada em um estado anterior. |
| **Last Update** (`T`) | `Text` | Horário da última atualização válida do estado. |
| **Status** (`S`) | `Text` | Diagnóstico do estado de retenção. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
