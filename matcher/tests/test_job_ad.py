from matcher.job_ad import split


def test_nice_to_have_heading_moves_skills_out_of_required():
    parts = split("Requirements\nC# and SQL\nAdvantageous\nDocker\nAzure")
    assert "C#" in parts.required
    assert "Docker" in parts.nice_to_have and "Docker" not in parts.required


def test_a_line_that_calls_itself_optional_is_nice_to_have():
    parts = split("Requirements\n- Java\n- Kubernetes experience would be advantageous")
    assert "Kubernetes" in parts.nice_to_have
    assert "Java" in parts.required


def test_requirements_heading_ends_a_nice_to_have_section():
    parts = split("Nice to have\nDocker\nMinimum requirements\nPython")
    assert "Python" in parts.required


def test_company_context_and_benefits_are_ignored():
    parts = split("About us\nWe run on Azure and Kubernetes.\nRequirements\nC#\nBenefits\nFree AWS training")
    assert "Azure" not in parts.required and "Azure" not in parts.nice_to_have
    assert "AWS" not in parts.required
    assert "C#" in parts.required


def test_duties_count_as_required():
    parts = split("About the company\nWe use Azure.\nKey responsibilities\nBuild REST APIs in C#")
    assert "REST APIs in C#" in parts.required
