"""Load reviewed product TDM workload fixtures (ADR-037 / T063)."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "docs" / "game-fixtures" / "tdm-workloads.json"

REQUIRED_COUNTERS = (
    "team1Kills", "team2Kills", "activeTicks", "respawns", "aiRays", "pathRequests",
    "shots", "damageEvents", "leaseAcquires", "querySamples", "contentBindings", "renderFrames",
)

MEASUREMENT_IDS = (
    "TDM-5v5-v1", "TDM-6v6-v1", "TDM-5v5-depot-v1", "TDM-6v6-depot-v1",
    "TDM-soak-6v6-compound-v1", "TDM-soak-6v6-depot-v1",
)


def load_workloads(path=None):
    document = json.loads((path or FIXTURE).read_text(encoding="utf-8"))
    if document.get("schemaVersion") != 2:
        raise ValueError("tdm-workloads schemaVersion must be 2 after ADR-037")
    by_id = {item["id"]: item for item in document["workloads"]}
    for workload_id in MEASUREMENT_IDS:
        if workload_id not in by_id:
            raise ValueError("Missing reviewed measurement workload " + workload_id)
    compound = by_id["TDM-5v5-v1"]
    depot = by_id["TDM-5v5-depot-v1"]
    if compound["mapId"] != "training-compound" or compound.get("seed", document["seed"]) != "460072030":
        raise ValueError("Compound measurement identity must remain training-compound / 460072030")
    if depot["mapId"] != "depot-yard" or depot["seed"] != "460072031":
        raise ValueError("Depot measurement identity must be depot-yard / 460072031")
    return document, by_id


def require_measurement(workload_id):
    _, by_id = load_workloads()
    if workload_id not in by_id or workload_id not in MEASUREMENT_IDS:
        raise ValueError("Unsupported product TDM workload: " + workload_id)
    item = by_id[workload_id]
    counters = item.get("counters") or []
    for name in REQUIRED_COUNTERS:
        if name not in counters:
            raise ValueError("Workload " + workload_id + " missing required counter " + name)
    return item
