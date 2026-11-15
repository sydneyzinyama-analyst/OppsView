import pytest

from matcher.skills import load_catalogue

catalogue = load_catalogue()


@pytest.mark.parametrize(
    ("text", "skill"),
    [
        ("5 years of C# experience", "C#"),
        ("Csharp and dotnet", "C#"),
        ("Strong .NET Core skills", ".NET"),
        ("Built APIs with ASP.NET Core", "ASP.NET Core"),
        ("Postgres and SQL Server", "PostgreSQL"),
        ("ReactJS front end", "React"),
        ("Experience with React.", "React"),
        ("Go/Golang microservices", "Go"),
        ("CI/CD pipelines with GitHub Actions", "CI/CD"),
        ("C++ and embedded systems", "C++"),
        ("rest-api design", "REST APIs"),
    ],
)
def test_finds_skills_in_their_usual_spellings(text, skill):
    assert skill in catalogue.find(text)


@pytest.mark.parametrize(
    ("text", "not_skill"),
    [
        ("JavaScript developer", "Java"),  # Java is not JavaScript
        ("You will be the rest of the team's go-to person", "REST APIs"),
        ("go-to person", "Go"),
        ("Must react quickly to incidents", "React"),
        ("A solid understanding of databases", "OOP"),
        ("You will excel in a fast-paced environment", "Excel"),
        ("swift turnaround times", "Swift"),
        ("Please express your interest", "Express"),
        ("C#.NET developer", "Java"),
    ],
)
def test_does_not_mistake_everyday_words_for_skills(text, not_skill):
    assert not_skill not in catalogue.find(text)


def test_implied_skills_are_added():
    assert catalogue.with_implied({"ASP.NET Core", "PostgreSQL"}) >= {
        "ASP.NET Core",
        ".NET",
        "C#",
        "PostgreSQL",
        "SQL",
    }


def test_implications_follow_chains():
    # Next.js -> React -> JavaScript
    assert "JavaScript" in catalogue.with_implied({"Next.js"})


def test_every_implied_skill_exists():
    names = set(catalogue.by_name)
    for skill in catalogue.skills:
        assert set(skill.implies) <= names, skill.name


def test_a_mention_inside_a_longer_skill_counts_only_as_the_longer_one():
    assert catalogue.find("SQL Server") == {"SQL Server"}
    assert catalogue.find("React Native apps") == {"React Native"}
    assert catalogue.find("SQL Server and some plain SQL") == {"SQL Server", "SQL"}
