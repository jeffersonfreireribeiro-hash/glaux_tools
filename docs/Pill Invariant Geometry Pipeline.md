---
name: "Pill Invariant Geometry Pipeline"
nickname: ""
category: "Buraqueira Tools"
subcategory: ""
class: ""
file: "PillLayerPipeline_Component.cs"
plugin: "Buraqueira Tools"
status: "Compilado / Ativo"
tags: [componente, grasshopper, buraqueira_tools, ]
---

# 🧩 Pill Invariant Geometry Pipeline (``)

**Categoria:** `Buraqueira Tools` ➔ ``  
**Arquivo C#:** `PillLayerPipeline_Component.cs`  
**Classe:** ``

---

## 📝 Descrição


---

## 📥 Entradas (Inputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **LayerFilter** (`LF`) | `Text` | Caminho hierarquico completo da camada (ex: '01_ARQ::PAREDES::CONCRETO') ou padrao com wildcard ('*PAREDE*', 'ACU::*'). Se vazio, utiliza a selecao do menu ou da janela visual. |
| **TypeFilter** (`TF`) | `Integer` | Filtro de tipo geometrico:\n0 = Qualquer\n1 = Brep / Superficie\n2 = Malha (Mesh)\n3 = Curva\n4 = Ponto\n5 = SubD\n6 = Texto / Anotacao\n7 = Extrusao |
| **SortMode** (`SM`) | `Integer` | Criterio de ordenacao canonica:\n0 = Espacial 3D (X -> Y -> Z do Bounding Box) [Padrao Wallacei]\n1 = Espacial 2D (X -> Y no plano XY)\n2 = Nome Natural do Objeto (ex: P1, P2, P10)\n3 = Nome do Objeto -> GUID\n4 = GUID Estavel |
| **PillKey** (`K`) | `Text` | Chave opcional para publicar automaticamente no barramento sem fio PillHub (ex: '[GEO] Paredes_Auditorio'). |
| **Tolerance** (`T`) | `Number` | Tolerancia de comparacao espacial em coordenadas (padrao: 0.001 m). |

---

## 📤 Saídas (Outputs)

| Parâmetro | Tipo | Descrição |
| :--- | :---: | :--- |
| **Geometry** (`G`) | `Generic` | Geometrias capturadas e ordenadas deterministicamente. |
| **Names** (`N`) | `Text` | Nomes dos objetos no Rhino (ou nome da camada se sem nome). |
| **IDs** (`ID`) | `Text` | GUIDs persistentes dos objetos no documento do Rhino. |
| **UserText** (`TXT`) | `Text` | Metadados e User Strings dos objetos (chave=valor). |
| **BBoxes** (`BB`) | `Box` | Caixas delimitadoras (Bounding Boxes) das geometrias. |
| **Summary** (`S`) | `Text` | Diagnostico detalhado: camadas resolvidas, contagem e criterio de ordenacao. |

---

## 💡 Notas de Implementação & Uso
* **Compilação:** Mapeado na suíte `Buraqueira Tools`.
* **Testes recomendados:** Validar no Grasshopper com árvores de dados (`DataTree`) e verificar estabilidade do solver.
