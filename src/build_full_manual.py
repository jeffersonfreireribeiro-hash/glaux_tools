import os
import json
import html
import subprocess

PROJECT_DIR = os.path.dirname(os.path.abspath(__file__))
CATALOG_PATH = os.path.join(PROJECT_DIR, "components_catalog.json")
OUTPUT_HTML = os.path.join(PROJECT_DIR, "Buraqueira_Tools_Manual_Completo.html")
OUTPUT_PDF = os.path.join(PROJECT_DIR, "Buraqueira_Tools_Manual_Completo.pdf")

with open(CATALOG_PATH, "r", encoding="utf-8-sig") as f:
    catalog = json.load(f)

# Normalize empty subcategories
for item in catalog:
    subcat = (item.get("SubCategory") or "").strip()
    name = item.get("Name") or ""
    filename = item.get("File") or ""
    if not subcat:
        if name.startswith("Pill") or filename.startswith("Pill"):
            item["SubCategory"] = "Pills"
        elif "CSV" in filename or "CSV" in name:
            item["SubCategory"] = "I/O"
        elif "Timer" in filename or "Stopwatch" in name or "Trigger" in name or "Accumulator" in name or "Latch" in name or "Convergence" in name:
            item["SubCategory"] = "Automation"
        else:
            item["SubCategory"] = "Utilitários"

groups_order = [
    "Pills",
    "Tree",
    "Statistics",
    "Automation",
    "Evaluation",
    "Visual",
    "Transform",
    "I/O",
    "Utilitários"
]

group_titles = {
    "Pills": {
        "Title": "1. Ecossistema Pills (Barramento Sem Fios & Variantes)",
        "Desc": "Componentes de transmissão sem fios (PillHub), concentradores verticais de parâmetros, cofre de variantes com conectores estilo Galapagos e caches inteligentes para otimização acústica e paramétrica."
    },
    "Tree": {
        "Title": "2. Manipulação Avançada de Árvores de Dados (Data Trees)",
        "Desc": "Conjunto profissional de ferramentas para reestruturação topológica, filtragem por caminhos, alinhamento de árvores, particionamento e entrelaçamento dinâmico de dados."
    },
    "Statistics": {
        "Title": "3. Estatística, Probabilidade e Distribuições",
        "Desc": "Módulos de estatística descritiva, detecção de outliers (IQR/MAD), correlações, entropia de informação e distribuições contínuas/discretas (Beta, Binomial, Qui-Quadrado, Weibull, etc.)."
    },
    "Automation": {
        "Title": "4. Automação, Estados e Processos Iterativos",
        "Desc": "Ferramentas para criação de loops, acumuladores circulares (com exportação JSON), travas lógicas (State Latch) e monitoramento de convergência."
    },
    "Evaluation": {
        "Title": "5. Avaliação de Modelos e Metas de Projeto",
        "Desc": "Métricas de validação de erro (R², RMSE, MAE), cálculo de desvios relativos de metas de projeto e análise de dispersão geométrica."
    },
    "Visual": {
        "Title": "6. Visualização Gráfica no Canvas",
        "Desc": "Renderizadores de gráficos analíticos 2D diretamente no Canvas do Grasshopper (linhas, dispersão X/Y e mapas de calor espaciais)."
    },
    "Transform": {
        "Title": "7. Matemática em Massa e Agrupamento",
        "Desc": "Operações matemáticas de alto desempenho vetorizadas em árvores, somas condicionais (SumIf) e agrupamento rápido por similaridade."
    },
    "I/O": {
        "Title": "8. Importação e Exportação de Dados (I/O)",
        "Desc": "Leitores e exportadores de dados tabulares (CSV, texto delimitado e planilhas multi-abas) com tipagem automática e alta velocidade."
    },
    "Utilitários": {
        "Title": "9. Utilitários Gerais",
        "Desc": "Funções auxiliares e de suporte operacional ao Canvas do Grasshopper."
    }
}

