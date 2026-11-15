"""Compares a CV with a job ad: the heart of the matcher."""

from __future__ import annotations

import re
from dataclasses import dataclass, field

from matcher import job_ad
from matcher.skills import Catalogue, load_catalogue

# A required skill counts twice as much as a nice-to-have one.
REQUIRED_WEIGHT = 2
NICE_WEIGHT = 1


@dataclass
class SkillGroup:
    matched: list[str] = field(default_factory=list)
    missing: list[str] = field(default_factory=list)


@dataclass
class MatchResult:
    # 0-100, or None when the ad mentions no skill OppsView knows.
    score: int | None
    required: SkillGroup
    nice_to_have: SkillGroup
    # Skills in the CV that the ad doesn't ask for: worth knowing, not scored.
    extra_in_cv: list[str]
    summary: str


def match(cv_text: str, job_ad_text: str, catalogue: Catalogue | None = None) -> MatchResult:
    catalogue = catalogue or load_catalogue()

    parts = job_ad.split(job_ad_text)
    required = _asks(parts.required, catalogue)
    # A skill listed as required anywhere is required, even if it's also
    # mentioned in the "nice to have" part.
    required_skills = {s for ask in required for s in ask}
    nice = [ask for ask in _asks(parts.nice_to_have, catalogue) if not ask & required_skills]

    # Having ASP.NET Core means having C# too, even if the CV doesn't say so.
    in_cv = catalogue.with_implied(catalogue.find(cv_text))

    required_group = _group(required, in_cv)
    nice_group = _group(nice, in_cv)

    total = len(required) * REQUIRED_WEIGHT + len(nice) * NICE_WEIGHT
    if total == 0:
        score = None
    else:
        earned = len(required_group.matched) * REQUIRED_WEIGHT + len(nice_group.matched) * NICE_WEIGHT
        score = round(100 * earned / total)

    asked = required_skills | {s for ask in nice for s in ask}
    extra = sorted(catalogue.find(cv_text) - asked, key=str.lower)
    return MatchResult(score, required_group, nice_group, extra, _summary(score, required_group, nice_group))


# Text between two skills that makes them alternatives: " or ", ", or ", "/".
_ALTERNATIVE = re.compile(r"^\s*(,?\s*(or|and/or)\s+(an?\s+|the\s+)?|/)\s*$", re.IGNORECASE)


def _asks(text: str, catalogue: Catalogue) -> list[frozenset[str]]:
    """
    What the text asks for. Each ask is one skill, or a set of alternatives
    when skills are joined by "or" / "/" ("Angular or React", "BSc Computer
    Science or a Diploma in IT"): having any one of them satisfies it.
    """
    singles: set[str] = set()
    groups: list[set[str]] = []
    for line in text.splitlines():
        found = catalogue.find_ordered(line)
        current: set[str] = set()
        previous_end = None
        for name, start, end in found:
            between = line[previous_end:start] if previous_end is not None else None
            if current and between is not None and _ALTERNATIVE.match(between):
                current.add(name)
            else:
                _flush(current, singles, groups)
                current = {name}
            previous_end = end
        _flush(current, singles, groups)

    # A skill asked for on its own somewhere is already a firm ask, so an
    # "either/or" that includes it adds nothing; skip those, and duplicates.
    asks = [frozenset({s}) for s in singles]
    for group in map(frozenset, groups):
        if not group & singles and group not in asks:
            asks.append(group)
    return asks


def _flush(current: set[str], singles: set[str], groups: list[set[str]]) -> None:
    if len(current) == 1:
        singles.update(current)
    elif len(current) > 1:
        groups.append(set(current))


def _label(ask: frozenset[str]) -> str:
    return " or ".join(sorted(ask, key=str.lower))


def _group(asks: list[frozenset[str]], in_cv: set[str]) -> SkillGroup:
    ordered = sorted(asks, key=lambda a: _label(a).lower())
    return SkillGroup(
        matched=[_label(a) for a in ordered if a & in_cv],
        missing=[_label(a) for a in ordered if not a & in_cv],
    )


def _summary(score: int | None, required: SkillGroup, nice: SkillGroup) -> str:
    if score is None:
        return "The job ad doesn't mention any skills OppsView knows, so there's nothing to score."
    req_total = len(required.matched) + len(required.missing)
    parts = []
    if req_total:
        parts.append(f"{len(required.matched)} of {req_total} required skills")
    nice_total = len(nice.matched) + len(nice.missing)
    if nice_total:
        parts.append(f"{len(nice.matched)} of {nice_total} nice-to-have skills")
    return f"{score}% match: you have " + " and ".join(parts) + "."
