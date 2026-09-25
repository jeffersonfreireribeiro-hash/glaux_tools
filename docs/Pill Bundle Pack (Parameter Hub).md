---
name: "Pill Bundle Pack (Parameter Hub)"
nickname: "PillPack"
category: "Buraqueira Tools"
subcategory: "Pills"
class: ""
file: "PillBundlePack_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills]
---

# 🧩 Pill Bundle Pack (Parameter Hub) (`PillPack`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillBundlePack_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Empacota múltiplos parâmetros, listas ou árvores nomeadas em um único pacote estruturado (PillBundle / Hub Central). Permite puxar automaticamente canais por nome de grupo/categoria (ex: 'ACU', 'GEO', 'ALL') ou empacotar valores manuais. Reduz a fiação complexa a uma única linha de transmissão e suporta exportação JSON.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Keys** (`K`) | `Text` | Lista de chaves de parâmetros (ex: 'ACU_T60_Alvo [s]') OU nomes de grupos/categorias (ex: 'ACU', 'GEO', 'MAT', 'ALL'). Se for um grupo, todas as variáveis ativas dessa categoria são automaticamente colhidas do barramento PillHub. |
| **Values** (`V`) | `Generic` | Valores manuais OPCIONAIS. DEIXE DESCONECTADO para colher automaticamente dos Transmitters do Canvas (via Wires ⚡ e PillHub). Conecte aqui apenas se quiser forçar valores manuais sem transmissores. |
| **Namespace** (`NS`) | `Text` | Namespace ou prefixo de escopo opcional (ex: 'SALA_01' ou 'CONFIG'). |
| **Wires** (`⚡`) | `Generic` | Cabos físicos ocultos automáticos (Wire Display: Hidden) dos Transmitters correspondentes para sincronização no Wallacei. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Bundle** (`B`) | `Generic` | Objeto PillBundle estruturado contendo todos os parâmetros empacotados. |
| **JSON** (`J`) | `Text` | String JSON serializada pronta para exportar para arquivo ou transmitir entre definições. |
| **Summary** (`S`) | `Text` | Resumo detalhado dos parâmetros empacotados. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
