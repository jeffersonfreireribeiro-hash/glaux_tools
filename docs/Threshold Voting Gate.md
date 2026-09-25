---
name: "Threshold Voting Gate"
nickname: "VoteGate"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "ThresholdVotingGate_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Threshold Voting Gate (`VoteGate`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `ThresholdVotingGate_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Avalia se um percentual ou quórum numérico mínimo de condições booleanas foi satisfeito, com suporte a pesos ponderados por critério. Ideal para metas de conformidade parcial ou aprovação multicritério.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Conditions** (`C`) | `Boolean` | Lista ou árvore de verificações booleanas (ex: saídas do Pill Constraint Checker). |
| **Threshold** (`Thresh`) | `Number` | Valor numérico ou percentual mínimo de aprovação (ex: 0.80 para 80% ou 3 para pelo menos 3 critérios válidos). Padrão = 0.5 (50%). |
| **Weights** (`W`) | `Number` | Pesos opcionais por critério para votação ponderada. Se omitido, todos os critérios têm peso 1.0. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Pass** (`OK`) | `Boolean` | True se o quórum mínimo de conformidade for atingido, False caso contrário. |
| **Score** (`%`) | `Number` | Percentual exato de conformidade obtido (0.0 a 100.0%). |
| **Weighted Score** (`Score`) | `Number` | Pontuação absoluta obtida (soma dos pesos dos critérios aprovados). |
| **Passed Count** (`N_pass`) | `Integer` | Quantidade de critérios aprovados. |
| **Failed Count** (`N_fail`) | `Integer` | Quantidade de critérios reprovados. |
| **Failed Indices** (`iFail`) | `Integer` | Índices (IDs locais) dos critérios que falharam (para diagnóstico rápido). |
| **Summary** (`Rep`) | `Text` | Diagnóstico textual completo dos critérios aprovados vs. reprovados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
