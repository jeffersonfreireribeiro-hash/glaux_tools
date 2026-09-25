---
name: "Pill Tag Receiver"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillHook_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Pill Tag Receiver (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PillHook_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Key** (`K`) | `Text` | Nome opcional do canal Pill a escutar (se omitido, selecione o canal clicando diretamente na etiqueta). |
| **Wire** (`⚡`) | `Generic` | Cabo físico oculto conectado ao Pill Transmitter (Wire Display: Hidden). Garante sincronização sequencial perfeita para o Wallacei/Galapagos mantendo a estética limpa. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Value** (`V`) | `Generic` | Valor ou árvore de dados recebida do canal sem fios Pill. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
