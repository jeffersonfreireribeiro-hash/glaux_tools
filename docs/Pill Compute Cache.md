---
name: "Pill Compute Cache"
nickname: "PillCache"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillCache_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Compute Cache (`PillCache`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillCache_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Bypass de recomputacao pesada. Armazena o resultado de operacoes caras (acustica, raytracing, malhas) em memoria associado ao hash dos parametros de entrada. Se as entradas nao mudarem, devolve o cache instantaneamente sem recomputar.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Key** (`K`) | `Text` | Identificador unico da tarefa/calculo em cache. O prefixo define o grupo automaticamente. |
| **InputParams** (`P`) | `Generic` | Parametros ou geometrias de entrada. Se qualquer valor mudar, o cache e invalidado automaticamente. |
| **ComputedData** (`D`) | `Generic` | Resultado pesado da computacao a ser memorizado. |
| **ForceRecalc** (`F`) | `Boolean` | Forca nova computacao e limpa o cache atual desta chave. |
| **Bypass** (`BY`) | `Boolean` | Se True, desativa o cache e sempre passa o dado direto. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dados resgatados do cache ou recem-computados (imutabilidade garantida). |
| **IsCached** (`C`) | `Boolean` | True se o resultado foi retornado da memoria cache sem gasto de processamento. |
| **ComputeTime** (`TS`) | `Text` | Horario em que o calculo original foi computado. |
| **Stats** (`S`) | `Text` | Estatisticas do cache: contagem de hits, hash e diagnostico. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
