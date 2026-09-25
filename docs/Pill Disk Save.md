---
name: "Pill Disk Save"
nickname: "PillSave"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillDiskSave_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Disk Save (`PillSave`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillDiskSave_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Salva qualquer dado ou árvore de dados do Grasshopper em disco (.pilldata) com preservação total de tipos e topologia (geometrias, listas, números, matrizes, simulações). Suporta acionamento por botão/gatilho.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Qualquer dado, lista ou árvore de dados do Grasshopper a ser persistido em disco. |
| **Key** (`K`) | `Text` | Nome da chave / identificador do arquivo (ex: 'Geometria_Paredes' ou 'Resultados_Wallacei'). |
| **Directory** (`DIR`) | `Text` | Diretório no disco. Se omitido, utiliza a subpasta 'PillVault' junto ao arquivo .gh atual. |
| **Save** (`S`) | `Boolean` | Gatilho para salvar em disco (True = efetua gravação). Conecte um Botão ou Toggle. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **FilePath** (`F`) | `Text` | Caminho completo do arquivo gravado no disco. |
| **Success** (`OK`) | `Boolean` | True se a gravação no disco foi concluída com sucesso. |
| **Summary** (`SUM`) | `Text` | Resumo dos dados salvos (ramificações, contagem de itens, tamanho e data). |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