html_parts = []

html_parts.append("""<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<title>Buraqueira Tools - Manual Completo & Dicionário de Componentes</title>
<style>
  @page {
    size: A4;
    margin: 16mm 14mm 16mm 14mm;
    @bottom-right {
      content: counter(page);
    }
  }

  body {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    color: #1e293b;
    line-height: 1.5;
    font-size: 10pt;
    margin: 0;
    padding: 0;
    background: #fff;
  }

  /* Capa */
  .cover {
    page-break-after: always;
    text-align: center;
    padding-top: 70px;
    padding-bottom: 50px;
  }
  .cover-badge {
    display: inline-block;
    background: #4338ca;
    color: white;
    font-weight: 800;
    font-size: 10.5pt;
    letter-spacing: 2px;
    padding: 6px 20px;
    border-radius: 20px;
    margin-bottom: 25px;
    text-transform: uppercase;
  }
  .cover h1 {
    font-size: 26pt;
    font-weight: 900;
    color: #0f172a;
    margin: 0 0 10px 0;
    letter-spacing: -0.5px;
  }
  .cover h2 {
    font-size: 13.5pt;
    font-weight: 600;
    color: #64748b;
    margin: 0 0 35px 0;
  }
  .cover-box {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 12px;
    padding: 22px;
    max-width: 540px;
    margin: 0 auto 40px auto;
    text-align: left;
    font-size: 9.5pt;
    color: #334155;
  }
  .cover-box strong.box-title {
    font-size: 10.5pt;
    color: #0f172a;
    display: block;
    margin-bottom: 8px;
  }
  .cover-box ul {
    margin: 8px 0 0 18px;
    padding: 0;
  }
  .cover-box li {
    margin-bottom: 6px;
  }
  .cover-meta {
    font-size: 8.5pt;
    color: #94a3b8;
  }

  /* Sumário */
  .toc {
    page-break-after: always;
    padding-top: 15px;
  }
  .toc h2 {
    font-size: 17pt;
    border-bottom: 2px solid #e2e8f0;
    padding-bottom: 8px;
    color: #0f172a;
    margin-bottom: 20px;
  }
  .toc-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 15px;
    margin-bottom: 25px;
  }
  .toc-card {
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 12px 14px;
  }
  .toc-card h3 {
    margin: 0 0 6px 0;
    font-size: 10pt;
    color: #4338ca;
  }
  .toc-card p {
    margin: 0;
    font-size: 8.5pt;
    color: #64748b;
  }

  /* Seções de Capítulos */
  .chapter-header {
    page-break-before: always;
    margin-top: 25px;
    margin-bottom: 20px;
    border-bottom: 2.5px solid #4338ca;
    padding-bottom: 8px;
  }
  .chapter-header h1 {
    font-size: 16pt;
    color: #0f172a;
    margin: 0 0 6px 0;
  }
  .chapter-header p {
    font-size: 9.5pt;
    color: #64748b;
    margin: 0;
  }

  /* Cartão de Componente */
  .component-card {
    background: #ffffff;
    border: 1px solid #cbd5e1;
    border-radius: 8px;
    padding: 14px 16px;
    margin-bottom: 16px;
    page-break-inside: avoid;
  }
  .comp-title-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid #f1f5f9;
    padding-bottom: 8px;
    margin-bottom: 10px;
  }
  .comp-name {
    font-size: 11.5pt;
    font-weight: 800;
    color: #1e293b;
  }
  .comp-nick {
    font-size: 8.5pt;
    font-family: monospace;
    background: #f1f5f9;
    padding: 2px 6px;
    border-radius: 4px;
    color: #475569;
  }
  .comp-desc {
    font-size: 9.2pt;
    color: #334155;
    margin-bottom: 12px;
    line-height: 1.45;
  }

  /* Tabelas de Parâmetros */
  table.params-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 8.4pt;
    margin-top: 6px;
  }
  table.params-table th, table.params-table td {
    padding: 5px 8px;
    border: 1px solid #e2e8f0;
    text-align: left;
  }
  table.params-table th {
    background: #f8fafc;
    font-weight: 700;
    color: #475569;
  }
  .io-tag {
    font-weight: 800;
    font-size: 7.5pt;
    padding: 1px 5px;
    border-radius: 3px;
    display: inline-block;
  }
  .io-in { background: #e0e7ff; color: #3730a3; }
  .io-out { background: #dcfce7; color: #166534; }

  .param-name {
    font-weight: 700;
    color: #0f172a;
  }
  .param-type {
    font-family: monospace;
    font-size: 7.8pt;
    color: #64748b;
  }

  .badge-cat {
    display: inline-block;
    background: #e2e8f0;
    color: #334155;
    font-weight: 700;
    font-size: 7.5pt;
    padding: 2px 6px;
    border-radius: 4px;
    margin-left: 6px;
  }

  .footer-bar {
    text-align: center;
    font-size: 8pt;
    color: #94a3b8;
    margin-top: 25px;
    padding-top: 10px;
    border-top: 1px solid #e2e8f0;
  }
</style>
</head>
<body>

<!-- CAPA -->
<div class="cover">
  <div class="cover-badge">Buraqueira Tools • DOCUMENTAÇÃO TÉCNICA COMPLETA</div>
  <h1>ENCICLOPÉDIA DE COMPONENTES &amp; PILHAS</h1>
  <h2>Manual Oficial de Todos os Módulos do Buraqueira Tools para Grasshopper</h2>

  <div class="cover-box">
    <strong class="box-title">Visão Geral do Conjunto Completo:</strong>
    <ul>
      <li><strong>""" + str(len(catalog)) + """ Componentes Especializados:</strong> Catálogo técnico integral com parâmetros de entrada (Inputs), saídas (Outputs) e descrições de funcionamento.</li>
      <li><strong>Ecossistema Pills:</strong> Transmissão de dados sem fios (PillHub), concentradores verticais de parâmetros, cofre de variantes com cabos estilo Galapagos e caches inteligentes.</li>
      <li><strong>Data Trees Avançadas:</strong> Álgebra e topologia de árvores, alinhamento, filtragem condicional e entrelaçamento de ramos.</li>
      <li><strong>Estatística &amp; Probabilidade:</strong> Média, variância, correlação, assimetria, curtose, tabelas de frequência e 7 distribuições probabilísticas.</li>
      <li><strong>Automação &amp; Gráficos:</strong> Acumuladores iterativos de dados, temporizadores e gráficos direto no Canvas.</li>
    </ul>
  </div>

  <div class="cover-meta">
    Buraqueira Tools • Desenvolvido para Acústica Arquitetônica &amp; Modelagem Paramétrica • Rhino 7 e Rhino 8
  </div>
</div>

<!-- SUMÁRIO -->
<div class="toc">
  <h2>Estrutura dos Módulos</h2>
  <div class="toc-grid">
""")

