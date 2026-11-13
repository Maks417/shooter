"""Optional fresh Unity rendering counters. Inventory and visible geometry are separate quantities."""
import math
import re

METHOD = "ProfilerRecorderResetStartEachUpdate-v1"
COUNTERS = {
    "drawCalls": ("drawCallsCount", "Draw Calls Count", "Render", "Count"),
    "renderedTriangles": ("renderedTrianglesCount", "Triangles Count", "Render", "Count"),
    "graphicsUsedMemoryBytes": ("graphicsUsedMemoryCount", "Gfx Used Memory", "Memory", "Bytes"),
}
# Explicit native labels observed in the locked Editor inventory. Release descriptors must match too.
NATIVE_CATEGORIES = {"Render": "Render", "Memory": "Memory"}
# Even UInt64 is bounded to signed64 because ProfilerRecorderSample.Value is a normalized long.
TYPE_MAXIMUM = {"Int32": 2**31 - 1, "UInt32": 2**32 - 1, "Int64": 2**63 - 1, "UInt64": 2**63 - 1}
SCOPE = ("Fresh engine ProfilerRecorder counters per Update interval. Triangles include engine-reported "
         "render work/passes, not distinct visible triangles. Gfx Used Memory is engine accounting, "
         "not dedicated VRAM, residency or total driver allocation.")


def analyze(meta, rows):
    result = {column: None for column in COUNTERS}
    unavailable = []
    if meta.get("renderCounterMethod") is None:
        return result, [column + ": optional fresh counter collector not present" for column in COUNTERS]
    if meta["renderCounterMethod"] != METHOD:
        raise ValueError("Unsupported render counter freshness method")
    proofs = meta.get("renderCounters")
    if not isinstance(proofs, list) or len(proofs) != len(COUNTERS):
        raise ValueError("Incomplete render counter identity proofs")
    by_column = {proof.get("column"): proof for proof in proofs if isinstance(proof, dict)}
    if set(by_column) != set(COUNTERS):
        raise ValueError("Duplicate/unknown render counter identity")
    if not rows or len(rows) > 262144 or meta["frames"] != len(rows):
        raise ValueError("Invalid render counter frame count")
    for column, (count_column, name, category, unit) in COUNTERS.items():
        proof = by_column[column]
        if proof.get("requestedCategory") != category or proof.get("name") != name:
            raise ValueError("Render counter requested identity mismatch")
        if type(proof.get("identityValidated")) is not bool or type(proof.get("available")) is not bool:
            raise ValueError("Invalid render counter availability flag")
        identity = proof["identityValidated"]
        if identity and (proof.get("unit") != unit or proof.get("category") != NATIVE_CATEGORIES[category] or
                         proof.get("expectedNativeCategoryName") != NATIVE_CATEGORIES[category] or
                         proof.get("dataType") not in TYPE_MAXIMUM or "Counter" not in (proof.get("flags") or "").split(", ")):
            raise ValueError("Validated render counter descriptor mismatch")
        values = []
        invalid = 0
        for row in rows:
            try:
                raw_value, raw_count = str(row[column]), str(row[count_column])
                if re.fullmatch(r"-?[0-9]+", raw_value) is None or re.fullmatch(r"[0-9]+", raw_count) is None:
                    raise ValueError("Counter samples must be explicit integer fields")
                value, count = int(raw_value), int(raw_count)
            except (KeyError, TypeError, ValueError) as error:
                raise ValueError("Missing/malformed raw render counter sample") from error
            if value < -1 or value > 2**63 - 1 or count > 2**63 - 1:
                raise ValueError("Invalid raw render counter sentinel/count")
            if identity and value > TYPE_MAXIMUM[proof["dataType"]]:
                raise ValueError("Raw render counter exceeds declared integer datatype")
            invalid += value <= 0 or count != 1
            values.append(value)
        if (type(proof.get("capturedFrames")) is not int or proof["capturedFrames"] != len(rows) or
                type(proof.get("invalidFrames")) is not int or proof["invalidFrames"] != invalid):
            raise ValueError("Render counter coverage proof mismatch")
        available = identity and invalid == 0
        if proof["available"] != available:
            raise ValueError("Render counter availability contradicts raw coverage")
        if not available:
            reason = proof.get("unavailable")
            if not isinstance(reason, str) or not reason:
                raise ValueError("Unavailable render counter lacks reason")
            unavailable.append(column + ": " + reason)
            continue
        if proof.get("unavailable") is not None:
            raise ValueError("Available render counter unexpectedly has an unavailable reason")
        values.sort()
        result[column] = {"p50": values[math.ceil(.5 * len(values)) - 1],
                          "p95": values[math.ceil(.95 * len(values)) - 1], "max": values[-1], "samples": len(values)}
    return result, unavailable
