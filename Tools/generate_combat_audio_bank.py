#!/usr/bin/env python3
"""Author stereo 48 kHz combat WAV bank + manifest (T089 / ADR-055).

Produces project-authored Foley-style PCM (noise/transient layers, stereo width),
not MIDI tones. Output: Content/Audio/combat/*.wav + manifest.json
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
CHANNELS = 2


def xorshift(state: int) -> tuple[int, float]:
    state ^= (state << 13) & 0xFFFFFFFF
    state ^= (state >> 17) & 0xFFFFFFFF
    state ^= (state << 5) & 0xFFFFFFFF
    return state, (state & 0xFFFFFF) / float(0x1000000)


def write_wav(path: Path, interleaved: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = len(interleaved) // 2
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        raw = bytearray()
        for s in interleaved:
            v = max(-1.0, min(1.0, s))
            raw += struct.pack("<h", int(v * 32767.0))
        w.writeframes(bytes(raw))


def normalize(mono: list[float], peak: float) -> None:
    m = max(abs(x) for x in mono) if mono else 0.0
    if m < 1e-8:
        return
    g = peak / m
    for i in range(len(mono)):
        mono[i] *= g


def noise_burst(buf: list[float], rng: int, start: float, dur: float, amp: float, low: float, high: float) -> int:
    s0 = int(start * RATE)
    n = max(1, int(dur * RATE))
    lp = 0.0
    hp = 0.0
    lp_c = max(0.01, min(0.95, low / (RATE * 0.5)))
    hp_c = max(0.01, min(0.95, high / (RATE * 0.5)))
    for i in range(n):
        idx = s0 + i
        if idx < 0 or idx >= len(buf):
            continue
        t = i / float(n)
        env = math.exp(-4.2 * t) * (1.0 - t * 0.15)
        rng, u = xorshift(rng)
        sample = u * 2.0 - 1.0
        lp += lp_c * (sample - lp)
        hp += hp_c * ((sample - lp) - hp)
        buf[idx] += hp * amp * env
    return rng


def thump(buf: list[float], start: float, dur: float, freq: float, amp: float) -> None:
    s0 = int(start * RATE)
    n = max(1, int(dur * RATE))
    for i in range(n):
        idx = s0 + i
        if idx < 0 or idx >= len(buf):
            continue
        t = i / float(RATE)
        env = math.exp(-12.0 * (i / float(n)))
        pitch = freq * (1.0 - 0.35 * (i / float(n)))
        buf[idx] += math.sin(2 * math.pi * pitch * t) * amp * env


def metallic(buf: list[float], rng: int, start: float, dur: float, freq: float, amp: float) -> int:
    s0 = int(start * RATE)
    n = max(1, int(dur * RATE))
    for i in range(n):
        idx = s0 + i
        if idx < 0 or idx >= len(buf):
            continue
        t = i / float(RATE)
        env = math.exp(-18.0 * (i / float(n)))
        tone = math.sin(2 * math.pi * freq * t)
        over = math.sin(2 * math.pi * freq * 2.7 * t) * 0.4
        rng, u = xorshift(rng)
        nse = (u * 2.0 - 1.0) * 0.25
        buf[idx] += (tone + over + nse) * amp * env
    return rng


def stereoize(mono: list[float], delay_sec: float, variation: int) -> list[float]:
    delay = max(1, int(delay_sec * RATE) + variation)
    out: list[float] = [0.0] * (len(mono) * 2)
    for i, sample in enumerate(mono):
        left = sample
        right = mono[i - delay] * 0.92 if i >= delay else sample * 0.72
        # mild width EQ difference
        out[i * 2] = left * 1.02
        out[i * 2 + 1] = right * 0.98
    return out


def bake_rifle(v: int) -> list[float]:
    n = int(RATE * 0.55)
    buf = [0.0] * n
    rng = 0xA11F1E01 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.02, 1.0, 1800, 11000)
    rng = noise_burst(buf, rng, 0.002, 0.14, 0.78, 180, 3200)
    thump(buf, 0.0, 0.09, 62 + v * 3, 0.62)
    rng = metallic(buf, rng, 0.01, 0.07, 2100 + v * 45, 0.4)
    rng = noise_burst(buf, rng, 0.05, 0.45, 0.32, 100, 1500)
    normalize(buf, 0.94)
    return stereoize(buf, 0.0004, v)


def bake_pistol(v: int) -> list[float]:
    n = int(RATE * 0.38)
    buf = [0.0] * n
    rng = 0xB1570101 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.014, 0.98, 2400, 12000)
    rng = noise_burst(buf, rng, 0.001, 0.08, 0.58, 350, 4000)
    thump(buf, 0.0, 0.05, 92 + v * 4, 0.42)
    rng = metallic(buf, rng, 0.008, 0.05, 2700 + v * 55, 0.48)
    rng = noise_burst(buf, rng, 0.03, 0.3, 0.2, 160, 1700)
    normalize(buf, 0.9)
    return stereoize(buf, 0.0003, v)


def bake_impact(v: int) -> list[float]:
    n = int(RATE * 0.24)
    buf = [0.0] * n
    rng = 0x1A7AC701 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.03, 0.88, 700, 6500)
    thump(buf, 0.0, 0.1, 105 + v * 8, 0.5)
    rng = noise_burst(buf, rng, 0.01, 0.18, 0.24, 180, 2300)
    normalize(buf, 0.82)
    return stereoize(buf, 0.00025, v)


def bake_damage(v: int) -> list[float]:
    n = int(RATE * 0.18)
    buf = [0.0] * n
    rng = 0xD4A4A6E1 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.022, 0.92, 450, 4200)
    thump(buf, 0.0, 0.08, 68 + v * 5, 0.7)
    rng = metallic(buf, rng, 0.004, 0.045, 1500, 0.28)
    normalize(buf, 0.86)
    return stereoize(buf, 0.00022, v)


def bake_headshot(v: int) -> list[float]:
    n = int(RATE * 0.2)
    buf = [0.0] * n
    rng = 0xCEA05001 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.014, 1.0, 2600, 13000)
    rng = metallic(buf, rng, 0.001, 0.05, 3300 + v * 70, 0.75)
    thump(buf, 0.0, 0.055, 80 + v * 4, 0.38)
    rng = noise_burst(buf, rng, 0.008, 0.13, 0.3, 550, 4800)
    normalize(buf, 0.92)
    return stereoize(buf, 0.00028, v)


def bake_footstep(v: int) -> list[float]:
    n = int(RATE * 0.12)
    buf = [0.0] * n
    rng = 0xF0075701 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.04, 0.6, 70, 1000)
    thump(buf, 0.0, 0.055, 52 + v * 4, 0.45)
    rng = noise_burst(buf, rng, 0.015, 0.09, 0.2, 180, 1900)
    normalize(buf, 0.58)
    return stereoize(buf, 0.00045, v)


def bake_reload_start(v: int) -> list[float]:
    n = int(RATE * 0.3)
    buf = [0.0] * n
    rng = 0x5E10AD01 ^ (v * 0x9E3779B9)
    rng = metallic(buf, rng, 0.0, 0.09, 1350 + v * 30, 0.6)
    rng = noise_burst(buf, rng, 0.04, 0.17, 0.28, 280, 2600)
    rng = metallic(buf, rng, 0.13, 0.11, 1850, 0.4)
    normalize(buf, 0.72)
    return stereoize(buf, 0.0002, v)


def bake_reload_complete(v: int) -> list[float]:
    n = int(RATE * 0.34)
    buf = [0.0] * n
    rng = 0x5E10AC01 ^ (v * 0x9E3779B9)
    rng = metallic(buf, rng, 0.0, 0.08, 1650 + v * 25, 0.55)
    rng = noise_burst(buf, rng, 0.05, 0.13, 0.32, 220, 2300)
    rng = metallic(buf, rng, 0.17, 0.1, 2350, 0.65)
    thump(buf, 0.19, 0.07, 88, 0.28)
    normalize(buf, 0.74)
    return stereoize(buf, 0.0002, v)


def bake_death(v: int) -> list[float]:
    n = int(RATE * 0.48)
    buf = [0.0] * n
    rng = 0xDE47A001 ^ (v * 0x9E3779B9)
    rng = noise_burst(buf, rng, 0.0, 0.09, 0.55, 90, 1300)
    thump(buf, 0.02, 0.22, 42 + v * 3, 0.6)
    rng = noise_burst(buf, rng, 0.1, 0.32, 0.22, 70, 900)
    normalize(buf, 0.68)
    return stereoize(buf, 0.00055, v)


def bake_ui(v: int) -> list[float]:
    n = int(RATE * 0.08)
    buf = [0.0] * n
    f0 = 540 + v * 40
    for i in range(n):
        t = i / float(RATE)
        env = max(0.0, 1.0 - t / 0.08) ** 2
        buf[i] = math.sin(2 * math.pi * f0 * t) * 0.38 * env + math.sin(2 * math.pi * f0 * 1.5 * t) * 0.12 * env
    normalize(buf, 0.52)
    return stereoize(buf, 0.00012, v)


def bake_ambient(v: int) -> list[float]:
    n = RATE * 2
    buf = [0.0] * n
    rng = 0xA4B1E001 ^ (v * 0x9E3779B9)
    for i in range(n):
        t = i / float(RATE)
        wind = math.sin(2 * math.pi * 0.15 * t) * 0.02
        rng, u = xorshift(rng)
        hiss = (u - 0.5) * 0.02
        hum = math.sin(2 * math.pi * 55 * t) * 0.009
        buf[i] = wind + hiss + hum
    fade = RATE // 20
    for i in range(fade):
        w = i / float(fade)
        a = buf[i]
        b = buf[n - fade + i]
        buf[i] = b * (1 - w) + a * w
        buf[n - fade + i] = a * (1 - w) + b * w
    normalize(buf, 0.14)
    return stereoize(buf, 0.0015, v)


CUES = [
    ("rifle_shot", 4, bake_rifle),
    ("pistol_shot", 4, bake_pistol),
    ("impact", 4, bake_impact),
    ("damage_taken", 4, bake_damage),
    ("footstep", 4, bake_footstep),
    ("reload_start", 4, bake_reload_start),
    ("reload_complete", 4, bake_reload_complete),
    ("death", 4, bake_death),
    ("ui", 4, bake_ui),
    ("ambient", 1, bake_ambient),
    ("headshot", 4, bake_headshot),
]


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    clips = []
    for kind, count, baker in CUES:
        for v in range(count):
            name = f"{kind}_{v:02d}.wav"
            path = OUT / name
            write_wav(path, baker(v))
            digest = sha256_file(path)
            clips.append(
                {
                    "kind": kind,
                    "variation": v,
                    "file": name,
                    "sha256": digest,
                    "source": "project-authored stereo Foley PCM render (T089); non-commercial; not extracted from any commercial game",
                    "license": "CC0-1.0",
                }
            )
            print(f"wrote {path.name} {digest[:12]}…")

    manifest = {
        "bankId": "combat-audio-bank-v3-stereo",
        "sampleRate": RATE,
        "channels": CHANNELS,
        "clips": clips,
    }
    man_path = OUT / "manifest.json"
    man_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"manifest {man_path} ({len(clips)} clips)")


if __name__ == "__main__":
    main()
