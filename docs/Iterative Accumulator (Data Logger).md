---
name: "Iterative Accumulator (Data Logger)"
nickname: "Accumulator"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "IterativeAccumulator_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Iterative Accumulator (Data Logger) (`Accumulator`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `IterativeAccumulator_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Buffer circular de histórico para processos iterativos e loops. Grava os últimos K estados de uma árvore sem perda de caminhos e com suporte a Pause, Reset e Exportação JSON/CSV.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados a ser gravada no histórico a cada iteração. |
| **Record / Step** (`Rec`) | `Boolean` | Pulso ou sinal booleano para gravar o estado atual. |
| **Buffer Size K** (`K`) | `Integer` | Capacidade máxima do buffer circular (ex.: 50, 100, 500). Use 0 para ilimitado. |
| **Pause** (`P`) | `Boolean` | Se True, pausa a gravação mantendo os dados no buffer. |
| **Reset** (`R`) | `Boolean` | Limpa todo o histórico gravado. |
| **Export Path** (`Path`) | `Text` | Caminho opcional de arquivo para exportar o histórico (.json ou .csv). |
| **Export Now** (`Exp`) | `Boolean` | Gatilho para disparar a exportação do arquivo. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **History Tree** (`H`) | `Generic` | Árvore cronológica completa onde cada ramo raiz {iter; ...} representa um estado gravado. |
| **Count** (`N`) | `Integer` | Total de iterações armazenadas atualmente no buffer. |
| **Status / Log** (`Log`) | `Text` | Relatório de ocupação do buffer e status de exportação. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
