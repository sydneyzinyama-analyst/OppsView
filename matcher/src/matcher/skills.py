"""The skills catalogue (skills.toml) and finding skills in text."""

from __future__ import annotations

import re
import tomllib
from dataclasses import dataclass, field
from functools import cache
from importlib import resources


@dataclass(frozen=True)
class Skill:
    name: str
    category: str
    aliases: tuple[str, ...] = ()
    case_sensitive: tuple[str, ...] = ()
    implies: tuple[str, ...] = ()
    # Compiled once when the catalogue loads, not on every search.
    _patterns: tuple[re.Pattern[str], ...] = field(default=(), compare=False, repr=False)

    def found_in(self, text: str) -> bool:
        return any(p.search(text) for p in self._patterns)

    def positions(self, text: str) -> list[tuple[int, int]]:
        """Every place the skill is mentioned, as (start, end)."""
        return [m.span() for p in self._patterns for m in p.finditer(text)]


def _pattern(alias: str, ignore_case: bool) -> re.Pattern[str]:
    # A skill must be a whole "word": "Java" must not match inside
    # "JavaScript", and "C#" must not match inside "C#.NET's" weirdness.
    # Letters, digits and the symbols that are part of skill names (+ # .)
    # can't touch either side; a trailing full stop is allowed ("...and Java.").
    escaped = re.escape(alias).replace(r"\ ", r"[\s\-]+")  # "rest api" also matches "rest-api"
    flags = re.IGNORECASE if ignore_case else 0
    return re.compile(rf"(?<![\w+#.]){escaped}(?![\w+#]|\.\w)", flags)


class Catalogue:
    """All known skills. Loaded once from skills.toml."""

    def __init__(self, skills: list[Skill]):
        self.skills = skills
        self.by_name = {s.name: s for s in skills}

    def find(self, text: str) -> set[str]:
        """Names of the skills mentioned in the text."""
        return {name for name, _, _ in self.find_ordered(text)}

    def find_ordered(self, text: str) -> list[tuple[str, int, int]]:
        """
        Skills mentioned in the text, with where they first appear, in reading
        order. A mention inside a longer one doesn't count: "SQL Server" is
        SQL Server, not also SQL; "React Native" is not also React.
        """
        mentions = [(s.name, start, end) for s in self.skills for start, end in s.positions(text)]

        def inside_longer(mention: tuple[str, int, int]) -> bool:
            name, start, end = mention
            return any(
                other_start <= start and end <= other_end and other_end - other_start > end - start
                for other_name, other_start, other_end in mentions
                if other_name != name
            )

        first: dict[str, tuple[str, int, int]] = {}
        for m in sorted(mentions, key=lambda m: m[1]):
            if m[0] not in first and not inside_longer(m):
                first[m[0]] = m
        return list(first.values())

    def with_implied(self, names: set[str]) -> set[str]:
        """Adds skills implied by the ones given (PostgreSQL -> SQL, ASP.NET Core -> C#, .NET)."""
        result = set(names)
        pending = list(names)
        while pending:
            skill = self.by_name.get(pending.pop())
            for implied in skill.implies if skill else ():
                if implied not in result:
                    result.add(implied)
                    pending.append(implied)
        return result


@cache
def load_catalogue() -> Catalogue:
    raw = tomllib.loads(resources.files("matcher").joinpath("skills.toml").read_text(encoding="utf-8"))
    skills = []
    for entry in raw["skill"]:
        aliases = tuple(entry.get("aliases", ()))
        exact = tuple(entry.get("case_sensitive", ()))
        # The display name is an alias too, unless it's listed as case-sensitive.
        names = aliases if entry["name"] in exact else (entry["name"], *aliases)
        patterns = tuple(_pattern(a, ignore_case=True) for a in dict.fromkeys(names)) + tuple(
            _pattern(a, ignore_case=False) for a in exact
        )
        skills.append(
            Skill(
                name=entry["name"],
                category=entry.get("category", "Other"),
                aliases=aliases,
                case_sensitive=exact,
                implies=tuple(entry.get("implies", ())),
                _patterns=patterns,
            )
        )
    return Catalogue(skills)
