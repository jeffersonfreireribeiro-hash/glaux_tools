---
name: "Pill Data Validation"
nickname: "PillValidate"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDataValidation_Component"
file: "PillDataValidation_Component.cs"
guid: "89a5e962-0c8e-4877-91ac-a61aaa196072"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill Data Validation (`PillValidate`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDataValidation_Component.cs`  
**Classe:** `PillDataValidation_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Valida uma DataTree antes de gravar ou exportar, reunindo num só lugar regras de tipo, nulos, duplicatas (por ramo ou globais), faixa numérica e estrutura (profundidade, número de ramos, comprimento uniforme, ramos obrigatórios). Emite a lista de problemas com caminho/índice, uma máscara paralela à árvore e a árvore só com os itens aprovados. Conecte 'Valid' ao 'Write' do Pill DB Write para só gravar dados válidos.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore a validar. |
| **Types** (`Ty`) | `Text` | Tipos aceitos (Number, Integer, Text, Point, Curve...). Vazio = qualquer. |
| **Allow Nulls** (`N`) | `Boolean` | Aceita itens nulos. |
| **Unique** (`U`) | `Integer` | Duplicatas: 0 = permitidas, 1 = proibidas no mesmo ramo, 2 = proibidas na árvore toda. |
| **Range** (`Rg`) | `Interval` | Faixa numérica aceita (itens não numéricos falham quando há faixa). |
| **Depth** (`Dp`) | `Integer` | Profundidade exata exigida dos caminhos (ex: 2 para {a;b}). |
| **Branch Count** (`BC`) | `Integer` | Número exato de ramos exigido. |
| **Uniform Length** (`UL`) | `Boolean` | Todos os ramos com a mesma quantidade de itens. |
| **Required Paths** (`RP`) | `Text` | Ramos que precisam existir (ex: '{0;0}'). |
| **Require Items** (`RI`) | `Boolean` | A árvore não pode estar vazia. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Valid** (`OK`) | `Boolean` | True se nenhuma regra falhou. |
| **Issues** (`Is`) | `Text` | Problemas encontrados (regra, caminho, índice e motivo). |
| **Issue Paths** (`IP`) | `Text` | Caminho de cada problema. |
| **Mask** (`M`) | `Boolean` | Máscara paralela à árvore: True = item aprovado. |
| **Valid Data** (`VD`) | `Generic` | A árvore só com os itens aprovados (caminhos preservados). |
| **Summary** (`S`) | `Text` | Resumo por regra. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
