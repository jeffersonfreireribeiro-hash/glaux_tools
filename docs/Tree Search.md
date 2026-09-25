---
name: "Tree Search"
nickname: "TreeSearch"
category: "Buraqueira Tools"
subcategory: "Tree"
class: ""
file: "TreeSearch_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, tree]
---

# 🧩 Tree Search (`TreeSearch`)

**Categoria:** `Buraqueira Tools` ➔ `Tree`  
**Arquivo C#:** `TreeSearch_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Busca termos específicos, palavras-chave ou padrões regex em listas ou árvores de dados. Retorna os itens correspondentes, seus índices (IDs locais), caminhos, itens descartados e máscara booleana.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore ou lista de dados onde a pesquisa será realizada. |
| **Query** (`Q`) | `Text` | Termo(s), palavra-chave ou padrão regex a ser buscado. |
| **Exact Match** (`E`) | `Boolean` | Se True, exige correspondência exata do texto inteiro. Se False, busca como substring (Contém). |
| **Case Sensitive** (`C`) | `Boolean` | Se True, diferencia maiúsculas de minúsculas. |
| **Regex** (`Rx`) | `Boolean` | Se True, trata a query como uma Expressão Regular (Regex). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Matched Items** (`M`) | `Generic` | Itens que contêm o termo pesquisado (preservando estrutura de árvore). |
| **Matched Indices** (`iM`) | `Integer` | Índices (IDs locais 0-indexados) dos itens encontrados dentro de seus respectivos ramos. |
| **Matched Paths** (`P`) | `Path` | Caminhos (GH_Path) onde ocorreram correspondências. |
| **Remainder Items** (`NM`) | `Generic` | Itens que NÃO atenderam à busca (resto/descartados). |
| **Match Mask** (`B`) | `Boolean` | Máscara booleana (True para match, False para descarte) com a mesma estrutura da árvore de entrada. |
| **Match Count** (`N`) | `Integer` | Quantidade total de itens encontrados em toda a árvore. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
