---
name: "Path Dictionary (Remap & Lookup)"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PathDictionary_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Path Dictionary (Remap & Lookup) (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PathDictionary_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Tree** (`T`) | `Generic` | Árvore de dados cujos caminhos serão remapeados ou consultados. |
| **Keys (From)** (`K`) | `Generic` | Chaves de origem. Aceita caminhos GH_Path, texto tipo '{5;4}', ou pares tipo '{5;4} -> {0}'. Se vazio, indexa automaticamente todos os caminhos. |
| **Values (To)** (`V`) | `Generic` | Novos caminhos de destino. Aceita caminhos GH_Path, inteiros ou texto. Opcional se K contiver pares 'De -> Para' ou se estiver usando modo auto-indexador. |
| **Unmatched Action** (`U`) | `Integer` | Ação para caminhos não cadastrados no dicionário:\n0 = Manter Original\n1 = Descartar / Filtrar (Drop)\n2 = Enviar para Caminho Padrão |
| **Default Path** (`Def`) | `Generic` | Caminho padrão quando U = 2. Padrão: {999}. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Remapped Tree** (`T`) | `Generic` | Árvore de dados com os caminhos remapeados pelo dicionário. |
| **Dictionary Map** (`Dict`) | `Text` | Tabela / Mapeamento do dicionário ({Origem} -> {Destino}). |
| **Unmatched Tree** (`Un`) | `Generic` | Árvore contendo os ramos que não foram encontrados no dicionário. |
| **Keys Count** (`N_keys`) | `Integer` | Quantidade total de chaves cadastradas no dicionário. |
| **Matched Count** (`N_match`) | `Integer` | Quantidade de ramos que foram mapeados com sucesso. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
