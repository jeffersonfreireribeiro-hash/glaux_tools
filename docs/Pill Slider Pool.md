---
name: "Pill Slider Pool"
nickname: "SliderPool"
category: "Glaux Tools"
subcategory: "Pills"
class: "PillSliderPool_Component"
file: "PillSliderPool_Component.cs"
plugin: "Glaux Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, glaux_tools, pills, sliders, hub]
---

# 🧩 Pill Slider Pool (`SliderPool`)

**Categoria:** `Glaux Tools` ➔ `Pills`  
**Arquivo C#:** `PillSliderPool_Component.cs`  
**Classe:** `PillSliderPool_Component`  

---

## 📝 Descrição

O **Pill Slider Pool** é o centro de controle e consolidação de parâmetros interativos da família Glaux. Ele reúne múltiplos sliders em uma única interface visual de alta densidade no Canvas, com suporte completo a diferentes tipos de dados:
* `float`: Números decimais com precisão configurável.
* `int`: Números inteiros com arredondamento automático.
* `bool`: Alternadores (switches) liga/desliga para ativar/desativar rotinas, modelos e solvers.
* `btn`: Botões de disparo de pulso momentâneo (Trigger) com auto-reset para `false`.
* `str`: Textos e caixas de seleção.
* `dom`: Intervalos e domínios numéricos bidirecionais com manípulo duplo.
* `list`: Menus dropdown suspensos com opções pré-definidas.

Cada slider é publicado automaticamente como um canal sem fios no **PillHub**, permitindo que componentes como [[Pill Receiver]] e [[Pill Hook]] leiam os valores individualmente sem acoplar a árvore de dependência (DAG) de outros sliders.

---

## 📥 Entradas (Inputs)

| Parâmetro | Nick | Acesso | Tipo | Descrição |
| :--- | :---: | :---: | :---: | :--- |
| **Def** | `C` | `List` | `Text` | Definições opcionais via texto/Panel para configurar sliders dinamicamente (ex: `[GEO] Raio = 10 (0..50) [float]`, `[SIM] Ativar = True [bool] [r]`). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Nick | Acesso | Tipo | Descrição |
| :--- | :---: | :---: | :---: | :--- |
| **Values** | `V` | `List` | `Number` | Lista ordenada com os valores numéricos atuais de todos os sliders (compatível com Gene Pool do Galapagos/Wallacei). |
| **Keys** | `K` | `List` | `Text` | Lista com os nomes e chaves canônicas de todos os parâmetros publicados no PillHub. |
| **Tree** | `T` | `Tree` | `Generic` | Árvore de dados (`GH_Structure`) com cada parâmetro em um ramo estruturado `{i}` contendo o valor tipado. |

---

## 🔗 Conexões & Compatibilidade de Pilhas (Pills)

```mermaid
flowchart TD
    DefInput["Panel de Configuração (Def)"] -->|Text Lines| Pool["[[Pill Slider Pool]]"]
    
    subgraph Modo Sem Fio (Recomendado - 100% Desacoplado)
        Pool -.->|PillHub Channel| Rx1["[[Pill Receiver]] (SIM_Ativar)"]
        Pool -.->|PillHub Channel| Rx2["[[Pill Receiver]] (GEO_Raio)"]
        Rx1 -->|Active| Solver["[[Acoustic Scene]] / Solver"]
        Rx2 -->|Radius| Geo["Geometria"]
    end

    subgraph Modo Cabeado (DAG Acoplada)
        Pool -->|Values (V)| Unpack["List Item / Unpack"]
        Unpack -->|Tudo Expira Junto| Solver
    end
```

### ⚡ Diagnóstico de Latência: Modo Cabeado vs Modo Sem Fio
* **Modo Cabeado (`Values` / `Tree` conectados):** Se o usuário conectar as saídas `Values` ou `Tree` na definição, qualquer alteração em qualquer slider (inclusive em um botão ou toggle de "Ativar Modelo") marca a saída inteira como expirada no Grasshopper. Isso força **todo o ecossistema a jusante** (geometria, malhas, simulação) a recalcular do zero.
* **Modo Sem Fio (`Pill Receiver` / `Pill Hook`):** O `PillHub` desacopla cada slider em um canal independente na memória. Alterar `SIM_Ativar` invalida exclusivamente o `Pill Receiver` conectado à entrada `Active` do solver, sem tocar nem re-executar as malhas, a geometria ou os outros parâmetros da cena!

---

## 🚀 Otimizações de Desempenho Implementadas

1. **Cache de Publicação Seletiva (`_lastPublishedValues`):** O componente monitora o estado anterior de cada slider. Em `SolveInstance`, apenas os sliders que realmente sofreram alteração física são republicados no barramento `PillHub`.
2. **Debounce e Throttling no Arraste (50 ms):** Durante a manipulação contínua do cursor sobre os sliders, a tela atualiza a 60 FPS (`Invalidate`), enquanto os eventos de expiração de solução são limitados a 50 ms, com finalização precisa no evento `MouseUp`.
3. **Preservação de Estado em `SyncParsedSliders`:** Sliders booleanos (`BoolValue`), inteiros (`IntValue`) e de início de domínio (`DomainStart`) mantêm seus valores ao reavaliar a entrada `Def`, prevenindo resets indesejados.
4. **Isolamento de Componentes Pill em `HasConnectedOutputs`:** Componentes que operam via PillHub (`PillHook_Component`, `PillReceiver_Component`) não são considerados saídas DAG externas acopladas, prevenindo expirações em cascata.
