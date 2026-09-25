---
name: "Multi Logic Gate"
nickname: "LogicGate"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "MultiLogicGate_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Multi Logic Gate (`LogicGate`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `MultiLogicGate_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Porta lógica consolidada de múltiplas entradas (AND, OR, XOR, NAND, NOR, XNOR). Avalia N condições simultaneamente sem poluir o canvas com múltiplos operadores encadeados. Retorna o booleano consolidado, contagens e os índices das entradas que falharam.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Inputs** (`L`) | `Boolean` | Lista ou árvore de valores booleanos a avaliar. Também aceita múltiplos fios conectados. |
| **Operation** (`Op`) | `Generic` | Operação lógica: AND (0), OR (1), XOR (2), NAND (3), NOR (4), XNOR (5). Padrão = AND. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Result** (`R`) | `Boolean` | Booleano resultante consolidado da operação lógica. |
| **True Count** (`N_true`) | `Integer` | Quantidade de condições satisfeitas (True). |
| **False Count** (`N_false`) | `Integer` | Quantidade de condições não satisfeitas (False). |
| **False Indices** (`iFalse`) | `Integer` | Índices (IDs locais) das entradas que falharam (para diagnóstico visual imediato). |
| **True Indices** (`iTrue`) | `Integer` | Índices (IDs locais) das entradas que foram satisfeitas. |
| **Diagnostic Summary** (`Rep`) | `Text` | Resumo diagnóstico com contagem e identificação de falhas. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
