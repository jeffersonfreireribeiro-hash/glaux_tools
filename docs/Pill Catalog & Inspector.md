---
name: "Pill Catalog & Inspector"
nickname: "PillCatalog"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillCatalog_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Catalog & Inspector (`PillCatalog`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillCatalog_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Varre o documento ativo e o barramento global, catalogando todos os transmitters e receivers. Detecta canais órfãos, transmissões duplicadas, tipos de dados e mapa de dependências.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Refresh** (`R`) | `Boolean` | Pulso opcional para forçar nova auditoria do canvas. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Channels** (`C`) | `Text` | Lista com o nome limpo de todos os canais ativos no documento/barramento. |
| **Status** (`S`) | `Text` | Status de integridade: 'Conectado (Tx+Rx)', 'Órfão (sem Rx)', 'Órfão (sem Tx)' ou 'Duplicado (Tx Múltiplo)'. |
| **Categories** (`CAT`) | `Text` | Categoria de cada canal (ACU, GEO, MAT, SIM, etc.). |
| **Types** (`T`) | `Text` | Tipo de dado transmitido em cada canal. |
| **AuditReport** (`RPT`) | `Text` | Relatório consolidado de auditoria com estatísticas e integridade de conexões. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
