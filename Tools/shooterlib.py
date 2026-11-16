"""Shared helpers for the Tools/*.py scripts (hashing, timestamps, percentiles).

Scripts run as `python Tools/<script>.py`, so this module is importable as a sibling. Keep it stdlib-only.
"""
import hashlib
import math
from datetime import datetime
from pathlib import Path


def sha(path: Path) -> str:
    """SHA-256 hex digest of a file's bytes."""
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def utc(text: str, require_timezone: bool = False) -> datetime:
    """Parse an ISO-8601 timestamp; a trailing Z means UTC."""
    result = datetime.fromisoformat(text.replace("Z", "+00:00"))
    if require_timezone and result.tzinfo is None:
        raise ValueError("Evidence review time must include its timezone")
    return result


def percentile(values, probability):
    """Nearest-rank percentile over finite, non-negative samples."""
    if not values or not 0 < probability <= 1 or any(not math.isfinite(x) or x < 0 for x in values):
        raise ValueError("Invalid percentile sample set")
    values = sorted(values)
    return values[math.ceil(probability * len(values)) - 1]
