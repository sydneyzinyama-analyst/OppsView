import io

import pytest
from docx import Document

from matcher.extraction import ExtractionError, extract_text

CV = (
    "Sydney Zinyama\nJunior Software Developer\n"
    "Skills: C#, ASP.NET Core, React, PostgreSQL, Git and xUnit testing."
)


def _docx(paragraphs: list[str], table: list[list[str]] | None = None) -> bytes:
    document = Document()
    for p in paragraphs:
        document.add_paragraph(p)
    if table:
        t = document.add_table(rows=len(table), cols=len(table[0]))
        for r, row in enumerate(table):
            for c, value in enumerate(row):
                t.cell(r, c).text = value
    buffer = io.BytesIO()
    document.save(buffer)
    return buffer.getvalue()


def _pdf(text: str) -> bytes:
    """A minimal valid one-page PDF with the given text (one line)."""
    stream = f"BT /F1 12 Tf 72 720 Td ({text}) Tj ET".encode()
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R "
        b"/Resources << /Font << /F1 5 0 R >> >> >>",
        b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"\nendstream",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ]
    out = io.BytesIO()
    out.write(b"%PDF-1.4\n")
    offsets = []
    for i, body in enumerate(objects, start=1):
        offsets.append(out.tell())
        out.write(f"{i} 0 obj\n".encode() + body + b"\nendobj\n")
    xref = out.tell()
    out.write(f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode())
    for offset in offsets:
        out.write(f"{offset:010d} 00000 n \n".encode())
    out.write(f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF".encode())
    return out.getvalue()


def test_reads_text_files():
    assert "ASP.NET Core" in extract_text("cv.txt", CV.encode())


def test_reads_word_documents_including_tables():
    text = extract_text("cv.docx", _docx(CV.splitlines(), table=[["Languages", "Python, TypeScript"]]))
    assert "React" in text
    assert "TypeScript" in text  # skills in a table are not missed


def test_reads_pdfs():
    text = extract_text(
        "CV.PDF", _pdf("Junior developer with C# ASP.NET Core React PostgreSQL and Git experience")
    )
    assert "PostgreSQL" in text


@pytest.mark.parametrize(
    ("filename", "content", "message"),
    [
        ("cv.jpg", b"x", "PDF, Word"),
        ("cv.doc", b"x", "PDF, Word"),
        ("cv.pdf", b"", "empty"),
        ("cv.pdf", b"not really a pdf", "couldn't be read"),
        ("cv.docx", b"not really a docx", "couldn't be read"),
        ("cv.txt", b"too short", "No text could be found"),
        ("cv.txt", b"x" * (5 * 1024 * 1024 + 1), "over 5 MB"),
    ],
)
def test_explains_files_it_cannot_read(filename, content, message):
    with pytest.raises(ExtractionError, match=message):
        extract_text(filename, content)
