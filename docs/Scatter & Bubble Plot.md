---
name: "Scatter & Bubble Plot"
nickname: "ChartScatter"
category: "Buraqueira Tools"
subcategory: "Visual"
class: ""
file: "ChartScatter_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, visual]
---

# 🧩 Scatter & Bubble Plot (`ChartScatter`)

**Categoria:** `Buraqueira Tools` ➔ `Visual`  
**Arquivo C#:** `ChartScatter_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Gera gráficos de dispersão 2D com marcadores de bolinhas (Scatter/Bubble Plot), centróide médio, regressão linear (R²) e elipse de dispersão, RENDERIZADO DIRETAMENTE NO PAINEL DO CANVAS DO GRASSHOPPER.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **X Values** (`X`) | `Number` | Valores do eixo X (Lista ou Árvore). |
| **Y Values** (`Y`) | `Number` | Valores do eixo Y (Lista ou Árvore). |
| **Bubble Radii** (`R`) | `Number` | Raios/tamanhos opcionais das bolinhas para cada ponto (em pixels). Padrão: 5.0. |
| **Color Values** (`C`) | `Number` | Valores escalares opcionais para mapeamento em gradiente de cores térmico/esmeralda. |
| **Title** (`T`) | `Text` | Título principal do gráfico. |
| **X Label** (`XLab`) | `Text` | Rótulo do eixo X. |
| **Y Label** (`YLab`) | `Text` | Rótulo do eixo Y. |
| **Show Trend & Stats** (`Stats`) | `Boolean` | Exibir reta de regressão linear, centróide (x̄, ȳ), elipse de dispersão e R². |
| **Width** (`W`) | `Integer` | Largura da imagem exportada em pixels. |
| **Height** (`H`) | `Integer` | Altura da imagem exportada em pixels. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Chart Image** (`Img`) | `Generic` | Imagem renderizada do gráfico de dispersão (System.Drawing.Bitmap). |
| **Regression Report** (`Rep`) | `Text` | Relatório com Equação Linear (y = ax + b), R², Pearson r, Covariância e Centróide. |
| **Centroid** (`Center`) | `Point` | Centróide médio dos pontos (x̄, ȳ, 0) no Rhino. |
| **Trend Line** (`Trend`) | `Line` | Linha de regressão linear no espaço 3D do Rhino. |
| **Dispersion Ellipse** (`Ellipse`) | `Curve` | Elipse de dispersão estatística (±1σ) no espaço do Rhino. |
| **Scatter Points** (`Pts`) | `Point` | Pontos 3D das bolinhas mapeadas no Rhino. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
