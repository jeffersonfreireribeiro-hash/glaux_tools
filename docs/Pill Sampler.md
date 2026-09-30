---
name: "Pill Sampler"
nickname: "Sampler"
category: "Glaux Tools"
subcategory: "Explore"
class: "PillSampler_Component"
file: "PillSampler_Component.cs"
guid: "6a60ae71-44a5-463b-80fe-ef1bfd987a8b"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, explore]
---

# 🧩 Pill Sampler (`Sampler`)

**Categoria:** `Glaux Tools` ➔ `Explore`  
**Arquivo C#:** `PillSampler_Component.cs`  
**Classe:** `PillSampler_Component`  
**Pilha:** [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md)

---

## 📝 Descrição
Gera as alternativas a rodar a partir do Pill Design Space. Métodos:

- lhs (padrão): Latin Hypercube, boa cobertura com poucas amostras;
- sobol: sequência quase aleatória (use potências de 2: 64, 128, 256…);
- random: Monte Carlo; grid: todas as combinações de N níveis;
- morris: trajetórias para triagem de sensibilidade (Count = trajetórias; total = Count × (k + 1));
- saltelli: para índices de Sobol (Count = N base; total = N × (k + 2)).

Mesma semente → mesmas amostras. Nada muda no canvas: ligue o plano no Pill Batch Runner.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Space** (`DS`) | `Generic` | Espaço de projeto do Pill Design Space. |
| **Method** (`M`) | `Text` | lhs, sobol, random, grid, morris ou saltelli. |
| **Count** (`N`) | `Integer` | Amostras (lhs, sobol, random), total aproximado (grid), trajetórias (morris) ou N base (saltelli). |
| **Seed** (`S`) | `Integer` | Semente: a mesma semente gera as mesmas amostras. |
| **Options** (`O`) | `Text` | Opcional: 'levels=5' (grid: níveis por variável; morris: níveis da grade), 'maximin=20' (lhs), 'centered' (lhs), 'scramble=false' (sobol/saltelli). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Plan** (`P`) | `Generic` | Plano de amostras (ligue no Pill Batch Runner). |
| **Samples** (`S`) | `Generic` | Um ramo {i} por amostra com o valor de cada variável (níveis como texto). |
| **Unit** (`U`) | `Number` | Mesmas amostras em coordenadas unitárias [0, 1] (úteis para gráficos). |
| **Count** (`N`) | `Integer` | Quantidade de amostras (execuções necessárias). |
| **Info** (`I`) | `Text` | Método, tamanho e observações. |

---

## 💡 Notas de Implementação & Uso
* **Fluxo completo, métodos e quando usar cada um:** ver [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md).
* **Determinístico:** mesma entrada e mesma semente → exatamente as mesmas amostras (gerador SplitMix64 próprio, igual em qualquer máquina).
* **Variáveis discretas:** cada valor possível (passo, nível) recebe uma faixa igual do intervalo [0, 1], então amostragens uniformes continuam uniformes depois da conversão.
* **Limites:** até 100 000 amostras por plano e 128 variáveis (Saltelli usa 2 × k dimensões de Sobol, até 256).
* **Testes automatizados:** núcleo (espaço, amostragem, sensibilidade, lote) e adaptadores de controle cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); o laço no canvas precisa ser validado dentro do Grasshopper.
