---
name: "Pill Time Impulse / Clock Pulse"
nickname: "PillPulse"
category: "Buraqueira Tools"
subcategory: "Pills"
class: "PillPulseTimer_Component"
file: "PillPulseTimer_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, pills, timer, automacao]
---

# 🧩 Pill Time Impulse / Clock Pulse (`PillPulse`)

**Categoria:** `Buraqueira Tools` ➔ `Pills`  
**Arquivo C#:** `PillPulseTimer_Component.cs`  
**Classe:** `PillPulseTimer_Component`  
**GUID:** `B7110022-E1EF-4000-8000-000000000022`

---

## 📝 Descrição

Pilha geradora de impulsos periódicos temporizados estilo metrônomo / clock pulse para o ecossistema Buraqueira Tools.
- Emite um sinal booleano `True` a cada intervalo programado de tempo com comportamento idêntico a um **Botão instantâneo** do Grasshopper.
- Retorna automaticamente a `False` logo após o pulso (duração configurável em milissegundos, padrão: 60 ms), impedindo que o fluxo fique eternamente preso em `True`.
- Possui controles interativos embutidos diretamente no canvas:
  - Botão **Play / Pause** para ligar/pausar a contagem.
  - Botão **Trigger Now** para forçar um impulso avulso manual imediatamente.
- Suporta limite de disparos (parar após $N$ ciclos), contagem de ciclos executados e contagem regressiva para o próximo pulso.

---

## 📥 Entradas (Inputs)

| Parâmetro | Nick | Tipo | Descrição | Padrão |
| :--- | :---: | :---: | :--- | :---: |
| **Interval** | `Intv` | `Number` | Intervalo de tempo entre impulsos em segundos (ex: 1.0, 0.5, 5.0, 60.0). | `1.0s` |
| **Active** | `On` | `Boolean` | Ativa ou pausa a geração periódica de impulsos (`True` = Ativo, `False` = Pausado). | `True` |
| **Trigger Now** | `Trig` | `Boolean` | Disparo manual avulso: envie `True` (ou conecte um Botão GH) para forçar um impulso. | `False` |
| **Reset** | `Rst` | `Boolean` | Reseta a contagem de ciclos ($N=0$) e reinicia o cronômetro. | `False` |
| **Limit** | `Lim` | `Integer` | Limite máximo de disparos (0 = contínuo sem limite; $N$ = para após $N$ ciclos). | `0` |
| **Pulse Duration**| `Dur` | `Integer` | Duração do sinal `True` em milissegundos antes do retorno a `False`. | `60 ms` |

---

## 📤 Saídas (Outputs)

| Parâmetro | Nick | Tipo | Descrição |
| :--- | :---: | :---: | :--- |
| **Impulse** | `Pulse` | `Boolean` | Sinal de pulso instantâneo (`True` transitório por `Dur` ms, depois `False`). |
| **Cycle Count** | `N` | `Integer` | Número total de impulsos disparados desde o início ou último reset. |
| **Next In** | `Sec` | `Number` | Tempo restante em segundos até o próximo pulso periódico. |
| **Running** | `Run` | `Boolean` | Indica se o temporizador está atualmente ativo e em contagem. |

---

## 💡 Aplicações Típicas
1. **Animações e Simulações Incrementais:** Alimentar loops e acumuladores iterativos (`IterativeAccumulator`).
2. **Atualização Periódica de Dados:** Disparar leitura automática de arquivos de disco (`PillDiskLoad`, `CSVImport`) a cada $X$ segundos.
3. **Automação de Otimização:** Avançar passos de solucionadores ou registradores de histórico paramétrico sem intervenção manual.
