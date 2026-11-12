"""Same-frame CPU-start-to-Present upper bound; native work zeros stay unknown."""
import math

METHOD = "nativeCpuFrameStartToPresentUpperBound-v1"


def render_budget_samples(meta, frames):
    proof = {"method": METHOD, "kind": "Conservative upper bound, not exact active render work",
             "budgetMs": 8, "frames": len(frames), "clockAllowanceTicks": 2,
             "nativeWorkSamples": 0, "nativeWorkUnavailable": 0}
    frequency = meta.get("nativeCpuFrequency", 0)
    if meta.get("renderBudgetMethod") != METHOD:
        raise ValueError("Unrecognized render-budget method")
    if not meta.get("timestampFrequencyMatches") or frequency <= 0 or frequency != meta.get("stopwatchFrequency"):
        proof["unavailable"] = "Native render clock identity unavailable"
        return None, proof
    values = []
    previous_start = previous_present = 0
    for row in frames:
        start, present, submit = (int(row[key]) for key in ("nativeFrameStartTimestamp", "nativePresentCalledTimestamp", "nativeFirstSubmitTimestamp"))
        lower, upper, end = (int(row[key]) for key in ("lowerTimestamp", "upperTimestamp", "endTimestamp"))
        if row["ambiguous"].lower() != "false" or start == 0 or present == 0:
            proof["unavailable"] = "Native start/Present endpoint missing or ambiguous; no samples filled or omitted"
            return None, proof
        if not lower < start <= upper < end or not start < present <= end + 30 * frequency:
            raise ValueError("Invalid native rendering timestamp order/range")
        if start <= previous_start or present <= previous_present:
            raise ValueError("Reordered or duplicated native rendering endpoints")
        if submit and not start <= submit <= present:
            raise ValueError("Native GPU submission falls outside the enclosing CPU-to-Present interval")
        value = (present - start + 2) * 1000 / frequency
        native = float(row["renderMs"])
        if not math.isfinite(native) or native < -1:
            raise ValueError("Invalid native render-work sample")
        if native > 0:
            proof["nativeWorkSamples"] += 1
            if native > value:
                raise ValueError("Observed active render work exceeds its proposed enclosing interval")
        else:
            proof["nativeWorkUnavailable"] += 1
        values.append(value)
        previous_start, previous_present = start, present
    if not values or proof["nativeWorkSamples"] == 0:
        proof["unavailable"] = "No positive native render-work control to validate containment"
        return None, proof
    proof["validEnclosingIntervals"] = len(values)
    proof["containmentChecksPassed"] = proof["nativeWorkSamples"]
    proof["clockFrequency"] = frequency
    return values, proof
