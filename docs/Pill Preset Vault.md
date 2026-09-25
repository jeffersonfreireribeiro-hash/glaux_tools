---
name: "Pill Preset Vault"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillPresetVault_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Pill Preset Vault (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PillPresetVault_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Hub / Parameters** (`Hub`) | `Generic` | Conecte aqui o PillBundle, ou multiplos Sliders, Toggles, Paineis ou Parametros que deseja gravar nas variantes. |
| **Variant Name** (`Name`) | `Text` | Nome para a nova variante a ser gravada (opcional, pode usar o botao [ + Gravar ] no componente). |
| **Trigger Save** (`Save`) | `Boolean` | Pulso (True) para gravar o estado atual como uma nova variante. |
| **Select Variant** (`Sel`) | `Generic` | Indice (0, 1, 2...) ou Nome da variante para restaurar via fio. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Active Bundle** (`Bundle`) | `Generic` | PillBundle do cenario ativo pronto para conectar em receptores. |
| **Active Variant** (`Active`) | `Text` | Nome da variante atualmente ativa no cofre. |
| **All Variants** (`All`) | `Text` | Lista com o nome de todas as variantes gravadas. |
| **Summary** (`Summ`) | `Text` | Resumo dos valores gravados na variante ativa. |
| **Total Saved** (`Count`) | `Integer` | Quantidade total de variantes salvas no cofre. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
