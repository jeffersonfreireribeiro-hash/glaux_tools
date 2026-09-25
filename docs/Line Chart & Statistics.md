---
name: "Line Chart & Statistics"
nickname: "ChartLine"
category: "Buraqueira Tools"
subcategory: "Visual"
class: ""
file: "ChartLine_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, visual]
---

# 🧩 Line Chart & Statistics (`ChartLine`)

**Categoria:** `Buraqueira Tools` ➔ `Visual`  
**Arquivo C#:** `ChartLine_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Gera gráficos 2D de linhas ou colunas/histogramas de alta definição com suporte a curva agregada (Média/Moda/KDE), múltiplas curvas (DataTree), valor alvo (Target) com tolerância e sobreposição de indicadores estatísticos completos.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **X Values** (`X`) | `Number` | Valores do eixo X (Lista ou Árvore). Se omitido, utiliza índices incrementais 0, 1, 2... |
| **Y Values** (`Y`) | `Number` | Valores do eixo Y (Lista ou Árvore). Cada ramo representa uma curva/série independente. |
| **Title** (`T`) | `Text` | Título principal do gráfico. |
| **X Label** (`XLab`) | `Text` | Rótulo/Nome do eixo X. |
| **Y Label** (`YLab`) | `Text` | Rótulo/Nome do eixo Y. |
| **Show Stats** (`Stats`) | `Boolean` | Exibir linhas de referência estatística (Média, Mediana, Moda e Faixa ±1σ). |
| **Width** (`W`) | `Integer` | Largura da imagem exportada em pixels. |
| **Height** (`H`) | `Integer` | Altura da imagem exportada em pixels. |
| **Chart Mode** (`Mode`) | `Generic` | Modo do gráfico: 0 ou 'Lines' para Gráfico de Linhas; 1 ou 'Columns'/'Histogram' para Gráfico de Colunas/Histograma de distribuição com curva KDE suave (idêntico à referência). Padrão: 'Lines'. |
| **Combined Curve** (`Combined`) | `Boolean` | Quando verdadeiro, calcula e plota a CURVA AGREGADA que junta todos os dados (Curva Média/Moda nas linhas, ou Curva KDE contínua nas colunas), atenuando as curvas individuais ao fundo como nuvem translúcida. Padrão: true. |
| **Target Value** (`Target`) | `Generic` | Valor alvo opcional ou faixa ideal (ex.: 1.40, ou Interval(1.2, 1.6), ou '1.2 To 1.6'). Quando fornecido, plota a linha do alvo ('Id'), faixa sombreada de tolerância ('Tol') e badge de desvio 'Δ: (Valor - Alvo)' no topo. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Chart Image** (`Img`) | `Generic` | Imagem renderizada do gráfico (System.Drawing.Bitmap). |
| **Stats Report** (`Rep`) | `Text` | Relatório quantitativo com Média, Mediana, Moda, Desvio Padrão, Alvo e Extremos por série. |
| **Series Points** (`Pts`) | `Point` | Árvore de pontos 3D (X, Y, 0) das curvas ou das colunas. |
| **Series Curves** (`Crv`) | `Curve` | Curvas/Polilinhas das séries de dados no espaço do Rhino. |
| **Reference Lines** (`Refs`) | `Line` | Linhas de referência estatística no Rhino (Média, Mediana, Moda, ±1σ e Alvo). |
| **Combined Curve** (`Trend`) | `Curve` | Curva agregada sintetizada (Curva Média no modo Linhas ou Curva KDE no modo Colunas) no espaço 3D do Rhino. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
