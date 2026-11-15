import pytest
from fastapi.testclient import TestClient

from matcher.api import app

client = TestClient(app)

AD = "Requirements\nC# and ASP.NET Core\nSQL Server\nAdvantageous\nDocker"
CV = "Junior developer: C#, ASP.NET Core Web API, PostgreSQL, Git."


def test_health():
    assert client.get("/health").json() == {"status": "ok"}


def test_match_returns_camel_case_json():
    response = client.post("/match", json={"cvText": CV, "jobAd": AD})

    assert response.status_code == 200
    body = response.json()
    assert set(body) == {"score", "required", "niceToHave", "extraInCv", "summary"}
    assert body["required"]["missing"] == ["SQL Server"]
    assert body["niceToHave"]["missing"] == ["Docker"]
    assert "PostgreSQL" in body["extraInCv"]
    assert 0 <= body["score"] <= 100


@pytest.mark.parametrize("payload", [{}, {"cvText": "", "jobAd": AD}, {"cvText": CV}])
def test_match_validates_input(payload):
    assert client.post("/match", json=payload).status_code == 422


def test_extract_reads_an_uploaded_file():
    response = client.post("/extract", files={"file": ("cv.txt", CV.encode() * 2, "text/plain")})
    assert response.status_code == 200
    assert "ASP.NET Core" in response.json()["text"]


def test_extract_explains_unreadable_files():
    response = client.post("/extract", files={"file": ("photo.jpg", b"\xff\xd8", "image/jpeg")})
    assert response.status_code == 422
    assert "PDF, Word" in response.json()["detail"]


def test_skills_lists_the_catalogue():
    names = [s["name"] for s in client.get("/skills").json()]
    assert "C#" in names and "Docker" in names


class TestApiKey:
    @pytest.fixture(autouse=True)
    def _key(self, monkeypatch):
        monkeypatch.setenv("MATCHER_API_KEY", "s3cret")

    def test_requests_without_the_key_are_refused(self):
        assert client.post("/match", json={"cvText": CV, "jobAd": AD}).status_code == 401

    def test_requests_with_the_wrong_key_are_refused(self):
        response = client.post("/match", json={"cvText": CV, "jobAd": AD}, headers={"X-Api-Key": "nope"})
        assert response.status_code == 401

    def test_requests_with_the_key_are_served(self):
        response = client.post("/match", json={"cvText": CV, "jobAd": AD}, headers={"X-Api-Key": "s3cret"})
        assert response.status_code == 200

    def test_health_never_needs_the_key(self):
        assert client.get("/health").status_code == 200
