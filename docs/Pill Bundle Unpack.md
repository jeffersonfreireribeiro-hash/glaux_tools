---
name: "Pill Bundle Unpack"
nickname: "PillUnpack"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillBundleUnpack_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Bundle Unpack (`PillUnpack`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillBundleUnpack_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Desempacota parâmetros de um PillBundle ou de uma string JSON externa, restaurando as chaves, valores e estruturas de árvore originais com total imutabilidade.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Bundle** (`B`) | `Generic` | Pacote PillBundle ou string JSON contendo os parâmetros estruturados. |
| **KeyFilter** (`F`) | `Text` | Filtro opcional de chaves específicas ou grupos/categorias a extrair (ex: 'ACU', 'GEO', 'MAT', 'ACU_T60'). Se vazio ou '*', desempacota todos os parâmetros contidos. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Keys** (`K`) | `Text` | Lista de chaves extraídas. |
| **Values** (`V`) | `Generic` | Árvore com os valores desempacotados. O ramo {i} contém os valores da chave K[i]. |
| **Summary** (`S`) | `Text` | Diagnóstico e resumo do conteúdo desempacotado. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
