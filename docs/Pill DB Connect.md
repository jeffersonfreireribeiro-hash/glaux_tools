---
name: "Pill DB Connect"
nickname: "PillDB"
category: "Glaux Tools"
subcategory: "Data"
class: "PillDbConnect_Component"
file: "PillDbConnect_Component.cs"
guid: "a662defd-cf3b-4847-ab9e-8ddb8eaf3fe3"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, data]
---

# 🧩 Pill DB Connect (`PillDB`)

**Categoria:** `Glaux Tools` ➔ `Data`  
**Arquivo C#:** `PillDbConnect_Component.cs`  
**Classe:** `PillDbConnect_Component`  
**Pilha:** [Pilha 2 — Data & Persistence](stacks/02_Persistence.md)

---

## 📝 Descrição
Abre ou cria um store local de dados paramétricos (.glauxdb): um arquivo único, sem dependências externas, com revisões, consultas e histórico.
- Vazio = store padrão do projeto na pasta PillVault ao lado do .gh.
- A mesma conexão é compartilhada por todos os componentes e documentos (sem arquivos presos abertos).
- 'Compact' reescreve o arquivo mantendo só as últimas N revisões de cada chave.
- Keys/Entries mostram o estado na última execução; para acompanhar gravações ao vivo use o Pill Schema Inspector.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Path** (`Path`) | `Text` | Arquivo .glauxdb (relativo = pasta do .gh). Vazio = PillVault/ |
| **Read Only** (`RO`) | `Boolean` | Somente leitura: componentes de escrita recusam gravar neste store. |
| **Compact** (`C`) | `Boolean` | Reescreve o arquivo mantendo só as últimas 'Keep' revisões de cada chave (conecte um botão). |
| **Keep** (`K`) | `Integer` | Revisões mantidas por chave na compactação (0 = todas; só limpa gravações interrompidas). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão com o store para os componentes Pill DB / Vault. |
| **Keys** (`K`) | `Text` | Chaves existentes (formato 'tipo:chave'). |
| **Entries** (`N`) | `Integer` | Total de entradas (revisões) no arquivo. |
| **Info** (`I`) | `Text` | Arquivo, tamanho, versão do formato e integridade. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 2 — Data & Persistence](stacks/02_Persistence.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
