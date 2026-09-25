---
name: "Pill Transmitter"
nickname: "PillTx"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillTransmitter_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Transmitter (`PillTx`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillTransmitter_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Transmite dados ou árvores (DataTree) sem fiação pelo canvas, com categoria cromática automática por prefixo (ACU_, GEO_, MAT_), unidade embutida, timestamp e validação de integridade.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Key** (`K`) | `Text` | Identificador do canal (ex: 'ACU_T60 [s]', 'GEO::Malha', 'MAT_Absorcao'). O prefixo define a categoria e a cor do pill automaticamente. |
| **Data** (`D`) | `Generic` | Dados ou árvore (DataTree) a serem publicados no canal. |
| **Unit** (`U`) | `Text` | Unidade opcional (ex: 's', 'dB', 'Hz', 'm³'). Se não informada, será detectada automaticamente de colchetes na chave [unidade]. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Pass** (`D`) | `Generic` | Pass-through opcional dos dados (para inspecionar ou encadear sem fio adicional). |
| **Info** (`I`) | `Text` | Diagnóstico do canal: chave limpa, categoria, unidade, contagem e timestamp. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
