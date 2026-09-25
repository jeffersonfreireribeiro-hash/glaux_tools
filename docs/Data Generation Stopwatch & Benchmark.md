---
name: "Data Generation Stopwatch & Benchmark"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "DataGenerationTimer_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Data Generation Stopwatch & Benchmark (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `DataGenerationTimer_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Árvore ou lista de dados/geometrias sendo geradas (pass-through transparente). |
| **Enable / Gate** (`G`) | `Boolean` | Habilita ou pausa a contagem do cronômetro (True = Ativo/Medindo, False = Pausado). |
| **Mode** (`M`) | `Integer` | Modo de Medição do Tempo:\n |
| **Reset** (`R`) | `Boolean` | Reseta o cronômetro, contador de ciclos e estatísticas acumuladas. |
| **Max History** (`Hist`) | `Integer` | Limite de registros no histórico para cálculo de médias e plotagem gráfica (Padrão: 100). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Data** (`D`) | `Generic` | Dados repassados intactos da entrada (Pass-Through transparente). |
| **Last Time (ms)** (`ms`) | `Number` | Duração da última geração de dados em milissegundos. |
| **Last Time (s)** (`s`) | `Number` | Duração da última geração de dados em segundos. |
| **Total Time (s)** (`Total`) | `Number` | Tempo total acumulado de geração de dados em segundos. |
| **Average Time (ms)** (`Avg`) | `Number` | Tempo médio por geração de dados em milissegundos. |
| **Cost per Item (ms)** (`Unit`) | `Number` | Custo médio de processamento por item gerado (ms/item). |
| **Throughput (items/s)** (`Rate`) | `Number` | Velocidade/taxa de geração de dados em itens por segundo. |
| **Cycle Count** (`N`) | `Integer` | Número total de gerações / ciclos computados. |
| **Time History (ms)** (`Hist`) | `Number` | Lista com os tempos das últimas N gerações em milissegundos (pronto para ChartLine). |
| **Report** (`Rep`) | `Text` | Relatório analítico completo com métricas de desempenho e diagnóstico. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
