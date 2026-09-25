---
name: "Pill Disk Load"
nickname: "PillLoad"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillDiskLoad_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Disk Load (`PillLoad`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillDiskLoad_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Carrega e restaura com fidelidade total qualquer árvore de dados ou geometria gravada em disco pelo Pill Disk Save (.pilldata). Suporta recarregamento sob demanda por botão ou timer.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Key** (`K`) | `Text` | Nome da chave a carregar ou caminho completo para o arquivo .pilldata. |
| **Directory** (`DIR`) | `Text` | Diretório onde procurar o arquivo. Se omitido, procura na subpasta 'PillVault' adjacente ao documento. |
| **Reload** (`R`) | `Boolean` | Gatilho para forçar a releitura do arquivo do disco (Botão ou Timer). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados restaurada com tipos e estrutura originais intactos. |
| **Timestamp** (`TS`) | `Text` | Data e hora em que o arquivo foi gravado no disco. |
| **Summary** (`SUM`) | `Text` | Informações de diagnóstico do arquivo (itens, galhos, tamanho em disco). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
