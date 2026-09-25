---
name: "Pill Constraint Checker"
nickname: "PillCheck"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillConstraintChecker_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Constraint Checker (`PillCheck`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillConstraintChecker_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Validador e guardião de restrições de projeto. Compara parâmetros acústicos e geométricos contra faixas-alvo (ex: RT60 dentro da ISO 3382). Emite status visual no canvas e relatórios de conformidade.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **ParamName** (`N`) | `Text` | Nome do parâmetro sob verificação (ex: 'RT60 1000Hz [s]'). |
| **Values** (`V`) | `Number` | Valores medidos ou calculados a verificar. |
| **MinTarget** (`Min`) | `Number` | Limite mínimo aceitável. |
| **MaxTarget** (`Max`) | `Number` | Limite máximo aceitável. |
| **Tolerance** (`Tol`) | `Number` | Margem de tolerância opcional (expandindo a faixa aceitável em ±Tol). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Pass** (`OK`) | `Boolean` | True se 100% dos valores cumprem os limites de conformidade; False se houver violação. |
| **ComplianceRate** (`CR`) | `Number` | Taxa percentual de conformidade [0% a 100%]. |
| **Violations** (`VIO`) | `Text` | Lista detalhada dos valores em desacordo com as metas. |
| **ConformingValues** (`CV`) | `Number` | Subconjunto com os valores aprovados. |
| **Report** (`RPT`) | `Text` | Relatório estruturado de conformidade com média e diagnóstico. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
