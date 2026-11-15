"""Splits a job ad into what's required and what's only "nice to have"."""

from __future__ import annotations

import re
from dataclasses import dataclass

# Headings that start a "nice to have" part of the ad.
_NICE_HEADING = re.compile(
    r"^\W*("
    r"nice[\s-]to[\s-]haves?|good to have|advantageous|(would be |will be )?(an )?advantage(ous)?"
    r"|added advantage|beneficial|bonus( points)?|desirable|plus"
    r"|preferred( skills| qualifications| experience)?|not required but|it would be (nice|great)"
    r")\b.{0,40}$",
    re.IGNORECASE,
)

# Headings that start a "required" part.
_REQUIRED_HEADING = re.compile(
    r"^\W*("
    r"(minimum |key |essential |job |technical )?"
    r"(requirements?|qualifications?( and experience)?|skills( required)?|experience( required)?)"
    r"|what (you('ll)? need|we('re| are) looking for|you bring)|must[\s-]haves?"
    r"|required( skills| experience)?|who you are|about you|you have|competencies"
    r")\b.{0,40}$",
    re.IGNORECASE,
)

# Headings about the job itself: skills mentioned there are expected.
_DUTIES_HEADING = re.compile(
    r"^\W*((key )?(duties|responsibilities)|what you('ll| will) do|the role|role description|job description|"
    r"your role|day[\s-]to[\s-]day)\b.{0,40}$",
    re.IGNORECASE,
)

# Headings about the company or the offer: skills there aren't asks
# ("We are a fintech running on Azure" doesn't mean you need Azure).
_CONTEXT_HEADING = re.compile(
    r"^\W*(about (us|the company|the client|our client|the team)|who we are|company (overview|profile)|"
    r"benefits|what we offer|why join( us)?|perks|remuneration|salary|how to apply|application process|"
    r"location|closing date|equal opportunit|employment equity|disclaimer|popia)\b.{0,60}$",
    re.IGNORECASE,
)

# A single line that says its contents are optional.
_NICE_LINE = re.compile(
    r"\b(advantageous|advantage|nice[\s-]to[\s-]have|beneficial|bonus|preferred|desirable|is a plus|"
    r"would be great|not essential|not required)\b",
    re.IGNORECASE,
)


@dataclass
class JobAdParts:
    required: str
    nice_to_have: str


def split(job_ad: str) -> JobAdParts:
    """
    Reads the ad line by line. Text under a "nice to have" heading, or on a
    line that calls itself advantageous, is nice-to-have. Text under
    "About us", "Benefits", "How to apply" and the like is ignored.
    Everything else (requirements, duties, the general description) counts
    as required, because a skill an ad mentions is usually one it expects.
    """
    required: list[str] = []
    nice: list[str] = []
    section = "required"  # "required", "nice" or "context" (ignored)

    for raw_line in job_ad.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if _NICE_HEADING.match(line):
            section = "nice"
            continue
        if _CONTEXT_HEADING.match(line):
            section = "context"
            continue
        if _REQUIRED_HEADING.match(line) or _DUTIES_HEADING.match(line):
            section = "required"
            continue
        if section == "context":
            continue
        if section == "nice" or _NICE_LINE.search(line):
            nice.append(line)
        else:
            required.append(line)

    return JobAdParts(required="\n".join(required), nice_to_have="\n".join(nice))
