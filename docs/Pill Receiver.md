---
name: "Pill Receiver"
nickname: "PillRx"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillReceiver_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Receiver (`PillRx`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillReceiver_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Recebe dados ou árvores (DataTree) de um canal sem fios Pill. Detecta incompatibilidade de tipo, calcula tempo decorrido e preserva a estrutura original de dados com imutabilidade absoluta.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Key** (`K`) | `Text` | Nome do canal a escutar (pode ser conectado via cabo ou selecionado diretamente no menu de clique direito do componente). |
| **ExpectedType** (`T`) | `Text` | Tipo de dado esperado opcional para validação (ex: 'Number', 'Point', 'Mesh', 'Curve', 'String'). Emite aviso se houver divergência. |
| **Wire** (`⚡`) | `Generic` | Cabo físico oculto conectado ao Pill Transmitter (Wire Display: Hidden). Garante sincronização sequencial perfeita para o Wallacei/Galapagos mantendo a estética limpa. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dados recebidos do canal sem fios, preservando a árvore original e com imutabilidade garantida. |
| **Timestamp** (`TS`) | `Text` | Horário exato da última atualização transmitida. |
| **Status** (`S`) | `Text` | Status do canal: Online, Órfão (Transmitter ausente), Mismatch (tipo incompatível) ou Desatualizado. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
