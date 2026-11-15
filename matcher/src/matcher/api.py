"""
The matcher's web API (FastAPI).

It's an internal service: the OppsView .NET API calls it, the browser
doesn't. If MATCHER_API_KEY is set, every request (except /health) must
send it in the X-Api-Key header, so nothing else on the network can use it.

Run locally:  uvicorn matcher.api:app --reload --port 8000
Docs:         http://localhost:8000/docs
"""

from __future__ import annotations

import os
import secrets
from typing import Annotated

from fastapi import Depends, FastAPI, File, Header, HTTPException, UploadFile, status
from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from matcher import extraction
from matcher.matching import match
from matcher.skills import load_catalogue

app = FastAPI(
    title="OppsView CV matcher",
    description="Compares a CV with a job ad: match score, matched and missing skills.",
    version="0.1.0",
)


def require_api_key(x_api_key: Annotated[str | None, Header()] = None) -> None:
    """A FastAPI dependency: runs before each endpoint that lists it."""
    expected = os.environ.get("MATCHER_API_KEY")
    if not expected:
        return  # no key configured: open (local development)
    # compare_digest takes the same time whatever the input, so the key
    # can't be guessed character by character from response times.
    if not x_api_key or not secrets.compare_digest(x_api_key, expected):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Missing or wrong X-Api-Key.")


Protected = [Depends(require_api_key)]


class CamelModel(BaseModel):
    """JSON in camelCase (cvText, niceToHave), like the rest of OppsView."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class MatchRequest(CamelModel):
    cv_text: str = Field(min_length=1, max_length=100_000)
    job_ad: str = Field(min_length=1, max_length=50_000)


class SkillGroupResponse(CamelModel):
    matched: list[str]
    missing: list[str]


class MatchResponse(CamelModel):
    score: int | None = Field(description="0-100, or null if the ad mentions no known skills.")
    required: SkillGroupResponse
    nice_to_have: SkillGroupResponse
    extra_in_cv: list[str] = Field(description="Skills in the CV that the ad doesn't ask for.")
    summary: str


class ExtractResponse(CamelModel):
    text: str


class SkillResponse(CamelModel):
    name: str
    category: str
    aliases: list[str]


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/match", dependencies=Protected)
def match_cv(request: MatchRequest) -> MatchResponse:
    result = match(request.cv_text, request.job_ad)
    return MatchResponse(
        score=result.score,
        required=SkillGroupResponse(**vars(result.required)),
        nice_to_have=SkillGroupResponse(**vars(result.nice_to_have)),
        extra_in_cv=result.extra_in_cv,
        summary=result.summary,
    )


@app.post("/extract", dependencies=Protected)
async def extract(
    file: Annotated[UploadFile, File(description="CV as PDF, .docx or .txt, up to 5 MB")],
) -> ExtractResponse:
    # Read one byte past the limit: enough to know it's too big without
    # loading a huge upload into memory.
    content = await file.read(extraction.MAX_BYTES + 1)
    try:
        return ExtractResponse(text=extraction.extract_text(file.filename or "", content))
    except extraction.ExtractionError as error:
        raise HTTPException(status.HTTP_422_UNPROCESSABLE_CONTENT, str(error)) from error


@app.get("/skills", dependencies=Protected)
def skills() -> list[SkillResponse]:
    return [
        SkillResponse(name=s.name, category=s.category, aliases=list(s.aliases))
        for s in load_catalogue().skills
    ]
