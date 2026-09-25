---
name: "Pill Isometric Surface Graph"
nickname: "IsoSurface"
category: "Glaux Tools"
subcategory: "Visual"
class: "PillIsometricSurfaceGraph_Component"
file: "PillIsometricSurfaceGraph_Component.cs"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, visual, pills, 3d, isométrica, surface, canvas]
---

# 🧩 Pill Isometric Surface Graph (`IsoSurface`)

**Categoria:** `Glaux Tools` ➔ `Visual`  
**Arquivo C#:** `PillIsometricSurfaceGraph_Component.cs`  
**Classe:** `PillIsometricSurfaceGraph_Component`  
**Status:** `Compilado / Ativo` (Release & Debug)

---

## 📝 Descrição

O **Pill Isometric Surface Graph** renderiza um gráfico de superfície tridimensional contínua em **projeção isométrica / axonométrica diretamente dentro do Canvas do Grasshopper**.

Projetado especialmente para visualização de matrizes espaciais, distribuições acústicas ($D_1$, $EDT$, $T_{30}$, $C_{80}$, SPL), campos de pressão sonora e dados de otimização paramétrica:
- **4 Quadrantes Isométricos Selecionáveis:** Permite alternar instantaneamente entre os pontos de vista cardeais:
  - `0` / `"SW"` / `"Sudoeste"`: Visão frontal padrão (idêntica ao estilo matplotlib clássico);
  - `1` / `"SE"` / `"Sudeste"`;
  - `2` / `"NE"` / `"Nordeste"`;
  - `3` / `"NW"` / `"Noroeste"`.
