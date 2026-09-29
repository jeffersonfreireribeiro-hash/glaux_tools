---
name: "Pill Tree Export"
nickname: "PillExport"
category: "Glaux Tools"
subcategory: "I/O"
class: "PillTreeExport_Component"
file: "PillTreeExport_Component.cs"
guid: "d34afde2-5f9f-44bc-a885-8d407e5885ea"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, io]
---

# 🧩 Pill Tree Export (`PillExport`)

**Categoria:** `Glaux Tools` ➔ `I/O`  
**Arquivo C#:** `PillTreeExport_Component.cs`  
**Classe:** `PillTreeExport_Component`  
**Pilha:** [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md)

---

## 📝 Descrição
Serializa qualquer DataTree sem perda estrutural (caminhos de qualquer profundidade, ramos vazios, nulos e tipos mistos) em JSON tipado, CSV longo (path,index,type,value), binário Glaux (.glxt) ou .pilldata (compatível com Pill Disk Save/Load).
- Sem 'File Path': devolve o texto (formatos binários em Base64).
- Com 'File Path': grava o arquivo de forma atômica quando 'Write' = True.
- Emite o hash SHA-256 de identidade exata dos dados (diferente do fingerprint tolerante do Pill Cache).

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados (DataTree) a exportar. |
| **Format** (`F`) | `Text` | Formato: 'json' (padrão), 'csv' (longo: path,index,type,value,data), 'binary' (.glxt compacto) ou 'pilldata' (Pill Disk Save). Com File Path, a extensão do arquivo é usada se este campo estiver vazio. |
| **File Path** (`Path`) | `Text` | Arquivo de destino opcional. Relativo = pasta do .gh. Sem arquivo, o resultado sai como texto. |
| **Write** (`W`) | `Boolean` | Grava o arquivo quando True (conecte um botão). Ignorado sem File Path. |
| **Metadata** (`M`) | `Text` | Metadados opcionais no formato 'chave=valor' (gravados no JSON/CSV/binário; não alteram o hash dos dados). |
| **Pretty** (`P`) | `Boolean` | JSON indentado (legível). Não afeta os dados. |
| **Delimiter** (`Del`) | `Text` | Separador do CSV: ',' (padrão), ';' (Excel pt-BR) ou 'tab'. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Text** (`T`) | `Text` | Árvore serializada (somente quando não há File Path). Formatos binários em Base64. |
| **Hash** (`H`) | `Text` | SHA-256 de identidade exata dos dados (ignora metadados). |
| **File Path** (`Path`) | `Text` | Arquivo gravado nesta solução (vazio se nada foi gravado). |
| **Info** (`I`) | `Text` | Resumo: ramos, itens, tipos, profundidade, tamanho e tempo. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
