---
name: "Pill Change Detector & Trigger"
nickname: "PillChange"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillChangeDetector_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Change Detector & Trigger (`PillChange`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillChangeDetector_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Monitora dados, listas, arvores ou pacotes (PillBundle). Quando detecta qualquer modificacao nos valores, dispara um sinal booleano True (Data Gate / Pulse Trigger) para acionar salvamento de presets, simulacoes ou automacoes.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dados, listas, arvores ou PillBundle a monitorar continuamente. |
| **Threshold** (`T`) | `Number` | Tolerancia para variacoes numericas (padrao: 0.0001). Variacoes menores que esta tolerancia sao ignoradas. |
| **Mode** (`M`) | `Integer` | Modo de disparo:\n0 = Pulso (True apenas no ciclo de alteracao, False quando estavel)\n1 = Latch (True enquanto for diferente da referencia inicial)\n2 = Toggle (Inverte o booleano a cada mudanca detectada) |
| **Reset** (`R`) | `Boolean` | Pulso para resetar a referencia memorizada para o estado atual. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Changed** (`C`) | `Boolean` | Sinal booleano: True se os dados foram modificados nesta solucao (ou conforme o Modo selecionado); False se inalterados. |
| **Pass** (`D`) | `Generic` | Pass-through direto dos dados recebidos para encadeamento de fluxo. |
| **Hash** (`H`) | `Text` | Assinatura digital (SHA-256) do estado atual dos dados monitorados. |
| **Delta** (`Δ`) | `Text` | Diagnostico e resumo da alteracao detectada (delta de contagem, valores e timestamp). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
