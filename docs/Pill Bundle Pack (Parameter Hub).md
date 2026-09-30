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
| **Keys** (`K`) | `Text` | Lista de chaves de parâmetros (ex: 'ACU_T60_Alvo [s]') OU nomes de grupos/categorias (ex: 'ACU', 'GEO', 'MAT', 'ALL'). Se for um grupo, todas as variáveis ativas dessa categoria são automaticamente colhidas do barramento PillHub. Opcional quando há Wires ⚡; um transmissor ligado nos Wires entra uma vez só, mesmo que a chave também o encontre. |
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

## 🔀 Keys e Wires ⚡ juntos

As duas entradas podem apontar para os mesmos transmissores. É o que acontece quando **Conectar Cabos Ocultos** liga os fios aos transmissores que as chaves já encontram.

- **Entram primeiro os Wires ⚡:** uma entrada por fio.
- **Depois entram as Keys:** colhem do barramento só os canais cujo transmissor **não** está ligado nos Wires. A identidade é a instância do transmissor, não o nome da entrada.
- **O nome da entrada é o mesmo nos dois caminhos:** apelidos genéricos (`Pill Transmitter`, `PillTx`, `Transmitter`, `Tx`) não viram sufixo. Ligar ou desligar os fios não renomeia as entradas.
- **Keys é opcional:** só Wires ⚡ basta. Arquivos antigos, que gravaram Keys como obrigatória, passam a aceitar a entrada vazia ao abrir.
- **O Summary conta** os canais das Keys que já vieram pelos Wires ("não duplicados").

**Correção da v1.2.1:** até a v1.2.0, com transmissores de apelido `Pill Transmitter` ou `Tx`, cada transmissor entrava duas vezes, sem aviso. Por exemplo, `SRF_Paredes` pelo fio e `SRF_Paredes_Pill Transmitter` pela chave. Num projeto real, 17 grupos de superfícies viravam 34 entradas (157 superfícies viravam 314). Além disso, um Pack sem chave e só com fios não calculava ("Input parameter K failed to collect data").

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Regras sem canvas** (nome da entrada, deduplicação): `PillBundlePacking.cs`, testadas em `tests/Glaux_Tools.Tests/PillBundlePackingTests.cs`.
* **Documento completo no Rhino 8** (transmissores, Pack e Unpack; Keys, Wires e os dois juntos; arquivo antigo): `tests/rhino/Test-PillBundlePack-KeysWires.ps1`.
