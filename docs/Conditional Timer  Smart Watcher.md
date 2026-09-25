---
name: "Conditional Timer / Smart Watcher"
nickname: "SmartTimer"
category: "Buraqueira Tools"
subcategory: "Automation"
class: ""
file: "ConditionalTimer_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, automation]
---

# 🧩 Conditional Timer / Smart Watcher (`SmartTimer`)

**Categoria:** `Buraqueira Tools` ➔ `Automation`  
**Arquivo C#:** `ConditionalTimer_Component.cs`  
**Classe:** ``

---

## 📝 Descrição
Temporizador inteligente e sentinela condicional (Watcher). Executa ciclos ou gravações apenas quando critérios forem atingidos, com controle estrito de disparos máximos (One-Shot, Burst ou Polling) para prevenir loops infinitos e travamentos.

---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore de dados opcional a ser liberada durante os pulsos de execução. |
| **Arm / Enable** (`E`) | `Boolean` | Habilita ou desarma o temporizador/watcher (True = Armado/Pronto, False = Desarmado/Pausa). |
| **Condition / Trigger** (`C`) | `Boolean` | Critério booleano de ativação (quando True, aciona a contagem ou execução). |
| **Interval Ms** (`ms`) | `Integer` | Intervalo entre pulsos ou checagens em milissegundos (padrão: 100 ms, mínimo: 10 ms). |
| **Max Ticks** (`Max`) | `Integer` | Limite máximo de pulsos por ativação (1 = One-Shot / Disparo Único; N = Rajada de N passos; 0 = Sem limite estrito enquanto a condição for True). Previne loops infinitos. |
| **Mode** (`M`) | `Integer` | Modo de Operação:\n0 = One-Shot (Dispara 1 vez na transição False->True e desarma)\n1 = Burst Mode (Dispara N passos com intervalo 'ms' enquanto True)\n2 = Polling Watcher (Checa a cada 'ms'; no momento em que a condição for True, dispara e desarma)\n3 = Gated Stream (Timer contínuo enquanto True, com trava de segurança em Max) |
| **Reset / Re-Arm** (`R`) | `Boolean` | Reseta o contador de iterações, cancela agendamentos pendentes e rearma o temporizador. |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Output Data** (`O`) | `Generic` | Dados liberados durante os pulsos de execução ativos. |
| **Tick Pulse** (`Tick`) | `Boolean` | Pulso booleano emitido (True) no ciclo/disparo atual (ideal para acionar gravadores e solvers). |
| **Tick Count** (`Count`) | `Integer` | Contagem de pulsos executados no ciclo atual. |
| **Is Active** (`Active`) | `Boolean` | True se o temporizador estiver ativamente executando ciclos. |
| **Finished / Done** (`Done`) | `Boolean` | True quando o ciclo de disparos for concluído e o componente desarmar com sucesso. |
| **Status** (`Status`) | `Text` | Diagnóstico e estado em tempo real do temporizador. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
