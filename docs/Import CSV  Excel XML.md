---
name: "Import CSV / Excel XML"
nickname: "CSV_In"
category: "Buraqueira Tools"
subcategory: "I/O"
class: ""
file: "CSVImport_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, i/o]
---

# 🧩 Import CSV / Excel XML (`CSV_In`)

**Categoria:** `Buraqueira Tools` ➔ `I/O`  
**Arquivo C#:** `CSVImport_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Lê e estrutura arquivos tabulares (.csv, .tsv, .txt) e planilhas multi-aba Excel XML / SpreadsheetML (.xml, .xls) em árvores de dados 2D indexadas por planilha e coluna.

---

## 📥 Entradas (Inputs)

| Parâmetro                  |   Tipo    | Descrição                                                                                                                                          |                                                                                 |
| :------------------------- | :-------: | :------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| **File Path** (`P`)        |  `Text`   | Caminho absoluto do arquivo (.csv, .tsv, .txt, .xml, .xls) no disco.                                                                               |                                                                                 |
| **Delimiter** (`Delim`)    |  `Text`   | Delimitador de colunas para arquivos de texto (ex: ',', ';', '\\t', '                                                                              | '). Se vazio ou não fornecido, auto-detecta automaticamente. Ignorado para XML. |
| **Has Headers** (`Head`)   | `Boolean` | Se True, a primeira linha de cada planilha é tratada como nomes de cabeçalhos de coluna.                                                           |                                                                                 |
| **Read Trigger** (`Read`)  | `Boolean` | Gatilho booleano para executar/recarregar a leitura do arquivo.                                                                                    |                                                                                 |
| **Sheet Filter** (`Sheet`) | `Generic` | Filtro opcional de planilha/aba (para arquivos XML/XLS): nome da aba (ex: 'Loop 1') ou índice 0-based (ex: 0, 1). Se vazio, importa todas as abas. |                                                                                 |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Columns** (`Cols`) | `Generic` | Árvore de dados (DataTree) 2D onde cada ramo {sheet; col} contém a lista de valores da coluna col na planilha sheet. |
| **Rows** (`Rows`) | `Generic` | Árvore de dados (DataTree) 2D onde cada ramo {sheet; row} contém a lista de valores da linha row na planilha sheet. |
| **Headers** (`H`) | `Text` | Árvore de dados (DataTree) onde cada ramo {sheet} contém os nomes dos cabeçalhos das colunas da planilha sheet. |
| **Sheet Names** (`Sheets`) | `Text` | Lista com os nomes de todas as planilhas/abas importadas. |
| **Row Count** (`N`) | `Integer` | Quantidade de linhas de dados válidas lidas por planilha. |
| **Info** (`Info`) | `Text` | Relatório detalhado do arquivo importado (formato, abas, dimensões). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
