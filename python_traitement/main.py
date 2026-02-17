import asyncio
from io import StringIO
from typing import Optional

import httpx
import pandas as pd
from cachetools import TTLCache, cached
from fastapi import FastAPI, File, HTTPException, Query, UploadFile
from fastapi.requests import Request
from fastapi.templating import Jinja2Templates
from pydantic import BaseModel, ValidationError
from starlette.templating import _TemplateResponse

DOWNSTREAM_URL = "http://dotnet-backend:5124/logs/bulk"

app = FastAPI()
templates = Jinja2Templates(directory="templates")
_last_records: list = []
cache = TTLCache(maxsize=100, ttl=30)


class LogItem(BaseModel):
    migration_start_time: str
    sub_job_id: str
    title: str
    type: str
    source_id: str
    source: str
    destination_id: str
    destination: str
    size: str
    status: str
    migration_action: str
    comment: Optional[str] = ""
    error_code: Optional[str] = ""


COLUMN_MAP = {
    "Migration start time": "migration_start_time",
    "Sub job ID": "sub_job_id",
    "Title": "title",
    "Type": "type",
    "Source ID": "source_id",
    "Source": "source",
    "Destination ID": "destination_id",
    "Destination": "destination",
    "Size": "size",
    "Status": "status",
    "Migration action": "migration_action",
    "Comment": "comment",
    "Error code": "error_code",
}


@cached(cache)
def fetch_logs():
    with httpx.Client(timeout=30.0) as client:
        resp = client.get("http://dotnet-backend:5124/logs")
        resp.raise_for_status()
        return resp.json()


async def _read_upload_to_text(upload: UploadFile) -> str:
    contents = await upload.read()
    return contents.decode("utf-8")


@app.get("/")
async def get_root(
    req: Request, page: int = Query(1, ge=1), page_size: int = Query(10, ge=1)
) -> _TemplateResponse:
    if "logs" not in cache:
        cache["logs"] = fetch_logs()
        logs = cache["logs"]
    logs = cache["logs"]

    start_idx = (page - 1) * page_size
    end_idx = start_idx + page_size

    paginated_logs = logs[start_idx:end_idx]
    total_records = len(logs)

    return templates.TemplateResponse(
        req,
        "index.html",
        {
            "logs": paginated_logs,
            "total_records": total_records,
            "page": page,
            "page_size": page_size,
            "total_pages": (total_records + page_size - 1) // page_size,
        },
    )


@app.post("/upload")
async def upload_csvs(
    req: Request, files: list[UploadFile] = [File(...)]
) -> _TemplateResponse:
    if not files:
        raise HTTPException(status_code=400, detail="No files uploaded")

    read_tasks = [_read_upload_to_text(f) for f in files]
    texts = await asyncio.gather(*read_tasks)

    loop = asyncio.get_running_loop()
    parse_tasks = [
        loop.run_in_executor(None, lambda t=text: pd.read_csv(StringIO(t)))
        for text in texts
    ]
    dfs = await asyncio.gather(*parse_tasks)
    total_rows = sum([len(d) for d in dfs])

    merged = pd.concat(dfs, ignore_index=True, sort=False).drop_duplicates(
        ignore_index=True
    )

    merged.columns = [c.strip() for c in merged.columns]
    merged = merged.rename(columns=COLUMN_MAP)

    records = merged.where(pd.notnull(merged), None).to_dict(orient="records")
    valid_items = []
    errors = []
    for i, rec in enumerate(records):
        clean = {
            k: (None if (isinstance(v, float) and pd.isna(v)) else v)
            for k, v in rec.items()
        }
        try:
            item = LogItem.model_validate(clean)
            valid_items.append(item.model_dump())
        except ValidationError as ve:
            errors.append({"index": i, "errors": ve.errors()})  # , "record": clean})

    if not valid_items:
        raise HTTPException(status_code=422, detail={"validation_errors": errors})

    global _last_records
    _last_records = valid_items

    async with httpx.AsyncClient(timeout=30.0) as client:
        resp = await client.post(DOWNSTREAM_URL, json=valid_items)
        if resp.status_code >= 400:
            return templates.TemplateResponse(req, "error.html", {"error": resp.text})
    return templates.TemplateResponse(
        req,
        "upload.html",
        {
            "uploaded_files": len(files),
            "rows_received": total_rows,
            "rows_valid": len(valid_items),
            "validation_errors": errors,
            "downstream_status": resp.status_code,
        },
    )
