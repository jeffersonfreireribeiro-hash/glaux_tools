---
name: "Pill Experiment Logger"
nickname: "PillExpLog"
category: "Glaux Tools"
subcategory: "Vault"
class: "PillExperimentLogger_Component"
file: "PillExperimentLogger_Component.cs"
guid: "04035cb4-dd6f-4df7-8683-c2012511c8ee"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, vault]
---

# 🧩 Pill Experiment Logger (`PillExpLog`)

**Categoria:** `Glaux Tools` ➔ `Vault`  
**Arquivo C#:** `PillExperimentLogger_Component.cs`  
**Classe:** `PillExperimentLogger_Component`  
**Pilha:** [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md)

---

## 📝 Descrição
Registra cada execução de uma simulação ou otimização (Wallacei, Galapagos, estudos paramétricos) como uma revisão do experimento no store: entrada (parâmetros/árvore) → configuração → versões → resultado → tempo de execução.
- Com 'Log' ligado, cada solução com entradas/resultados novos vira uma execução (duplicatas consecutivas são ignoradas).
- 'Buffer' agrupa N execuções numa gravação só (lote) para loops rápidos; o buffer é gravado também ao fechar o documento. Depois, use Pill DB Query (Tree 'result') para achar as execuções com um resultado e Pill Restore/History para ver com quais parâmetros ele foi produzido.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Store** (`S`) | `Generic` | Conexão do Pill DB Connect, ou caminho de um arquivo .glauxdb. Vazio = store padrão do projeto (pasta PillVault do .gh). |
| **Experiment** (`E`) | `Text` | Nome do experimento (cada execução vira uma revisão desta chave). |
| **Parameters** (`P`) | `Generic` | PillBundle(s) com os parâmetros da execução. |
| **Inputs** (`In`) | `Generic` | Árvore de entradas da execução (alternativa ou complemento aos parâmetros). |
| **Results** (`Res`) | `Generic` | Árvore de resultados/objetivos da execução. |
| **Config** (`Cfg`) | `Text` | Configuração 'chave=valor' (ex: 'solver=raytracing', 'raios=10000'). |
| **Runtime** (`ms`) | `Number` | Tempo da execução em ms. |
| **Log** (`L`) | `Boolean` | Registra quando True (toggle ligado durante a otimização). |
| **Buffer** (`B`) | `Integer` | Execuções acumuladas antes de gravar em lote (1 = grava cada uma). |
| **Skip Duplicates** (`SD`) | `Boolean` | Ignora uma execução idêntica (entradas e resultados) à anterior. |
| **Flush** (`F`) | `Boolean` | Grava imediatamente o que estiver no buffer. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Run** (`Run`) | `Integer` | Número (revisão) da última execução gravada. |
| **Logged** (`N`) | `Integer` | Execuções registradas nesta sessão. |
| **Pending** (`Pd`) | `Integer` | Execuções no buffer aguardando gravação. |
| **Info** (`I`) | `Text` | Resumo. |

---

## 💡 Notas de Implementação & Uso
* **Arquitetura, tipos, limitações, desempenho, persistência e testes:** ver [Pilha 3 — Project Vault & Provenance](stacks/03_Project_Vault.md).
* **Testes automatizados:** núcleo coberto em `tests/Glaux_Tools.Tests` (fora do Rhino); o componente em si precisa ser validado dentro do Grasshopper.
