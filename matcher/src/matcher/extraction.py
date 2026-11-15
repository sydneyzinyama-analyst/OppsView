"""Getting plain text out of an uploaded CV (PDF, Word or text)."""

from __future__ import annotations

import io
from pathlib import PurePath
from zipfile import BadZipFile

from docx import Document
from docx.opc.exceptions import PackageNotFoundError
from pypdf import PdfReader
from pypdf.errors import PdfReadError

MAX_BYTES = 5 * 1024 * 1024
SUPPORTED = {".pdf", ".docx", ".txt"}


class ExtractionError(ValueError):
    """The file couldn't be turned into text; the message says why, for the user."""


def extract_text(filename: str, content: bytes) -> str:
    suffix = PurePath(filename or "").suffix.lower()
    if suffix not in SUPPORTED:
        raise ExtractionError("Please upload your CV as a PDF, Word (.docx) or text file.")
    if len(content) > MAX_BYTES:
        raise ExtractionError("That file is over 5 MB. Please upload a smaller CV.")
    if not content:
        raise ExtractionError("That file is empty.")

    try:
        if suffix == ".pdf":
            text = _pdf(content)
        elif suffix == ".docx":
            text = _docx(content)
        else:
            text = content.decode("utf-8", errors="replace")
    except (PdfReadError, BadZipFile, PackageNotFoundError, ValueError, KeyError, OSError) as error:
        # A .docx is a zip file inside; a corrupt or renamed file fails there.
        raise ExtractionError("That file couldn't be read. Is it a valid PDF or Word document?") from error

    text = "\n".join(line.strip() for line in text.splitlines() if line.strip())
    if len(text) < 50:
        # Scanned CVs are pictures of text: there's no text to read.
        raise ExtractionError(
            "No text could be found in that file. If it's a scanned CV, please upload a PDF or Word version "
            "that was typed, not scanned."
        )
    return text


def _pdf(content: bytes) -> str:
    reader = PdfReader(io.BytesIO(content))
    return "\n".join(page.extract_text() or "" for page in reader.pages)


def _docx(content: bytes) -> str:
    document = Document(io.BytesIO(content))
    lines = [p.text for p in document.paragraphs]
    # Many CV templates put skills in tables.
    for table in document.tables:
        for row in table.rows:
            lines.extend(cell.text for cell in row.cells)
    return "\n".join(lines)
