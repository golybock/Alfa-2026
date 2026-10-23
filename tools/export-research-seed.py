import argparse
import json
import pathlib
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--container", default="alfa-news-postgres-1")
parser.add_argument("--database", required=True)
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parent.parent
index = json.loads((root / "data/bank_research/index.json").read_text(encoding="utf-8-sig"))
if any(re.fullmatch("[0-9a-f]{64}", entry["sha256"]) is None for entry in index):
    raise ValueError("The index contains an invalid SHA-256")
hashes = ",".join("'" + entry["sha256"] + "'" for entry in index)
query = f"""
SELECT jsonb_pretty(jsonb_build_object(
    'documents', COALESCE((SELECT jsonb_agg((to_jsonb(d) - 'uploaded_by')
        || jsonb_build_object('file_path', 'files/' || d.sha256 || '.pdf') ORDER BY d.sha256)
        FROM research_doc d WHERE d.sha256 IN ({hashes})), '[]'::jsonb),
    'pages', COALESCE((SELECT jsonb_agg(to_jsonb(p) - 'reviewed_by' - 'reviewed_at' ORDER BY d.sha256,p.page_no)
        FROM research_page p JOIN research_doc d ON p.document_id=d.id WHERE d.sha256 IN ({hashes})), '[]'::jsonb),
    'chunks', COALESCE((SELECT jsonb_agg(to_jsonb(c) - 'search_text' - 'embedding' ORDER BY d.sha256,p.page_no,c.local_id)
        FROM research_chunk c JOIN research_page p ON c.page_id=p.id JOIN research_doc d ON p.document_id=d.id
        WHERE d.sha256 IN ({hashes})), '[]'::jsonb),
    'live_pages', COALESCE((SELECT jsonb_agg(DISTINCT p.id)
        FROM research_page p JOIN research_doc d ON p.document_id=d.id
        JOIN background_job j ON j.kind='OcrPdfPage' AND j.payload->>'document_id'=d.id::text
            AND (j.payload->>'page_no')::integer=p.page_no
        JOIN agent_run r ON r.job_id=j.id JOIN llm_usage u ON u.run_id=r.id
        WHERE d.sha256 IN ({hashes}) AND u.error IS NULL AND u.actual_cost_rub>0
            AND u.stage IN ('pdf_ocr','repair')), '[]'::jsonb)));
"""
result = subprocess.run(["docker", "exec", "-i", args.container, "sh", "-c",
    'psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$1"', "sh", args.database],
    input=query, text=True, encoding="utf-8", capture_output=True, check=True)
seed = json.loads(result.stdout)
live_pages = set(seed.pop("live_pages"))
if len(seed["documents"]) != len(index) or len(seed["pages"]) != sum(entry["page_count"] for entry in index):
    raise ValueError("The source package has not been fully registered")
if any(page["ocr_status"] not in ("ready", "failed") for page in seed["pages"]):
    raise ValueError("OCR still has pending pages")
if any(page["ocr_status"] == "ready" and page["id"] not in live_pages for page in seed["pages"]):
    raise ValueError("A ready page has no successful paid live attempt; mock results must not enter this seed")
if any(doc["ocr_status"] not in ("ready", "partial", "failed") for doc in seed["documents"]):
    raise ValueError("Documents have not been finalized")
if any(row["review_status"] != "not_reviewed" for group in ("documents", "pages", "chunks") for row in seed[group]):
    raise ValueError("This seed must not distribute operator reviews")
if any(chart["review_status"] != "not_reviewed" for chunk in seed["chunks"] for chart in chunk["chart_data"]):
    raise ValueError("This seed must not distribute chart reviews")
output = root / "backend/AlfaNews.Core/Data/Migrations/Seeds/bank-research-ocr.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(result.stdout.rstrip(), encoding="utf-8")
print(f"Exported {len(seed['documents'])} documents, {len(seed['pages'])} pages, {len(seed['chunks'])} chunks")