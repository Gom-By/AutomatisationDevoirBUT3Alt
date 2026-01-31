import asyncio
from datetime import date
from io import StringIO
from typing import List, Optional

import httpx
import pandas as pd
from fastapi import FastAPI, File, HTTPException, UploadFile
from pydantic import BaseModel, ValidationError

DOWNSTREAM_URL = "http://localhost:5124/logs/bulk"

app = FastAPI()
_last_records: Optional[list] = None


class LogItem(BaseModel):
    migration_start_time: str
    sub_job_id: str
    title: str
    type: str
    source_ID: str
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
    "Source ID": "source_ID",
    "Source": "source",
    "Destination ID": "destination_id",
    "Destination": "destination",
    "Size": "size",
    "Status": "status",
    "Migration action": "migration_action",
    "Comment": "comment",
    "Error code": "error_code",
}


async def _read_upload_to_text(upload: UploadFile) -> str:
    contents = await upload.read()
    return contents.decode("utf-8")


async def _parse_csv_text(text: str) -> pd.DataFrame:
    return pd.read_csv(StringIO(text))


@app.post("/upload")
async def upload_csvs(files: List[UploadFile] = File(...)):
    if not files:
        raise HTTPException(status_code=400, detail="No files uploaded")

    read_tasks = [asyncio.create_task(_read_upload_to_text(f)) for f in files]
    texts = await asyncio.gather(*read_tasks)

    loop = asyncio.get_running_loop()
    parse_tasks = [
        loop.run_in_executor(None, lambda t=text: pd.read_csv(StringIO(t)))
        for text in texts
    ]
    dfs = await asyncio.gather(*parse_tasks)

    merged = pd.concat(dfs, ignore_index=True, sort=False).drop_duplicates(
        ignore_index=True
    )

    merged.columns = [c.strip() for c in merged.columns]
    merged = merged.rename(columns=COLUMN_MAP)

    records = merged.where(pd.notnull(merged), None).to_dict(orient="records")

    valid_items = []
    errors = []
    for i, rec in enumerate(records):
        # replace any float('nan') in dict values with None
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

    # after building valid_items and before sending downstream
    global _last_records
    _last_records = valid_items

    # send to downstream API
    async with httpx.AsyncClient(timeout=30.0) as client:
        resp = await client.post(DOWNSTREAM_URL, json=valid_items)
        if resp.status_code >= 400:
            raise HTTPException(
                status_code=502,
                detail={"downstream_status": resp.status_code, "text": resp.text},
            )

    return {
        "uploaded_files": len(files),
        "rows_received": len(records),
        "rows_valid": len(valid_items),
        "validation_errors": errors,
        "downstream_status": resp.status_code,
    }


@app.get("/logs")
async def get_logs():
    if not _last_records:
        return []
    return _last_records
