#!/usr/bin/env python3
"""Slice CC0 recorded rifle/pistol takes into Content/Audio/combat shot variations.

Sources (OpenGameArt, CC0 — real outdoor target-shooting recordings):
- Tabasco \"Gunshot Sounds\" https://opengameart.org/content/gunshot-sounds
  cz.wav (CZ-52 pistol), sks.wav / mosin.wav (rifles)
- kurt \"Gunshots\" https://opengameart.org/content/gunshots
  22 Pistol.wav, 22 Magnum.wav
"""
from __future__ import annotations

import hashlib
import json
import math
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Content" / "Audio" / "combat"
RATE = 48000

SRC_TABASCO = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx\tabasco\sounds")
SRC_KURT_PISTOL = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx\oga_pistol")
SRC_KURT_MAGNUM = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx\oga_magnum")


def read_wav(path: Path) -> tuple[int, int, list[float]]:
    with wave.open(str(path), "rb") as w:
        ch = w.getnchannels()
        rate = w.getframerate()
        sw = w.getsampwidth()
        raw = w.readframes(w.getnframes())
    if sw != 2:
        raise SystemExit(f"expected 16-bit PCM: {path}")
    samples = [struct.unpack_from("<h", raw, i)[0] / 32768.0 for i in range(0, len(raw), 2)]
    return rate, ch, samples


def resample(mono: list[float], src: int, dst: int) -> list[float]:
    if src == dst:
        return mono
    n_dst = max(1, int(len(mono) * dst / src))
    out = [0.0] * n_dst
    for i in range(n_dst):
        pos = i * src / float(dst)
        i0 = int(pos)
        i1 = min(i0 + 1, len(mono) - 1)
        t = pos - i0
        out[i] = mono[i0] * (1.0 - t) + mono[i1] * t
    return out


def to_stereo_48k(rate: int, ch: int, interleaved: list[float]) -> list[float]:
    if ch == 1:
        left = interleaved
        right = interleaved[:]
    else:
        left = interleaved[0::2]
        right = interleaved[1::2]
    left = resample(left, rate, RATE)
    right = resample(right, rate, RATE)
    out = [0.0] * (len(left) * 2)
    for i in range(len(left)):
        out[i * 2] = left[i]
        out[i * 2 + 1] = right[i]
    return out


def mono_mix(stereo: list[float]) -> list[float]:
    return [0.5 * (stereo[i] + stereo[i + 1]) for i in range(0, len(stereo) - 1, 2)]


