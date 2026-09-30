---
name: "Pill Design Space"
nickname: "DesignSpace"
category: "Glaux Tools"
subcategory: "Explore"
class: "PillDesignSpace_Component"
file: "PillDesignSpace_Component.cs"
guid: "36f19785-0348-4a2c-8fe3-60c984a432f7"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, explore]
---

# 🧩 Pill Design Space (`DesignSpace`)

**Categoria:** `Glaux Tools` ➔ `Explore`  
**Arquivo C#:** `PillDesignSpace_Component.cs`  
**Classe:** `PillDesignSpace_Component`  
**Pilha:** [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md)

---

## 📝 Descrição
Define as variáveis de projeto a explorar (o que o Pill Sampler vai variar e o Pill Batch Runner vai aplicar):

- Controls: ligue sliders, toggles, value lists, um Pill Slider Pool ou um Pill Dashboard (todos os controles dele);
- Names: ou escolha controles pelo nome, com curingas ('Largura', '[VAR]*', '*');
- Ranges: ajuste faixas e níveis: 'Largura | min=2 | max=8 | step=0.5', 'Material | levels=Concreto;Madeira', 'Rotação | off'.

Por padrão vale a faixa do próprio controle; o passo vem da precisão do slider (inteiro, 1 casa decimal...).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Controls** (`C`) | `Generic` | Controles a variar (ligue sliders, toggles, value lists, Pill Slider Pool ou Pill Dashboard). Só a origem do fio importa, não o valor. |
| **Names** (`N`) | `Text` | Controles pelo nome ou apelido, com curingas (* e ?). Sliders do Slider Pool: '[CATEGORIA] Nome'; do Dashboard: 'Painel: Rótulo'. |
| **Ranges** (`R`) | `Text` | Uma linha por ajuste: 'Nome \| min=… \| max=… \| step=…', 'Nome \| levels=A;B;C' ou 'Nome \| off'. O nome aceita curingas. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Space** (`DS`) | `Generic` | Espaço de projeto (ligue no Pill Sampler). |
| **Names** (`N`) | `Text` | Nome de cada variável, na ordem das colunas das amostras. |
| **Ranges** (`R`) | `Text` | Faixa, passo ou níveis de cada variável. |
| **Count** (`k`) | `Integer` | Quantidade de variáveis. |
| **Info** (`I`) | `Text` | Resumo (tamanho do espaço discreto, avisos). |

---

## 💡 Notas de Implementação & Uso
* **Fluxo completo, arquitetura, métodos e limitações:** ver [Pilha 7 — Exploração de Design](stacks/07_Exploracao_de_Design.md).
* **Passo padrão:** vem da precisão do controle (slider inteiro → 1; par/ímpar → 2; float com 2 casas → 0,01; Slider Pool → casas decimais do item; Dashboard → `step=` do widget). Assim a amostra aplicada é exatamente o valor que o controle aceita.
* **Faixas além do controle:** um aviso é mostrado; o controle limita os valores e o Batch Runner registra o valor realmente aplicado.
* **Ligar por fio × pelo nome:** o fio é o jeito mais direto; com o fio, o Design Space e o Sampler recalculam a cada amostra aplicada (o Sampler reaproveita o plano quando nada mudou). `Names` evita essa cadeia e alcança sliders do Slider Pool e do Dashboard sem fio.
* **Testes automatizados:** núcleo (espaço, amostragem, sensibilidade, lote) e adaptadores de controle cobertos em `tests/Glaux_Tools.Tests` (fora do Rhino); o laço no canvas precisa ser validado dentro do Grasshopper.
