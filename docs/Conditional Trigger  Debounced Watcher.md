---
name: "Conditional Trigger / Debounced Watcher"
nickname: "Trigger"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "ConditionalTrigger_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Conditional Trigger / Debounced Watcher (`Trigger`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `ConditionalTrigger_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Dispara a recomputação a jusante apenas em borda de subida (False -> True), mudança de estado ou após um intervalo de estabilidade (Debounce) para evitar travamentos por sliders oscilantes.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados a ser liberada sob condição ou após estabilidade. |
| **Trigger / Condition** (`T`) | `Boolean` | Condição booleana de disparo. |
| **Mode** (`M`) | `Integer` | Modo de disparo:\n0 = Borda de Subida (Rising Edge: False -> True)\n1 = Debounce / Estabilidade (espera ms sem oscilações antes de disparar)\n2 = Mudança de Valor (qualquer transição True/False)\n3 = Pass-Through contínuo enquanto True |
| **Debounce Ms** (`ms`) | `Integer` | Tempo de estabilidade em milissegundos para o modo Debounce (padrão: 250 ms). |
| **Force Fire** (`F`) | `Boolean` | Força um disparo imediato avulso. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Output** (`O`) | `Generic` | Dados liberados após a validação do gatilho ou debounce. |
| **Has Fired** (`Fired`) | `Boolean` | Pulso booleano indicando que o evento de disparo ocorreu nesta iteração. |
| **Event Count** (`N`) | `Integer` | Total de disparos acumulados. |
| **Status** (`S`) | `Text` | Estado atual do gatilho. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