def find_onsets(mono: list[float], min_gap_sec: float, thr: float = 0.15) -> list[int]:
    win = max(32, int(0.004 * RATE))
    hop = max(16, win // 2)
    energies: list[tuple[int, float]] = []
    for i in range(0, len(mono) - win, hop):
        chunk = mono[i : i + win]
        e = math.sqrt(sum(x * x for x in chunk) / win)
        energies.append((i, e))
    peak = max((e for _, e in energies), default=0.0)
    gate = max(thr, peak * 0.42)
    onsets: list[int] = []
    min_gap = int(min_gap_sec * RATE)
    last = -10**9
    prev = 0.0
    for i, e in energies:
        if e >= gate and prev < gate and (i - last) >= min_gap:
            onsets.append(max(0, i - int(0.004 * RATE)))
            last = i
        prev = e
    return onsets


def extract_clip(stereo: list[float], start_frame: int, dur_sec: float) -> list[float] | None:
    n = int(dur_sec * RATE)
    if start_frame * 2 >= len(stereo):
        return None
    out = [0.0] * (n * 2)
    # No attack fade — keep the recorded transient; only fade the tail out.
    tail = int(0.05 * RATE)
    for i in range(n):
        src = start_frame + i
        if src * 2 + 1 >= len(stereo):
            break
        g = 1.0
        remain = n - i
        if remain < tail:
            g = remain / float(max(1, tail))
        out[i * 2] = stereo[src * 2] * g
        out[i * 2 + 1] = stereo[src * 2 + 1] * g

    # Reject quiet / mostly-tail slices: require strong attack in first 40 ms.
    attack = 0.0
    attack_n = int(0.04 * RATE) * 2
    for i in range(0, min(attack_n, len(out))):
        attack = max(attack, abs(out[i]))
    if attack < 0.08:
        return None

    peak = max((abs(x) for x in out), default=0.0)
    if peak < 1e-4:
        return None
    g = 0.94 / peak
    for i in range(len(out)):
        out[i] *= g
    return out


def write_wav(path: Path, stereo: list[float]) -> None:
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        raw = bytearray()
        for s in stereo:
            v = max(-1.0, min(1.0, s))
            raw += struct.pack("<h", int(v * 32767.0))
        w.writeframes(bytes(raw))


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def attack_score(stereo: list[float]) -> float:
    n = min(len(stereo), int(0.05 * RATE) * 2)
    return sum(abs(stereo[i]) for i in range(n)) / max(1, n)


def collect_clips(sources: list[tuple[Path, float, float]], count: int) -> list[list[float]]:
    candidates: list[list[float]] = []
    for path, min_gap, dur in sources:
        rate, ch, interleaved = read_wav(path)
        stereo = to_stereo_48k(rate, ch, interleaved)
        mono = mono_mix(stereo)
        for onset in find_onsets(mono, min_gap_sec=min_gap):
            clip = extract_clip(stereo, onset, dur)
            if clip is not None:
                candidates.append(clip)
    candidates.sort(key=attack_score, reverse=True)

    picked: list[list[float]] = []
    for c in candidates:
        # crude de-dupe: first 32 samples rounded
        fp = tuple(round(c[i], 2) for i in range(0, min(64, len(c)), 2))
        if any(tuple(round(p[i], 2) for i in range(0, min(64, len(p)), 2)) == fp for p in picked):
            continue
        picked.append(c)
        if len(picked) >= count:
            break
    if len(picked) < count:
        raise SystemExit(f"only found {len(picked)} usable shots, need {count}")
    return picked


def update_manifest(kind_files: dict[str, list[tuple[str, str, str]]]) -> None:
    man_path = OUT / "manifest.json"
    manifest = json.loads(man_path.read_text(encoding="utf-8"))
    new_clips = []
    for entry in manifest["clips"]:
        kind = entry["kind"]
        var = int(entry["variation"])
        if kind in kind_files and var < len(kind_files[kind]):
            file_name, digest, source = kind_files[kind][var]
            new_clips.append(
                {
                    "kind": kind,
                    "variation": var,
                    "file": file_name,
                    "sha256": digest,
                    "source": source,
                    "license": "CC0-1.0",
                }
            )
        else:
            path = OUT / entry["file"]
            entry = dict(entry)
            entry["sha256"] = sha256_file(path)
            new_clips.append(entry)
    manifest["clips"] = new_clips
    man_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    rifle = collect_clips(
        [
            (SRC_TABASCO / "sks.wav", 0.40, 0.78),
            (SRC_TABASCO / "mosin.wav", 0.50, 0.90),
        ],
        4,
    )
    pistol = collect_clips(
        [
            (SRC_TABASCO / "cz.wav", 0.28, 0.50),
            (SRC_KURT_PISTOL, 0.35, 0.52),
            (SRC_KURT_MAGNUM, 0.35, 0.58),
        ],
        4,
    )

    kind_files: dict[str, list[tuple[str, str, str]]] = {"rifle_shot": [], "pistol_shot": []}
    rifle_source = (
        "OpenGameArt CC0 real recordings — Tabasco Gunshot Sounds "
        "(https://opengameart.org/content/gunshot-sounds) SKS/Mosin Nagant rifle takes; "
        "sliced to 48 kHz stereo one-shots. Not from commercial games."
    )
    pistol_source = (
        "OpenGameArt CC0 real recordings — Tabasco CZ-52 "
        "(https://opengameart.org/content/gunshot-sounds) and kurt Gunshots "
        "(https://opengameart.org/content/gunshots); sliced to 48 kHz stereo one-shots. "
        "Not from commercial games."
    )

    for i, clip in enumerate(rifle):
        name = f"rifle_shot_{i:02d}.wav"
        path = OUT / name
        write_wav(path, clip)
        kind_files["rifle_shot"].append((name, sha256_file(path), rifle_source))
        print(f"wrote {name} attack={attack_score(clip):.3f}")
    for i, clip in enumerate(pistol):
        name = f"pistol_shot_{i:02d}.wav"
        path = OUT / name
        write_wav(path, clip)
        kind_files["pistol_shot"].append((name, sha256_file(path), pistol_source))
        print(f"wrote {name} attack={attack_score(clip):.3f}")

    update_manifest(kind_files)
    print("manifest updated")


if __name__ == "__main__":
    main()
