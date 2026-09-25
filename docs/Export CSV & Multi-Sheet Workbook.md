---
name: "Export CSV & Multi-Sheet Workbook"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "CSVExport_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Export CSV & Multi-Sheet Workbook (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `CSVExport_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados (DataTree) ou listas contendo os dados a exportar por colunas {col} ou linhas {row}. |
| **Headers** (`H`) | `Text` | Lista opcional de cabeçalhos para as colunas do arquivo. |
| **File Path** (`P`) | `Text` | Caminho de destino do arquivo no disco (ex: C:\\Dados\\resultado.csv ou C:\\Dados\\projeto.xls). Se a extensão for .xls ou .xml, ativa automaticamente o modo Multi-Sheet do Excel! |
| **Delimiter** (`Delim`) | `Text` | Delimitador de colunas para CSV (Padrão: ','). Aceita ',', ';', '\\t', '|'. Ignorado no modo Excel. |
| **Mode** (`M`) | `Integer` | Modo de gravação:\n |
| **Write Trigger** (`Write`) | `Boolean` | Gatilho booleano para executar a gravação no disco. |
| **Sheet Name / Loop ID** (`Sheet`) | `Text` | Nome opcional da planilha/aba ou identificador do loop atual (ex: 'Loop_1', 'Geracao_5', 'Teste_A'). Se omitido, nomeia automaticamente como 'Loop 1', 'Loop 2', etc. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **File Path** (`P`) | `Text` | Caminho do arquivo gravado no disco. |
| **Success** (`OK`) | `Boolean` | True se os dados foram gravados com sucesso. |
| **Info** (`Info`) | `Text` | Relatório de linhas exportadas, abas criadas e bytes gravados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
