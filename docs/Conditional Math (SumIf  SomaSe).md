---
name: "Conditional Math (SumIf / SomaSe)"
nickname: "SomaSe"
category: "Buraqueira Tools"
subcategory: "Statistics"
class: ""
file: "SumIf_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, statistics]
---

# 🧩 Conditional Math (SumIf / SomaSe) (`SomaSe`)

**Categoria:** `Buraqueira Tools` ➔ `Statistics`  
**Arquivo C#:** `SumIf_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Executa operações condicionais no estilo Excel (SOMASE, SOMASES, CONT.SE, MÉDIA.SE, MULT.SE). Avalia critérios numéricos ou expressões de texto (ex.: '>50', '<=0', '=A', '!=0') e máscaras booleanas, com suporte a intervalo de soma separado.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Values** (`V`) | `Generic` | Valores a serem somados/agregados (intervalo de soma). |
| **Criteria** (`C`) | `Generic` | Critério de condição: expressão de texto (ex: '>50', '<0', '>=10', '!=5', '=A'), número de igualdade ou máscara booleana. |
| **Evaluation Range** (`R`) | `Generic` | Faixa de avaliação opcional. Se não fornecida, avalia o critério diretamente em Values. Se fornecida, avalia o critério em R e soma os valores correspondentes em V. |
| **Operation** (`Op`) | `Integer` | Operação: 0=Soma (SOMASE / SUMIF), 1=Média (MÉDIA.SE / AVERAGEIF), 2=Contagem (CONT.SE / COUNTIF), 3=Produto (MULT.SE / PRODUCTIF), 4=Mínimo (MÍN.SE / MINIF), 5=Máximo (MÁX.SE / MAXIF). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Result** (`R`) | `Number` | Resultado escalar agregado para cada ramo. |
| **Filtered Values** (`F`) | `Generic` | Árvore de dados contendo apenas os elementos que atenderam ao critério. |
| **Mask** (`M`) | `Boolean` | Máscara booleana correspondente 1:1 com os itens avaliados. |
| **Count** (`N`) | `Integer` | Quantidade de elementos aceitos pelo critério por ramo. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
