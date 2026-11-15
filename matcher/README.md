# OppsView CV matcher

Phase 3: a small **Python + FastAPI** service that compares a CV with a job ad and returns a match score, the skills you have, and the skills you're missing (required vs nice-to-have).

It's an internal service. The browser never calls it: the OppsView .NET API does, after checking who's signed in.

```
Website ──▶ .NET API (/cv, /match) ──▶ matcher (/extract, /match) :8000
```

## How the matching works

1. **Skills catalogue** ([`src/matcher/skills.toml`](src/matcher/skills.toml)): about 75 skills with the ways people write them (C# = csharp, .NET = dotnet…). Everyday words that are also skills (React, Excel, Swift, Go…) only count when written the way the technology is. Some skills imply others: PostgreSQL implies SQL, and ASP.NET Core implies C# and .NET. **To add a skill, add a block to this file; no code changes are needed.**
2. **Reading the job ad** ([`job_ad.py`](src/matcher/job_ad.py)): text under headings like "Advantageous" or "Nice to have", and lines that call themselves a "bonus" or "advantage", count as *nice to have*. "About us", "Benefits" and "How to apply" are ignored. Everything else counts as *required*.
3. **Scoring** ([`matching.py`](src/matcher/matching.py)): skills joined by "or" or "/" are alternatives ("Angular or React", "Diploma or degree"), and having any one of them counts. Required skills weigh double. The score is `earned / possible × 100`, with a plain-English summary ("8 of 8 required skills and 1 of 4 nice-to-have").
4. **CV text** ([`extraction.py`](src/matcher/extraction.py)): from PDF, Word (.docx, including tables) or .txt. Scanned (image-only) CVs get a clear message.

Plain keyword rules rather than machine learning, so every result can be explained. Smarter matching (e.g. "3+ years of C#") can come later.

## Running it (Windows, PowerShell)

You need **Python 3.12 or newer**. With Scoop:

```powershell
scoop install python
python --version
```

Then, from the `matcher` folder, the first time only:

```powershell
python -m venv .venv                 # a private Python environment for this project
.venv\Scripts\Activate.ps1           # use it in this window (the prompt shows (.venv))
pip install -e ".[dev]"              # FastAPI, PDF/Word readers, pytest, ruff
```

> If PowerShell says running scripts is disabled, run once:
> `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

Every time you want to run it:

```powershell
.venv\Scripts\Activate.ps1
uvicorn matcher.api:app --reload --port 8000
```

It listens on http://localhost:8000. Interactive docs are at http://localhost:8000/docs.

### Optional: a shared key

If the environment variable `MATCHER_API_KEY` is set, the matcher only answers requests that send the same value in an `X-Api-Key` header. The .NET API sends it from its `Matcher:ApiKey` setting. Locally this is optional, because the matcher only listens on your own machine. To use one:

```powershell
$env:MATCHER_API_KEY = 'some-long-random-value'      # in the matcher's window, before uvicorn
```

```powershell
dotnet user-secrets set "Matcher:ApiKey" 'some-long-random-value' --project src/OppsView.Api   # from api/
```

## Endpoints

| Method | Route | |
|---|---|---|
| POST | `/match` | `{ "cvText": "...", "jobAd": "..." }` returns `{ score, required: {matched, missing}, niceToHave: {...}, extraInCv, summary }` |
| POST | `/extract` | multipart `file` (PDF, .docx, .txt; max 5 MB) returns `{ text }`, or 422 with a message |
| GET | `/skills` | the catalogue |
| GET | `/health` | `{ "status": "ok" }` (never needs the key) |

## Tests and linting

```powershell
pytest
ruff check .
ruff format .
```
