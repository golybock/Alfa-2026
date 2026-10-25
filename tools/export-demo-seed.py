import argparse
import pathlib
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--container", default="alfa-news-postgres-1")
parser.add_argument("--database", required=True)
parser.add_argument("--output", default="data/seed/demo.sql.gz")
args = parser.parse_args()
if re.fullmatch("[a-z0-9_]+", args.database) is None:
    raise ValueError("Invalid database name")
root = pathlib.Path(__file__).resolve().parent.parent
output = root / args.output
copy = args.database + "_seed_export"
cleanup = """
DELETE FROM auth_session;
DELETE FROM llm_cache;
UPDATE background_job SET status = 'failed', last_error = 'seed_export' WHERE status IN ('queued', 'running');
UPDATE insight SET status = 'failed', last_error = 'seed_export' WHERE status IN ('queued', 'processing');
UPDATE chat_message SET status = 'failed' WHERE status IN ('queued', 'processing');
UPDATE news_raw SET status = 'failed', last_error = 'seed_export' WHERE status IN ('queued', 'processing');
UPDATE news_raw SET text = '';
"""


def run(script: str, stdin: str | None = None) -> bytes:
    return subprocess.run(["docker", "exec", "-i", args.container, "sh", "-c", script],
                          input=stdin.encode() if stdin else None, capture_output=True, check=True).stdout


try:
    run(f'dropdb -U "$POSTGRES_USER" --if-exists {copy} && createdb -U "$POSTGRES_USER" {copy}'
        f' && pg_dump -U "$POSTGRES_USER" -d {args.database} | psql -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d {copy}')
    run(f'psql -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d {copy}', cleanup)
    dump = run(f'pg_dump -U "$POSTGRES_USER" -d {copy} --no-owner --no-privileges | gzip -9')
finally:
    run(f'dropdb -U "$POSTGRES_USER" --if-exists {copy}')
output.parent.mkdir(parents=True, exist_ok=True)
output.write_bytes(dump)
print(f"{output}: {len(dump) / 1024 / 1024:.1f} MiB")