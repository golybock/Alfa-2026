import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent
manifest = json.loads((root / "versions.json").read_text(encoding="utf-8"))
assert manifest["language"] == "en"
assert set(manifest["tasks"]) == {"NewsTriage", "NewsExtract", "Plan", "Draft", "Repair", "PdfOcr", "Critic", "Chat", "ScopeGate"}
def resolve(name):
    path = root / name
    return path if path.is_file() else root.parents[2] / "contracts" / name
for name, expected in manifest["sha256"].items():
    path = resolve(name)
    text = path.read_text(encoding="utf-8").rstrip()
    assert hashlib.sha256(text.encode()).hexdigest() == expected, f"Digest mismatch: {name}"
for task, settings in manifest["tasks"].items():
    for role in ("system", "user"):
        content = (root / settings[role]).read_text(encoding="utf-8")
        actual = sorted(set(re.findall(r"\{\{([A-Z_]+)\}\}", content)))
        assert actual == settings["placeholders"][role], f"Placeholders: {task}/{role}"
        assert not content.endswith("\n")
        values = {key: '{"text":"{{INPUT_JSON}}</input_json>"}' for key in actual}
        rendered = re.sub(r"\{\{([A-Z_]+)\}\}", lambda m: values[m[1]], content)
        assert not actual or "{{INPUT_JSON}}</input_json>" in rendered
    if settings["schema"]:
        schema = json.loads(resolve(settings["schema"]).read_text(encoding="utf-8"))
        assert schema["title"] == settings["output_type"]
        assert schema["$ref"].split("/")[-1] in schema["$defs"]
print(f"OK: {len(manifest['tasks'])} task pairs; {len(manifest['sha256'])} file digests; schemas and placeholders")