- **Malha com Gradiente e Wireframe:** Facetas quadrilaterais coloridas segundo o gradiente ativo com ordenação de profundidade (*Painter's algorithm*) e linhas pretas de malha.
- **Caixa 3D Delimitadora (Bounding Box):** Planos de fundo e chão com linhas de grade suaves, eixos de coordenadas ($X, Y, Z$), ticks e rótulos numéricos formatados.
- **Legenda Colorbar Vertical:** Barra de gradiente com escala e valores numéricos posicionada à direita da área gráfica.
- **Malha Nativa do Rhino (`Mesh 3D`):** Emite a geometria 3D com cores por vértice para visualização no Viewport 3D do Rhino.
- **Exportação em Alta Resolução (300 DPI):** Botão `📷 Salvar PNG` interativo no cabeçalho e gatilho `Save Image` para exportar a figura com fundo branco puro pronta para pranchas e publicações científicas.

---

## 📥 Entradas (Inputs)

| Parâmetro | Nick | Acesso | Tipo C# / Goo | Descrição |
| :--- | :---: | :---: | :--- | :--- |
| **X Values** | `X` | `List` | `Number` (`double`) | Lista de coordenadas do eixo horizontal X ($x_1$). Se omitido, adota $0, 1, 2...$ |
| **Y Values** | `Y` | `List` | `Number` (`double`) | Lista de coordenadas do eixo de profundidade Y ($x_2$). Se omitido, adota $0, 1, 2...$ |
| **Values (Z)** | `V` | `Tree` | `Generic` / `double` | Valores de elevação/altura Z ($D_1$). Aceita `DataTree` ($N_y$ ramos), lista plana ($N_x \times N_y$) ou lista de `Point3d`. |
| **Quadrant / View** | `View` | `Item` | `Generic` | Ponto de vista isométrico (0="SW", 1="SE", 2="NE", 3="NW"). Padrão: 0 (SW). |
| **Elevation** | `Elev` | `Item` | `Number` (`double`) | Ângulo de elevação vertical em graus ($10^\circ$ a $80^\circ$). Padrão: $30^\circ$. |
| **Colormap** | `Col` | `List` | `Generic` / `Color` | Paleta de cores ('Jet', 'Turbo', 'Viridis', 'Inferno', 'Plasma', 'CoolWarm', 'Spectral') ou lista de cores customizadas. Padrão: 'Jet'. |
| **Title** | `T` | `Item` | `Text` (`string`) | Título principal renderizado no cabeçalho. Padrão: 'Shoebox EDT'. |
| **X Label** | `XLab` | `Item` | `Text` (`string`) | Rótulo da variável no eixo horizontal X. Padrão: 'x1'. |
| **Y Label** | `YLab` | `Item` | `Text` (`string`) | Rótulo da variável no eixo de profundidade Y. Padrão: 'x2'. |
| **Z Label** | `ZLab` | `Item` | `Text` (`string`) | Rótulo da grandeza no eixo vertical Z. Padrão: 'D1'. |
| **Wireframe** | `Wire` | `Item` | `Boolean` (`bool`) | Desenha linhas de grade sobre as facetas da malha. Padrão: `True`. |
| **Domain (Z Limits)** | `D` | `Item` | `Generic` (`Interval`) | Limites manuais de Mínimo e Máximo de Z (ex: Interval(-0.05, 0.0)). Opcional. |
| **Export Folder** | `Folder` | `Item` | `Text` (`string`) | Pasta de destino para gravação da imagem PNG. Se vazio, adota a Área de Trabalho. |
| **Save Image** | `Save` | `Item` | `Boolean` (`bool`) | Gatilho booleano para exportar o arquivo PNG em 300 DPI com fundo branco. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Nick | Acesso | Tipo C# / Goo | Descrição |
| :--- | :---: | :---: | :--- | :--- |
| **Chart Image** | `Img` | `Item` | `Generic` (`Bitmap`) | Imagem renderizada do gráfico de superfície isométrica 3D. |
| **Mesh 3D** | `M` | `Item` | `Mesh` (`Rhino.Geometry.Mesh`) | Malha tridimensional nativa do Rhino com cores por vértice baseadas no gradiente. |
| **Domain** | `D` | `Item` | `Interval` | Intervalo adotado de variação do eixo Z $[Z_{min}, Z_{max}]$. |
| **Summary** | `T` | `Item` | `Text` (`string`) | Relatório com dimensões da grade e estatísticas de Z ($N_x \times N_y, Z_{min}, Z_{max}, \mu, \sigma$). |
| **Saved Path** | `Path` | `Item` | `Text` (`string`) | Caminho absoluto do arquivo PNG gravado com sucesso no disco. |

---

## 🔗 Conexões & Compatibilidade de Pilhas (Pills)

```mermaid
flowchart LR
    subgraph Upstream["Upstream (Fornecedores de Dados)"]
        Analyzer["[[Room Acoustic Analyzer]]<br><i>T30, EDT, C80</i>"]
        Heatmap["[[Spatial Grid & Viewport Heatmap]]<br><i>GridVals, GridPts</i>"]
        DataStack["[[Data Stack]]<br><i>Data (D)</i>"]
        MatrixSolver["[[Matrix Solver]]<br><i>Matrix (M)</i>"]
    end

    subgraph ThisComponent["Este Componente"]
        IsoSurface["<b>Pill Isometric Surface Graph</b><br><i>(IsoSurface)</i>"]
    end

    subgraph Downstream["Downstream (Consumidores)"]
        RhinoView["<b>Rhino Viewport 3D</b><br><i>Mesh (M) Bake / Preview</i>"]
        PillDisk["[[Pill Disk Save]]<br><i>Data / Image Save</i>"]
        Panel["<b>GH Panel</b><br><i>Summary (T)</i>"]
    end

    Analyzer -->|Values (V)| IsoSurface
    Heatmap -->|Values (V)| IsoSurface
    DataStack -->|Values (V)| IsoSurface
    MatrixSolver -->|Values (V)| IsoSurface

    IsoSurface -->|Mesh 3D (M)| RhinoView
    IsoSurface -->|Chart Image (Img)| PillDisk
    IsoSurface -->|Summary (T)| Panel
```

---

## 📐 Formulação Matemática da Projeção

1. **Transformação para o Cubo Normalizado $[-1, 1]^3$:**
   $$x_{norm} = 2 \cdot \frac{x - X_{min}}{X_{max} - X_{min}} - 1, \quad y_{norm} = 2 \cdot \frac{y - Y_{min}}{Y_{max} - Y_{min}} - 1, \quad z_{norm} = 2 \cdot \frac{z - Z_{min}}{Z_{max} - Z_{min}} - 1$$

2. **Rotação de Azimute $\theta$ e Inclinação $\phi$:**
   $$\begin{aligned}
   x' &= x_{norm} \cos\theta - y_{norm} \sin\theta \\
   y' &= x_{norm} \sin\theta + y_{norm} \cos\theta \\
   u &= x' \\
   v &= y' \sin\phi - z_{norm} \cos\phi \\
   w &= y' \cos\phi + z_{norm} \sin\phi \quad (\text{profundidade})
   \end{aligned}$$

3. **Mapeamento de Tela e Ordenação (Painter's Algorithm):**
   $$X_{canvas} = X_{center} + u \cdot S_x, \quad Y_{canvas} = Y_{center} + v \cdot S_y$$
   As facetas $Q_{i, j}$ são ordenadas crescentemente por $w_{avg} = \frac{1}{4} \sum_{k=1}^4 w_k$ para garantir renderização livre de sobreposição oclusiva.
