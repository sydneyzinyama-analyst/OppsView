from matcher.matching import match

AD = """Junior .NET Developer

About us
We are a fintech running on Azure and Kubernetes.

Requirements
- BSc Computer Science or a Diploma in IT
- C# and ASP.NET Core
- SQL Server
- Git

Advantageous
- Angular or React
- Docker
"""


def test_scores_required_skills_double():
    cv = "C#, ASP.NET Core, Git. Diploma in Information Technology."
    result = match(cv, AD)
    # Required: degree-or-diploma, C#, ASP.NET Core, SQL Server, Git, plus .NET
    # from the title -> 6 asks x2 = 12. Nice: Angular-or-React, Docker x1 = 2.
    # Matched: 5 required (all but SQL Server) = 10, nice 0. 10/14 = 71%.
    assert result.score == 71
    assert result.required.missing == ["SQL Server"]
    assert result.nice_to_have.missing == ["Angular or React", "Docker"]


def test_alternatives_are_satisfied_by_either_one():
    result = match("Diploma in IT. React.", AD)
    assert "Degree in IT / Computer Science or Diploma in IT" in result.required.matched
    assert "Angular or React" in result.nice_to_have.matched


def test_company_context_is_not_scored():
    result = match("", AD)
    asked = result.required.matched + result.required.missing + result.nice_to_have.missing
    assert "Azure" not in asked and "Kubernetes" not in asked


def test_skills_implied_by_the_cv_count():
    # The CV only says ASP.NET Core; that implies C# and .NET.
    result = match("ASP.NET Core Web API", "Requirements\nC#\n.NET\nREST APIs")
    assert result.required.missing == []
    assert result.score == 100


def test_a_skill_required_anywhere_is_not_also_nice_to_have():
    result = match("", "Requirements\nDocker\nNice to have\nDocker and Kubernetes")
    assert result.required.missing == ["Docker"]
    assert result.nice_to_have.missing == ["Kubernetes"]


def test_an_alternative_already_required_on_its_own_is_not_counted_twice():
    result = match("", "Requirements\nJava\nJava or Kotlin")
    assert result.required.missing == ["Java"]


def test_lists_cv_skills_the_ad_does_not_ask_for():
    result = match("C#, Python, Docker", "Requirements\nC#")
    assert result.extra_in_cv == ["Docker", "Python"]


def test_an_ad_with_no_known_skills_has_no_score():
    result = match("C#", "We need a hard-working person with a great attitude.")
    assert result.score is None
    assert "nothing to score" in result.summary


def test_summary_explains_the_score():
    result = match("C#", "Requirements\nC#\nJava\nNice to have\nDocker")
    assert result.summary == "40% match: you have 1 of 2 required skills and 0 of 1 nice-to-have skills."


def test_diploma_or_degree_is_satisfied_by_a_diploma():
    result = match(
        "Diploma in Information Technology",
        "Minimum requirements\n- Diploma or degree in IT / Computer Science",
    )
    assert result.required.missing == []
    assert result.score == 100
