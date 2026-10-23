"""ALF-58: выгрузка заголовков, сборка набора для llm-evaluate и подсчёт качества фильтра заголовков.

Сырые заголовки и разметка хранятся только в data/cache/alf58/ (игнорируется Git).

  python tools/alf58_triage.py export --db alfanews_a07_calib --out data/cache/alf58/headlines.csv
  python tools/alf58_triage.py suite --labels data/cache/alf58/headlines.csv --split test --batch 50 --seed 1 --out data/cache/alf58/suite-test-50.json
  dotnet run --project backend/AlfaNews.Worker -- llm-evaluate data/cache/alf58/suite-test-50.json data/cache/alf58/report-deepseek-test-50.json
  python tools/alf58_triage.py score --labels data/cache/alf58/headlines.csv --report data/cache/alf58/report-deepseek-test-50.json

GLM low: перед llm-evaluate задать Llm__TriageUseGlm=true. Повтор того же набора с тем же именем берётся из кэша шлюза,
поэтому для проверки повторяемости меняйте --seed (другое имя набора и порядок заголовков).
"""
import argparse
import csv
import json
import random
import statistics
import subprocess
import sys
from pathlib import Path

LABELS = ('yes', 'maybe', 'no')
COLUMNS = ['id', 'source_id', 'published_at', 'title', 'label', 'reason', 'ambiguous', 'split']
SPECIALIZED = 'spec-'


def export(args):
    query = (
        "COPY (SELECT id, source_id, published_at, title FROM news_raw "
        f"WHERE source_id NOT LIKE '{SPECIALIZED}%' AND title <> '' ORDER BY published_at DESC NULLS LAST, id LIMIT {int(args.limit)}) "
        "TO STDOUT WITH CSV HEADER")
    raw = subprocess.run(['docker', 'exec', '-i', args.container, 'psql', '-U', args.user, '-d', args.db, '-c', query],
                         check=True, capture_output=True).stdout.decode('utf-8')
    rows = list(csv.DictReader(raw.splitlines()))
    seen = set()
    unique = []
    for row in rows:
        key = ' '.join(row['title'].lower().split())
        if key in seen:
            continue
        seen.add(key)
        unique.append(row)
    rng = random.Random(args.seed)
    rng.shuffle(unique)
    # Половина — для разработки промпта, половина — независимая проверка; в few-shot попадают только dev.
    for index, row in enumerate(unique):
        row.update(label='', reason='', ambiguous='', split='dev' if index % 2 == 0 else 'test')
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open('w', encoding='utf-8-sig', newline='') as file:
        writer = csv.DictWriter(file, COLUMNS)
        writer.writeheader()
        writer.writerows(unique)
    print(f'{len(unique)} заголовков (из {len(rows)}, без повторов) → {out}')


def read_labels(path, split=None):
    with open(path, encoding='utf-8-sig', newline='') as file:
        rows = list(csv.DictReader(file))
    bad = [row['id'] for row in rows if row['label'] and row['label'] not in LABELS]
    if bad:
        sys.exit(f'Неизвестные метки у {len(bad)} строк, например {bad[:3]}')
    return [row for row in rows if row['label'] and (split is None or row['split'] == split)]


def suite(args):
    rows = read_labels(args.labels, args.split)
    if not rows:
        sys.exit('Нет размеченных строк для выбранной части.')
    rng = random.Random(args.seed)
    rng.shuffle(rows)
    name = f'alf58-{args.split}-b{args.batch}-s{args.seed}'
    cases = []
    for start in range(0, len(rows), args.batch):
        batch = rows[start:start + args.batch]
        cases.append({
            'name': f'batch-{start // args.batch + 1:03d}',
            'task': 'NewsTriage',
            'values': {'INPUT_JSON': [{'id': row['id'], 'title': row['title']} for row in batch]}
        })
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({'name': name, 'cases': cases}, ensure_ascii=False, indent=1), encoding='utf-8')
    print(f'{name}: {len(rows)} заголовков, {len(cases)} пакетов → {out}')


