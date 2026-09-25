---
name: "Pill Geometry Filter"
nickname: "PillGeomFilter"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillGeometryFilter_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Geometry Filter (`PillGeomFilter`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillGeometryFilter_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Filtro e higienizador de geometrias para simulação acústica e modelagem paramétrica. Elimina com precisão cirúrgica elementos degenerados, micro-faces colapsadas com área próxima a zero, superfícies invertidas, valores negativos, NaN ou tendendo a -infinity. Suporta descarte pontual ou limpeza de subfaces em Breps e Malhas com publicação opcional no PillHub.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Geometry** (`G`) | `Geometry` | Geometrias brutas ou árvores de geometrias a inspecionar e filtrar (Brep, Mesh, Surface, Extrusion, Curve). |
| **MinArea** (`Min`) | `Number` | Área mínima aceitável (m²). Geometrias com área menor ou igual a este limiar, colapsadas, negativas, NaN ou tendendo a -infinito serão sumariamente eliminadas. Default: 0.0001 m². |
| **MaxArea** (`Max`) | `Number` | Área máxima aceitável opcional (m²). Se definido como valor positivo, geometrias infinitas ou gigantescas que ultrapassarem este limite serão eliminadas. 0 = desativado. |
| **CleanSubFaces** (`Sub`) | `Boolean` | Se True, inspeciona e remove internamente micro-triângulos e faces degeneradas de área zero em Breps e Malhas compostas. Se False, avalia o elemento como um bloco único. |
| **PreservePaths** (`P`) | `Integer` | Modo de preservação de caminhos da árvore de dados: 0 = Podar ramos vazios, 1 = Manter ramos vazios, 2 = Preencher com <null>. |
| **Key** (`K`) | `Text` | Nome opcional do canal Pill para transmissão sem fios automática das geometrias limpas pelo PillHub (categoria [GEO]). |
| **Active** (`Active`) | `Boolean` | Gatilho de ativação do filtro. Se False, opera em modo pass-through (todas as geometrias passam sem filtragem). O ÚLTIMO PARÂMETRO DA LISTA. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Clean** (`C`) | `Geometry` | Árvore contendo apenas as geometrias higienizadas e aprovadas (com área válida > MinArea e livre de degenerações). |
| **Discarded** (`D`) | `Geometry` | Árvore contendo as geometrias eliminadas (área próxima a zero, degeneradas, NaN, negativas, infinitas ou inválidas). |
| **Areas** (`A`) | `Number` | Árvore com os valores calculados de área (m²) de cada elemento inspecionado. |
| **Pattern** (`P`) | `Boolean` | Máscara booleana (True = Aprovada/Limpa, False = Eliminada/Degenerada). |
| **Report** (`R`) | `Text` | Relatório estruturado com estatísticas de conformidade, percentual de descarte, menor área detectada e diagnóstico de micro-faces. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
