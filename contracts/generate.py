"""Build TS/JSON Schema mirrors of the records in AlfaNews.Contracts.

This supports only the declaration style used here, not arbitrary C# syntax.
Run: python contracts/generate.py. No network or third-party packages required.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parent
MODELS = ROOT.parent / 'backend' / 'AlfaNews.Contracts'
source = '\n'.join(path.read_text(encoding='utf-8')
                   for section in ('Domain', 'Api', 'Processing')
                   for path in sorted((MODELS / section).glob('*.cs')))


def snake(name):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', name).lower()


def parts(text):
    result, start, depth = [], 0, 0
    for i, c in enumerate(text):
        if c in '<(': depth += 1
        if c in '>)': depth -= 1
        if c == ',' and depth == 0:
            result.append(text[start:i].strip())
            start = i + 1
    result.append(text[start:].strip())
    return result


enums = {n: [snake(v.strip()) for v in values.split(',')] for n, values in re.findall(r'public enum (\w+)\s*{([^}]+)}', source)}
records = {}
for name, body in re.findall(r'public sealed record (\w+)\((.*?)\);', source, re.S):
    fields = []
    for field in parts(body):
        omitted = 'JsonIgnore' in field
        field = re.sub(r'\[property:.*?\]\s*', '', field, flags=re.S)
        decl, _, default = field.partition(' = ')
        typ, prop = decl.rsplit(' ', 1)
        fields.append((snake(prop), typ.strip(), bool(default), omitted))
    records[name] = fields


def ts_type(typ):
    if typ.endswith('?'): return ts_type(typ[:-1]) + ' | null'
    if typ.startswith('IReadOnlyList<'): return 'ReadonlyArray<' + ts_type(typ[14:-1]) + '>'
    if typ.startswith('IReadOnlyDictionary<'):
        key, val = parts(typ[20:-1])
        assert key == 'string'
        return 'Readonly<Record<string, ' + ts_type(val) + '>>'
    return {'string':'string', 'decimal':'number', 'int':'number', 'long':'number', 'bool':'boolean',
            'DateOnly':'string', 'DateTimeOffset':'string', 'JsonElement':'JsonValue'}.get(typ, typ)


def schema_type(typ):
    if typ.endswith('?'): return {'anyOf':[schema_type(typ[:-1]), {'type':'null'}]}
    if typ.startswith('IReadOnlyList<'): return {'type':'array', 'items':schema_type(typ[14:-1])}
    if typ.startswith('IReadOnlyDictionary<'):
        key, val = parts(typ[20:-1])
        assert key == 'string'
        return {'type':'object', 'additionalProperties':schema_type(val)}
    if typ in enums or typ in records: return {'$ref':'#/$defs/' + typ}
    if typ == 'JsonElement': return {}
    if typ == 'DateOnly': return {'type':'string', 'format':'date'}
    if typ == 'DateTimeOffset': return {'type':'string', 'format':'date-time'}
    return {'type':{'string':'string', 'decimal':'number', 'int':'integer', 'long':'integer', 'bool':'boolean'}[typ]}


definitions = {name: {'type':'string', 'enum':values} for name, values in enums.items()}
for name, fields in records.items():
    definitions[name] = {'type':'object', 'properties':{n:schema_type(t) for n,t,_,_ in fields},
                         'required':[n for n,_,optional,_ in fields if not optional], 'additionalProperties':False}


def reachable(name):
    seen = set()
    def visit(current):
        if current in seen: return
        seen.add(current)
        for ref in re.findall(r'"\$ref":\s*"#/\$defs/(\w+)"', json.dumps(definitions[current])):
            visit(ref)
    visit(name)
    return {key:definitions[key] for key in sorted(seen)}


api_source = '\n'.join(path.read_text(encoding='utf-8') for path in sorted((MODELS / 'Api').glob('*.cs')))
public_roots = set(re.findall(r'public sealed record (\w+)', api_source))
public_roots.add('InsightDto')
public_types = set().union(*(reachable(name) for name in public_roots))
lines = ['export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };', '']
for name in sorted(public_types):
    if name in enums:
        lines += ['export type ' + name + ' = ' + ' | '.join(json.dumps(v) for v in enums[name]) + ';', '']
    else:
        lines.append('export interface ' + name + ' {')
        for prop, typ, optional, omitted in records[name]:
            # Поле с JsonIgnore(WhenWritingNull) пропадает из JSON вместо null; остальные необязательные приходят и как null.
            if omitted: typ = typ.removesuffix('?')
            lines.append('  readonly ' + prop + ('?' if optional else '') + ': ' + ts_type(typ) + ';')
        lines += ['}', '']
(ROOT / 'api.ts').write_text('\n'.join(lines).rstrip('\n'), encoding='utf-8')

schema_roots = ['NewsTriage','NewsExtraction','NewsExtractionDraft','PdfPageExtraction','AgentPlan','InsightDraft','CriticResult','ChatDraft','ScopeGateResult',
                'FeedResponse','InsightDto','ChatAnswerDto','JobResponse','DocumentResponse','PageReviewRequest','UserActivityRequest']
(ROOT / 'schemas').mkdir(exist_ok=True)
for name in schema_roots:
    doc = {'$schema':'https://json-schema.org/draft/2020-12/schema', 'title':name,
           '$ref':'#/$defs/' + name, '$defs':reachable(name)}
    (ROOT / 'schemas' / (name + '.schema.json')).write_text(json.dumps(doc, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Generated api.ts ({len(public_types)} types) and {len(schema_roots)} JSON schemas.')