#!/usr/bin/env python3
"""Add knife swing / grenade throw / grenade explode clips to Content/Audio/combat.

Sources:
- OpenGameArt swing.wav (CC0 melee swings, long take) → knife_swing_*
- Authored throw whoosh from filtered noise (stereo) → grenade_throw_*
- OpenGameArt explosion.wav + Tabasco shotty.wav (CC0) → grenade_explode_*
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

SWING = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx2\swing.wav")
EXPL = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx2\expl2")
SHOTTY = Path(r"C:\Users\Max\AppData\Local\Temp\shooter-sfx\tabasco\sounds\shotty.wav")


def read_wav(path: Path) -> tuple[int, int, list[float]]:
    with wave.open(str(path), "rb") as w:
        ch, rate, sw = w.getnchannels(), w.getframerate(), w.getsampwidth()
        raw = w.readframes(w.getnframes())
    if sw != 2:
        raise SystemExit(f"16-bit required: {path}")
    samples = [struct.unpack_from("<h", raw, i)[0] / 32768.0 for i in range(0, len(raw), 2)]
    return rate, ch, samples


def resample(mono: list[float], src: int, dst: int) -> list[float]:
    if src == dst:
        return mono
    n = max(1, int(len(mono) * dst / src))
    out = [0.0] * n
    for i in range(n):
        pos = i * src / float(dst)
        i0 = int(pos)
        i1 = min(i0 + 1, len(mono) - 1)
        t = pos - i0
        out[i] = mono[i0] * (1 - t) + mono[i1] * t
    return out


def to_stereo_48k(rate: int, ch: int, interleaved: list[float]) -> list[float]:
    if ch == 1:
        left = interleaved
        right = list(interleaved)
    else:
        left = interleaved[0::2]
        right = interleaved[1::2]
    left = resample(left, rate, RATE)
    right = resample(right, rate, RATE)
    # mild Haas if mono-sourced
    if ch == 1:
        delay = 12
        right = ([0.0] * delay) + right[:-delay] if len(right) > delay else right
    out = [0.0] * (len(left) * 2)
    for i in range(len(left)):
        out[i * 2] = left[i]
        out[i * 2 + 1] = right[i] if i < len(right) else left[i]
    return out


def mono_mix(stereo: list[float]) -> list[float]:
    return [0.5 * (stereo[i] + stereo[i + 1]) for i in range(0, len(stereo) - 1, 2)]


def find_onsets(mono: list[float], gap: float, thr: float = 0.08) -> list[int]:
    win = max(32, int(0.005 * RATE))
    hop = win // 2
    es = []
    for i in range(0, len(mono) - win, hop):
        chunk = mono[i : i + win]
        e = math.sqrt(sum(x * x for x in chunk) / win)
        es.append((i, e))
    peak = max((e for _, e in es), default=0.0)
    gate = max(thr, peak * 0.35)
    out, last, prev = [], -10**9, 0.0
    min_gap = int(gap * RATE)
    for i, e in es:
        if e >= gate and prev < gate and i - last >= min_gap:
            out.append(max(0, i - int(0.004 * RATE)))
            last = i
        prev = e
    return out


def extract(stereo: list[float], start: int, dur: float) -> list[float] | None:
    n = int(dur * RATE)
    out = [0.0] * (n * 2)
    tail = int(0.05 * RATE)
    for i in range(n):
        src = start + i
        if src * 2 + 1 >= len(stereo):
            break
        g = 1.0
        if n - i < tail:
            g = (n - i) / float(max(1, tail))
        out[i * 2] = stereo[src * 2] * g
        out[i * 2 + 1] = stereo[src * 2 + 1] * g
    attack = max((abs(out[i]) for i in range(0, min(len(out), int(0.04 * RATE) * 2))), default=0.0)
    if attack < 0.05:
        return None
    peak = max((abs(x) for x in out), default=0.0)
    if peak < 1e-4:
        return None
    g = 0.92 / peak
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


def xorshift(state: int) -> tuple[int, float]:
    state ^= (state << 13) & 0xFFFFFFFF
    state ^= (state >> 17) & 0xFFFFFFFF
    state ^= (state << 5) & 0xFFFFFFFF
    return state, (state & 0xFFFFFF) / float(0x1000000)


def bake_throw(variation: int) -> list[float]:
    n = int(RATE * 0.30)
    left = [0.0] * n
    right = [0.0] * n
    rng = 0x7A000001 ^ (variation * 0x9E3779B9)
    for i in range(n):
        t = i / float(n)
        env = math.sin(math.pi * min(1.0, t * 1.6)) * math.exp(-2.8 * t)
        rng, u = xorshift(rng)
        nse = (u * 2 - 1) * 0.55
        # band-ish: mix raw + smoothed
        tone = math.sin(2 * math.pi * (420 + variation * 35) * (i / RATE)) * 0.15
        left[i] = (nse * 0.7 + tone) * env
        right[i] = (nse * 0.55 + tone * 0.9) * env * 0.95
    # metallic click near start
    for i in range(int(0.04 * RATE)):
        t = i / RATE
        env = math.exp(-40 * t)
        click = math.sin(2 * math.pi * (1800 + variation * 90) * t) * 0.35 * env
        left[i] += click
        right[i] += click * 0.85
    peak = max(max(abs(x) for x in left), max(abs(x) for x in right))
    g = 0.75 / max(peak, 1e-6)
    out = [0.0] * (n * 2)
    for i in range(n):
        out[i * 2] = left[i] * g
        out[i * 2 + 1] = right[i] * g
    return out


def pick_clips(path: Path, count: int, gap: float, dur: float) -> list[list[float]]:
    rate, ch, interleaved = read_wav(path)
    stereo = to_stereo_48k(rate, ch, interleaved)
    mono = mono_mix(stereo)
    cands = []
    for onset in find_onsets(mono, gap=gap):
        clip = extract(stereo, onset, dur)
        if clip is not None:
            cands.append(clip)
    cands.sort(key=lambda c: sum(abs(x) for x in c[: int(0.05 * RATE) * 2]), reverse=True)
    picked = []
    for c in cands:
        fp = tuple(round(c[i], 2) for i in range(0, min(48, len(c)), 2))
        if any(tuple(round(p[i], 2) for i in range(0, min(48, len(p)), 2)) == fp for p in picked):
            continue
        picked.append(c)
        if len(picked) >= count:
            break
    if len(picked) < count:
        # pad with whole-file trimmed starts
        while len(picked) < count:
            start = int((len(picked) * 0.9) * RATE) % max(1, len(stereo) // 2 - int(dur * RATE))
            clip = extract(stereo, start, dur)
            if clip is not None:
                picked.append(clip)
            else:
                picked.append(cands[len(picked) % len(cands)] if cands else bake_throw(len(picked)))
    return picked[:count]


def mix_explode(variation: int) -> list[float]:
    # Layer OGA explosion + shotgun boom with slight offset per variation.
    rate_a, ch_a, a = read_wav(EXPL)
    rate_b, ch_b, b = read_wav(SHOTTY)
    sa = to_stereo_48k(rate_a, ch_a, a)
    sb = to_stereo_48k(rate_b, ch_b, b)
    n = int(RATE * 0.95)
    out = [0.0] * (n * 2)
    delay = int((0.008 + variation * 0.003) * RATE)
    for i in range(n):
        ia = i * 2
        ib = (i - delay) * 2 if i >= delay else -1
        l = sa[ia] * 0.85 if ia + 1 < len(sa) else 0.0
        r = sa[ia + 1] * 0.85 if ia + 1 < len(sa) else 0.0
        if ib >= 0 and ib + 1 < len(sb):
            l += sb[ib] * 0.75
            r += sb[ib + 1] * 0.75
        # extra low thump
        t = i / RATE
        th = math.sin(2 * math.pi * (42 + variation) * t) * math.exp(-6.0 * t) * 0.35
        out[ia] = l + th
        out[ia + 1] = r + th * 0.9
    peak = max(abs(x) for x in out)
    g = 0.95 / max(peak, 1e-6)
    for i in range(len(out)):
        out[i] *= g
    return out


def update_manifest(entries: dict[str, list[tuple[str, str, str]]]) -> None:
    man_path = OUT / "manifest.json"
    manifest = json.loads(man_path.read_text(encoding="utf-8"))
    manifest["bankId"] = "combat-audio-bank-v4-stereo"
    # drop old knife/grenade entries if re-running
    kept = [c for c in manifest["clips"] if c["kind"] not in entries]
    for kind, rows in entries.items():
        for file_name, digest, source in rows:
            var = int(file_name.rsplit("_", 1)[-1].split(".")[0])
            kept.append(
                {
                    "kind": kind,
                    "variation": var,
                    "file": file_name,
                    "sha256": digest,
                    "source": source,
                    "license": "CC0-1.0",
                }
            )
    # stable kind order
    order = [
        "rifle_shot",
        "pistol_shot",
        "impact",
        "damage_taken",
        "footstep",
        "reload_start",
        "reload_complete",
        "death",
        "ui",
        "ambient",
        "headshot",
        "knife_swing",
        "grenade_throw",
        "grenade_explode",
    ]
    kept.sort(key=lambda c: (order.index(c["kind"]) if c["kind"] in order else 99, c["variation"]))
    # refresh hashes for kept non-replaced files
    for c in kept:
        if c["kind"] not in entries:
            c["sha256"] = sha256_file(OUT / c["file"])
    manifest["clips"] = kept
    man_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    knife = pick_clips(SWING, 4, gap=0.55, dur=0.28)
    throws = [bake_throw(i) for i in range(4)]
    explodes = [mix_explode(i) for i in range(4)]

    entries: dict[str, list[tuple[str, str, str]]] = {
        "knife_swing": [],
        "grenade_throw": [],
        "grenade_explode": [],
    }
    knife_src = (
        "OpenGameArt CC0 melee swing take (swing.wav); sliced to 48 kHz stereo knife one-shots. "
        "Not from commercial games."
    )
    throw_src = "Project-authored stereo throw/whoosh PCM for grenade ProjectileSpawned (CC0-1.0)."
    boom_src = (
        "OpenGameArt CC0 explosion.wav layered with Tabasco 10-gauge shotgun boom "
        "(https://opengameart.org/content/gunshot-sounds); 48 kHz stereo. Not from commercial games."
    )

    for i, clip in enumerate(knife):
        name = f"knife_swing_{i:02d}.wav"
        write_wav(OUT / name, clip)
        entries["knife_swing"].append((name, sha256_file(OUT / name), knife_src))
        print("wrote", name)
    for i, clip in enumerate(throws):
        name = f"grenade_throw_{i:02d}.wav"
        write_wav(OUT / name, clip)
        entries["grenade_throw"].append((name, sha256_file(OUT / name), throw_src))
        print("wrote", name)
    for i, clip in enumerate(explodes):
        name = f"grenade_explode_{i:02d}.wav"
        write_wav(OUT / name, clip)
        entries["grenade_explode"].append((name, sha256_file(OUT / name), boom_src))
        print("wrote", name)

    update_manifest(entries)
    print("manifest updated ->", OUT / "manifest.json")


if __name__ == "__main__":
    main()