# TOC Cards
for g in groups_order:
    if g in group_titles:
        info = group_titles[g]
        count = len([x for x in catalog if x.get("SubCategory") == g])
        if count > 0:
            html_parts.append(f"""    <div class="toc-card">
      <h3>{html.escape(info["Title"])} <span class="badge-cat">{count} Pilhas</span></h3>
      <p>{html.escape(info["Desc"])}</p>
    </div>
""")

html_parts.append("""  </div>
</div>
""")

# CHAPTERS
for g in groups_order:
    items = [x for x in catalog if x.get("SubCategory") == g]
    items.sort(key=lambda x: x.get("Name", ""))
    if not items:
        continue

    info = group_titles.get(g, {})
    title = info.get("Title", g)
    desc = info.get("Desc", "")

    html_parts.append(f"""<div class="chapter-header">
  <h1>{html.escape(title)}</h1>
  <p>{html.escape(desc)} (Total: {len(items)} componentes)</p>
</div>
""")

    for c in items:
        name = c.get("Name") or ""
        subcat = c.get("SubCategory") or ""
        nick = c.get("NickName") or ""
        comp_desc = c.get("Description") or ""
        inputs = c.get("Inputs") or []
        outputs = c.get("Outputs") or []

        html_parts.append(f"""<div class="component-card">
  <div class="comp-title-bar">
    <div>
      <span class="comp-name">{html.escape(name)}</span>
      <span class="badge-cat">{html.escape(subcat)}</span>
    </div>
    <span class="comp-nick">{html.escape(nick)}</span>
  </div>
  <div class="comp-desc">{html.escape(comp_desc)}</div>
""")

        if inputs or outputs:
            html_parts.append("""  <table class="params-table">
    <thead>
      <tr>
        <th style="width: 10%;">Direção</th>
        <th style="width: 25%;">Nome (Sigla)</th>
        <th style="width: 20%;">Tipo</th>
        <th>Descrição</th>
      </tr>
    </thead>
    <tbody>
""")

            for inp in inputs:
                p_name = inp.get("Name") or ""
                p_nick = inp.get("Nick") or ""
                p_type = inp.get("Type") or ""
                p_desc = inp.get("Desc") or ""
                html_parts.append(f"""      <tr>
        <td><span class="io-tag io-in">INPUT</span></td>
        <td><span class="param-name">{html.escape(p_name)}</span> <span class="param-type">({html.escape(p_nick)})</span></td>
        <td><span class="param-type">{html.escape(p_type)}</span></td>
        <td>{html.escape(p_desc)}</td>
      </tr>
""")

            for out in outputs:
                p_name = out.get("Name") or ""
                p_nick = out.get("Nick") or ""
                p_type = out.get("Type") or ""
                p_desc = out.get("Desc") or ""
                html_parts.append(f"""      <tr>
        <td><span class="io-tag io-out">OUTPUT</span></td>
        <td><span class="param-name">{html.escape(p_name)}</span> <span class="param-type">({html.escape(p_nick)})</span></td>
        <td><span class="param-type">{html.escape(p_type)}</span></td>
        <td>{html.escape(p_desc)}</td>
      </tr>
""")

            html_parts.append("""    </tbody>
  </table>
""")

        html_parts.append("</div>\n")

