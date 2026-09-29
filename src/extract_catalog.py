import os
import re
import json

PROJECT_DIR = os.path.dirname(os.path.abspath(__file__))
files = [f for f in os.listdir(PROJECT_DIR) if f.endswith(".cs") and "Component" in f]

# Descrições compartilhadas declaradas como constantes (ex.: StoreInput.InputDescription)
SHARED_CONSTANTS = {}
for root, _, names in os.walk(os.path.join(PROJECT_DIR, "Core")):
    for n in names:
        if not n.endswith(".cs"):
            continue
        with open(os.path.join(root, n), "r", encoding="utf-8", errors="replace") as f:
            src = f.read()
        for m in re.finditer(r'class\s+(\w+)(.*?)(?=\n\s*(?:internal|public)\s+(?:static\s+|sealed\s+)*class\s|\Z)', src, re.DOTALL):
            for c in re.finditer(r'const\s+string\s+(\w+)\s*=\s*((?:\s*"(?:[^"\\]|\\.)*"\s*\+?)+);', m.group(2)):
                value = "".join(re.findall(r'"((?:[^"\\]|\\.)*)"', c.group(2)))
                SHARED_CONSTANTS[m.group(1) + "." + c.group(1)] = value

PARAM_RE = r'pManager\.Add([A-Za-z0-9]+)\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(?:"((?:[^"\\]|\\.)*)"|([A-Za-z_][A-Za-z0-9_.]*))'


def read_params(block):
    params = []
    for ptype, pname, pnick, pdesc, pconst in re.findall(PARAM_RE, block):
        desc = pdesc if not pconst else SHARED_CONSTANTS.get(pconst, "")
        params.append({
            "Name": pname,
            "Nick": pnick,
            "Type": ptype.replace("Parameter", ""),
            "Desc": desc.replace(r'\"', '"').replace(r'\n', ' ')
        })
    return params

catalog = []

for filename in files:
    filepath = os.path.join(PROJECT_DIR, filename)
    with open(filepath, "r", encoding="utf-8", errors="replace") as f:
        content = f.read()

    # Match base(...)
    ctor_match = re.search(r'base\s*\((.*?)\)\s*\{', content, re.DOTALL)
    name = filename.replace("_Component.cs", "")
    nick = ""
    desc = ""
    cat = "Buraqueira Tools"
    subcat = ""

    if ctor_match:
        args_text = ctor_match.group(1)
        str_matches = re.findall(r'"((?:[^"\\]|\\.)*)"', args_text)
        if re.search(r':\s*GlauxCapsuleComponent\b', content) and len(str_matches) >= 5:
            # GlauxCapsuleComponent(name, nick, description..., subCategory, capsuleCategory, color)
            name = str_matches[0]
            nick = str_matches[1]
            cat = "Glaux Tools"
            subcat = str_matches[-2]
            desc = "".join(str_matches[2:-2]).replace(r'\n', ' ').replace(r'\r', '').replace(r'\"', '"')
        elif len(str_matches) >= 5:
            name = str_matches[0]
            nick = str_matches[1]
            cat = str_matches[-2]
            subcat = str_matches[-1]
            desc = "".join(str_matches[2:-2]).replace(r'\n', ' ').replace(r'\r', '').replace(r'\"', '"')
        elif len(str_matches) >= 1:
            name = str_matches[0]

    # Inputs
    inputs = []
    in_block = re.search(r'RegisterInputParams\s*\([^)]*\)\s*\{(.*?)(?:protected override|#region|public override|\n\s{8}\})', content, re.DOTALL)
    if in_block:
        inputs = read_params(in_block.group(1))

    # Outputs
    outputs = []
    out_block = re.search(r'RegisterOutputParams\s*\([^)]*\)\s*\{(.*?)(?:protected override|#region|public override|\n\s{8}\})', content, re.DOTALL)
    if out_block:
        outputs = read_params(out_block.group(1))

    catalog.append({
        "File": filename,
        "Name": name,
        "NickName": nick,
        "Category": cat,
        "SubCategory": subcat,
        "Description": desc,
        "Inputs": inputs,
        "Outputs": outputs
    })

catalog.sort(key=lambda x: x["Name"])

catalog_json_path = os.path.join(PROJECT_DIR, "components_catalog.json")
with open(catalog_json_path, "w", encoding="utf-8-sig") as f:
    json.dump(catalog, f, indent=2, ensure_ascii=False)

print(f"Successfully generated catalog with {len(catalog)} components with perfect UTF-8 encoding!")

