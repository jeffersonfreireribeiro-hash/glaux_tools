---
name: "Pill Tree Import"
nickname: "PillImport"
category: "Glaux Tools"
subcategory: "I/O"
class: "PillTreeImport_Component"
file: "PillTreeImport_Component.cs"
guid: "9886c29f-7dd5-430f-bd38-2c74b8caef41"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, io]
---

# 🧩 Pill Tree Import (`PillImport`)

**Categoria:** `Glaux Tools` ➔ `I/O`  
**Arquivo C#:** `PillTreeImport_Component.cs`  
**Classe:** `PillTreeImport_Component`  
**Pilha:** [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md)

---

## 📝 Descrição
Reconstrói uma DataTree exportada pelo Pill Tree Export (JSON, CSV longo, binário .glxt) ou gravada pelo Pill Disk Save (.pilldata), preservando caminhos, ramos vazios, nulos e tipos.
- Aceita texto (T) ou arquivo (Path); o formato é detectado pelo conteúdo quando 'Format' fica vazio.
- Arquivos só são relidos quando mudam (tamanho/data), evitando reprocessar a cada solução.
- Tipos de plugins não carregados são mantidos como valores opacos, sem perda ao reexportar.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Text** (`T`) | `Text` | Texto serializado (JSON, CSV longo ou Base64 de binário). Ignorado se File Path for informado. |
| **File Path** (`Path`) | `Text` | Arquivo a importar (.json, .csv, .glxt, .pilldata). Relativo = pasta do .gh. |
| **Format** (`F`) | `Text` | Vazio = detectar automaticamente. Ou: json, csv, binary, pilldata. |
| **Delimiter** (`Del`) | `Text` | Separador do CSV: ',' (padrão), ';' ou 'tab'. |
| **Reload** (`R`) | `Boolean` | Força a releitura do arquivo mesmo sem mudança detectada. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore reconstruída. |
| **Hash** (`H`) | `Text` | SHA-256 de identidade dos dados importados (igual ao do Export quando nada mudou). |
| **Metadata** (`M`) | `Text` | Metadados 'chave=valor' gravados no arquivo. |
| **Info** (`I`) | `Text` | Resumo: formato, ramos, itens, tipos e tempo de leitura. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 1 — Data Core & Serialização](stacks/01_Data_Core.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