html_parts.append("""<div class="footer-bar">
  Buraqueira Tools • Manual Completo Gerado Automaticamente • Buraqueiras/src/Tools
</div>
</body>
</html>
""")

full_html = "".join(html_parts)

with open(OUTPUT_HTML, "w", encoding="utf-8") as f:
    f.write(full_html)

print(f"HTML gerado com sucesso: {OUTPUT_HTML} ({len(full_html)} chars)")

# Browser search for PDF generation
candidates = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe"
]

browser = None
for b in candidates:
    if os.path.exists(b):
        browser = b
        break

if browser:
    html_uri = "file:///" + OUTPUT_HTML.replace("\\", "/")
    cmd = [
        browser,
        "--headless",
        "--disable-gpu",
        "--no-pdf-header-footer",
        "--run-all-compositor-stages-before-draw",
        "--virtual-time-budget=4000",
        f"--print-to-pdf={OUTPUT_PDF}",
        html_uri
    ]
    print(f"Compilando PDF via {browser}...")
    res = subprocess.run(cmd, capture_output=True, text=True)
    if os.path.exists(OUTPUT_PDF):
        size_kb = round(os.path.getsize(OUTPUT_PDF) / 1024, 1)
        print(f"PDF gerado com sucesso: {OUTPUT_PDF} ({size_kb} KB)")
    else:
        print("Erro ao gerar PDF:", res.stderr)
else:
    print("Nenhum navegador encontrado para compilar o PDF.")