def score(args):
    labels = {row['id']: row for row in read_labels(args.labels)}
    report = json.loads(Path(args.report).read_text(encoding='utf-8'))
    matrix = {truth: {predicted: 0 for predicted in LABELS + ('missing',)} for truth in LABELS}
    invalid_cases = 0
    extra_ids = 0
    for case in report['cases']:
        candidate = case.get('candidate')
        if case.get('error') or candidate is None:
            invalid_cases += 1
            continue
        answers = {}
        for decision in candidate['decisions']:
            if decision['id'] not in labels or decision['id'] in answers:
                extra_ids += 1
                continue
            answers[decision['id']] = decision['importance']
        for item in case_ids(args.suite, case['name']) if args.suite else answers:
            if item in labels:
                matrix[labels[item]['label']][answers.get(item, 'missing')] += 1
    usage = [row for row in report['usage'] if row['attempt'] > 0]
    durations = [row['duration_ms'] for row in usage if row['duration_ms'] is not None]
    total = sum(sum(row.values()) for row in matrix.values())
    yes_predicted = sum(matrix[truth]['yes'] for truth in LABELS)
    yes_true = sum(matrix['yes'].values())
    print('Матрица (строки — разметка, столбцы — модель):')
    print('        ' + ''.join(f'{label:>9}' for label in LABELS + ('missing',)))
    for truth in LABELS:
        print(f'{truth:>7} ' + ''.join(f'{matrix[truth][label]:>9}' for label in LABELS + ('missing',)))
    print(f'Заголовков: {total}; пакетов с ошибкой: {invalid_cases}; лишних/повторных ID: {extra_ids}')
    print(f'yes: precision {safe(matrix["yes"]["yes"], yes_predicted)}, recall {safe(matrix["yes"]["yes"], yes_true)}')
    print(f'Важные (yes) ушли в no: {safe(matrix["yes"]["no"], yes_true)}; доля maybe у модели: '
          f'{safe(sum(matrix[truth]["maybe"] for truth in LABELS), total)}')
    print(f'Шум в первом приоритете (no→yes среди yes модели): {safe(matrix["no"]["yes"], yes_predicted)}')
    print(f'Запросов: {len(usage)}, починок: {sum(1 for row in usage if row["stage"] == "repair")}, '
          f'стоимость {sum(row["actual_cost_rub"] or 0 for row in usage):.4f} ₽, неизвестная стоимость: '
          f'{sum(1 for row in usage if row["actual_cost_rub"] is None)}')
    if durations:
        print(f'Задержка, мс: n={len(durations)}, медиана {statistics.median(durations):.0f}, максимум {max(durations)}'
              + ('' if len(durations) >= 20 else ' (p95 не считается: мало замеров)'))


def case_ids(suite_path, case_name):
    data = json.loads(Path(suite_path).read_text(encoding='utf-8'))
    case = next(item for item in data['cases'] if item['name'] == case_name)
    return [item['id'] for item in case['values']['INPUT_JSON']]


def safe(numerator, denominator):
    return f'{numerator}/{denominator} = {numerator / denominator:.2f}' if denominator else f'{numerator}/0'


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest='command', required=True)
    exporter = commands.add_parser('export')
    exporter.add_argument('--db', required=True)
    exporter.add_argument('--out', required=True)
    exporter.add_argument('--limit', type=int, default=400)
    exporter.add_argument('--seed', type=int, default=58)
    exporter.add_argument('--container', default='alfa-news-postgres-1')
    exporter.add_argument('--user', default='alfanews')
    builder = commands.add_parser('suite')
    builder.add_argument('--labels', required=True)
    builder.add_argument('--split', choices=('dev', 'test'), required=True)
    builder.add_argument('--batch', type=int, default=50)
    builder.add_argument('--seed', type=int, default=1)
    builder.add_argument('--out', required=True)
    scorer = commands.add_parser('score')
    scorer.add_argument('--labels', required=True)
    scorer.add_argument('--report', required=True)
    scorer.add_argument('--suite', help='набор, по которому строился отчёт: пропущенные моделью ID считаются как missing')
    args = parser.parse_args()
    {'export': export, 'suite': suite, 'score': score}[args.command](args)


if __name__ == '__main__':
    main()
