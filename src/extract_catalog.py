import os
import re
import json

PROJECT_DIR = os.path.dirname(os.path.abspath(__file__))
files = [f for f in os.listdir(PROJECT_DIR) if f.endswith(".cs") and "Component" in f]

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
        if len(str_matches) >= 5:
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
        in_content = in_block.group(1)
        param_matches = re.findall(r'pManager\.Add([A-Za-z0-9]+)\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"((?:[^"\\]|\\.)*)"', in_content)
        for ptype, pname, pnick, pdesc in param_matches:
            inputs.append({
                "Name": pname,
                "Nick": pnick,
                "Type": ptype.replace("Parameter", ""),
                "Desc": pdesc.replace(r'\"', '"')
            })

    # Outputs
    outputs = []
    out_block = re.search(r'RegisterOutputParams\s*\([^)]*\)\s*\{(.*?)(?:protected override|#region|public override|\n\s{8}\})', content, re.DOTALL)
    if out_block:
        out_content = out_block.group(1)
        param_matches = re.findall(r'pManager\.Add([A-Za-z0-9]+)\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"((?:[^"\\]|\\.)*)"', out_content)
        for ptype, pname, pnick, pdesc in param_matches:
            outputs.append({
                "Name": pname,
                "Nick": pnick,
                "Type": ptype.replace("Parameter", ""),
                "Desc": pdesc.replace(r'\"', '"')
            })

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

