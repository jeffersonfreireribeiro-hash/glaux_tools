---
name: "Geometric Frequency (1D Clustering)"
nickname: "GeoCluster"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "GeometricClusterFrequency_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Geometric Frequency (1D Clustering) (`GeoCluster`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `GeometricClusterFrequency_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Agrupa valores numéricos/geométricos com tolerância de proximidade configurável (Clustering 1D / Create Set com Tolerância). Retorna valores canônicos médios, quantitativo de repetições, porcentagens e o mapa de índices para rotulagem.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Number` | Conjunto de dados numéricos / dimensões de peças a agrupar (comprimentos, áreas, ângulos, coordenadas). |
| **Tolerance** (`Tol`) | `Number` | Tolerância geométrica / distância máxima para pertencer ao mesmo grupo (ex.: 0.005 m = 5 mm). Padrão: 0.005. |
| **Canonical Mode** (`M`) | `Integer` | Modo do valor canônico representativo:\n0 = Média do Grupo (Cluster Mean)\n1 = Mediana do Grupo\n2 = Primeiro Valor Encontrado\n3 = Arredondado para Múltiplo de Tol |
| **Per Branch** (`B`) | `Boolean` | Se True, processa individualmente por ramo. Se False, agrupa globalmente sobre toda a árvore. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Canonical Values** (`C`) | `Number` | Valores médios/canônicos de cada tipo de peça/grupo identificado. |
| **Counts** (`N`) | `Integer` | Quantitativo / Número de repetições de cada tipo de peça. |
| **Percentages %** (`%`) | `Number` | Frequência relativa percentual de cada grupo em relação ao total (0 a 100%). |
| **Index Map** (`Map`) | `Integer` | Mapa de índices (0..K-1) para cada elemento original (ideal para rotular geometrias 1:1 com tags de tipo). |
| **Grouped Tree** (`T`) | `Number` | Árvore de dados onde cada ramo {k} contém todos os valores pertencentes ao tipo k. |
| **Original Indices** (`iTree`) | `Integer` | Árvore com os índices originais dos elementos em cada grupo {k}. |
| **Quant Report** (`Rep`) | `Text` | Tabela quantitativa formatada e diagnóstico de tipologia. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
