#!/usr/bin/env python3
"""Build the shipped combat SFX bank and music bank from downloaded source recordings (ADR-096).

Recipe: Tools/audio-sources.json
  sources[]  id, url, file (under .local/audio-src/), license, note; archives are unpacked beside the file.
  clips[]    kind, variation, source (path under .local/audio-src/), start, end (seconds), gainDb, fadeIn,
             fadeOut, optional reverse/highpass/lowpass/pitch (rate factor)/pad (seconds of silence appended
             before filters so echoes ring out)/filters (extra ffmpeg audio filters, e.g. aecho),
             optional layers[] mixed in with the same fields plus delay (seconds), optional length (trim the
             mixed clip to this many seconds) and tailFade (fade-out seconds at the end of the mixed clip).
             A layer may replace source with generate {type: sweep|tone, from, to (Hz), duration, attack,
             decay (1/s)} for a synthesised layer (ADR-111 sub-bass drop). A source flagged local in
             sources[] is a repo-local WAV (for example a previous bank clip kept under .local/audio-src/kept/).
  music[]    cue, source, start, end, fadeIn, fadeOut, loopStartSeconds, loopEndSeconds.

Commands (run from the repo root):
  python Tools/build_audio_bank.py fetch     download + unpack every source (asks nothing; run only after review)
  python Tools/build_audio_bank.py analyze <path-under-audio-src> [--min-silence 0.08]
                                             print duration and non-silent regions to choose slice points
  python Tools/build_audio_bank.py build     render clips/music, write WAV/OGG + manifests into Content/Audio
  python Tools/build_audio_bank.py review    write Artifacts/audio-review/index.html to audition the bank
  python Tools/build_audio_bank.py check     verify manifests against the files on disk

Clips are 48 kHz 16-bit stereo WAV, peak-normalised per kind. Music is loudness-normalised and encoded as
Vorbis q6 at 48 kHz. Requires ffmpeg/ffprobe on PATH; 7-Zip for .7z archives.
"""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECIPE = ROOT / "Tools" / "audio-sources.json"
SRC = ROOT / ".local" / "audio-src"
COMBAT_OUT = ROOT / "Content" / "Audio" / "combat"
MUSIC_OUT = ROOT / "Content" / "Audio" / "music"
REVIEW_OUT = ROOT / "Artifacts" / "audio-review"
BANK_ID = "combat-audio-bank-v9-stereo"
MUSIC_BANK_ID = "music-bank-v1"
RATE = 48000

# Target sample peak per kind (dBFS). Gunfire and blasts sit at the top; Foley and UI below so the
# mixer's distance/channel gains, not the source files, decide the balance.
PEAK_DB = {
    "rifle_shot": -1.0, "pistol_shot": -1.0, "grenade_explode": -1.0, "grenade_explode_far": -3.0,
    "distant_battle": -6.0, "headshot": -3.0, "impact": -4.0, "damage_taken": -4.0, "death": -4.0,
    "knife_swing": -5.0, "knife_hit_flesh": -3.0, "knife_hit_hard": -3.0,
    "reload_start": -4.0, "reload_pistol": -4.0, "reload_complete": -4.0, "dry_fire": -6.0,
    "weapon_switch": -6.0, "grenade_pin": -4.5, "grenade_throw": -4.0, "grenade_bounce": -5.0,
    "footstep": -6.0, "bullet_flyby": -3.0, "hit_marker": -9.0, "kill_confirm": -6.0,
    "ui": -9.0, "ui_hover": -14.0, "ui_click": -9.0, "ui_back": -10.0, "ambient": -14.0,
    # ADR-102 bank v6 named-gun reports and handling Foley.
    "ak47_shot": -1.5, "ak102_shot": -3.0, "m16_shot": -1.5, "mk14_shot": -1.5, "tac338_shot": -2.0,
    "g17_shot": -1.5, "eagle_shot": -3.0, "bolt_cycle": -5.0, "reload_ak": -6.0, "reload_bolt": -5.0,
    # ADR-118 bank v9: four more named guns, the pump stroke and one shell into the tube.
    "m4a1_shot": -1.5, "m24_shot": -2.0, "mk16_shot": -1.5, "m870_shot": -1.0, "pump_cycle": -5.0, "reload_shell": -6.0,
}
MUSIC_LUFS = -16.0
CUES = ["menu_theme", "match_calm", "match_tension", "victory_stinger", "defeat_stinger", "draw_stinger", "results_loop"]


def ffmpeg(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["ffmpeg", "-hide_banner", "-nostdin", "-y", *args], capture_output=True, text=True, check=False)


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def load_recipe() -> dict:
    return json.loads(RECIPE.read_text(encoding="utf-8"))


def source_path(rel: str) -> Path:
    path = (SRC / rel).resolve()
    if SRC.resolve() not in path.parents:
        raise SystemExit("source escapes .local/audio-src: " + rel)
    if not path.is_file():
        raise SystemExit("missing source " + rel + " (run fetch first)")
    return path


# ---------------------------------------------------------------- fetch

def fetch(recipe: dict) -> None:
    SRC.mkdir(parents=True, exist_ok=True)
    for s in recipe["sources"]:
        target = (SRC / s["file"]).resolve()
        if SRC.resolve() not in target.parents:
            raise SystemExit("bad source file " + s["file"])
        target.parent.mkdir(parents=True, exist_ok=True)
        if s.get("local"):
            if not target.exists():
                print("LOCAL: restore", target, "-", s["note"])
            continue
        if not target.exists() and s.get("manual"):
            print("MANUAL: download", s["url"], "and save it as", target)
            continue
        if not target.exists():
            print("download", s["url"])
            req = urllib.request.Request(s["url"], headers={"User-Agent": "Mozilla/5.0 (audio bank build)"})
            with urllib.request.urlopen(req, timeout=120) as r, target.open("wb") as out:
                shutil.copyfileobj(r, out)
        if target.exists():
            unpack(target)


def unpack(archive: Path) -> None:
    dest = archive.with_suffix("")
    if archive.suffix.lower() not in (".zip", ".7z") or dest.exists():
        return
    dest.mkdir(parents=True)
    if archive.suffix.lower() == ".zip":
        with zipfile.ZipFile(archive) as z:
            for member in z.namelist():
                resolved = (dest / member).resolve()
                if dest.resolve() not in resolved.parents and resolved != dest.resolve():
                    raise SystemExit("zip entry escapes target: " + member)
            z.extractall(dest)
    else:
        seven = shutil.which("7z") or r"C:\Program Files\7-Zip\7z.exe"
        res = subprocess.run([seven, "x", "-y", "-o" + str(dest), str(archive)], capture_output=True, text=True)
        if res.returncode != 0:
            raise SystemExit("7z failed: " + res.stderr[-400:])


# ---------------------------------------------------------------- analyze

def analyze(rel: str, min_silence: float) -> None:
    path = source_path(rel)
    probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration:stream=sample_rate,channels",
                            "-of", "json", str(path)], capture_output=True, text=True, check=True)
    info = json.loads(probe.stdout)
    duration = float(info["format"]["duration"])
    res = ffmpeg("-i", str(path), "-af", "silencedetect=noise=-45dB:d=" + str(min_silence), "-f", "null", "-")
    starts = [float(x) for x in re.findall(r"silence_start: ([0-9.]+)", res.stderr)]
    ends = [float(x) for x in re.findall(r"silence_end: ([0-9.]+)", res.stderr)]
    regions, cursor = [], 0.0
    for i, s in enumerate(starts):
        if s - cursor > 0.01:
            regions.append((cursor, s))
        cursor = ends[i] if i < len(ends) else duration
    if duration - cursor > 0.01:
        regions.append((cursor, duration))
    print(json.dumps({"file": rel, "duration": round(duration, 3), "streams": info.get("streams"),
                      "sound": [[round(a, 3), round(b, 3)] for a, b in regions]}, indent=1))


# ---------------------------------------------------------------- build

def filter_chain(c: dict) -> list[str]:
    f = []
    if c.get("highpass"):
        f.append("highpass=f=%g" % c["highpass"])
    if c.get("lowpass"):
        f.append("lowpass=f=%g" % c["lowpass"])
    if c.get("reverse"):
        f.append("areverse")
    if c.get("gainDb"):
        f.append("volume=%gdB" % c["gainDb"])
    return f


def generate_expr(g: dict) -> tuple[str, float]:
    """aevalsrc expression for a synthesised tone or exponential sweep with an attack/decay envelope."""
    dur = float(g["duration"])
    f0 = float(g["from"])
    f1 = float(g.get("to", f0))
    attack = max(1e-4, float(g.get("attack", 0.005)))
    decay = float(g.get("decay", 0.0))
    if g["type"] == "sweep" and abs(f1 - f0) > 1e-6:
        import math
        k = math.log(f1 / f0)
        phase = "2*PI*%.9g*%.9g/%.9g*(exp(%.9g*t/%.9g)-1)" % (f0, dur, k, k, dur)
    elif g["type"] in ("sweep", "tone"):
        phase = "2*PI*%.9g*t" % f0
    else:
        raise SystemExit("unknown generate type " + str(g["type"]))
    env = "min(1,t/%.9g)*exp(-%.9g*t)" % (attack, decay)
    return "%s*sin(%s)" % (env, phase), dur


def render_segment(c: dict, out: Path) -> None:
    """One source segment (or generated layer) → 48 kHz stereo float WAV with fades (before normalisation)."""
    args = []
    if c.get("generate"):
        expr, dur = generate_expr(c["generate"])
        args += ["-f", "lavfi", "-i", "aevalsrc=exprs='%s':s=%d:d=%g:c=mono" % (expr, RATE, dur)]
    else:
        src = source_path(c["source"])
        if c.get("start") is not None:
            args += ["-ss", "%.4f" % c["start"]]
        if c.get("end") is not None:
            args += ["-to", "%.4f" % c["end"]]
        args += ["-i", str(src)]
        dur = (c["end"] - (c.get("start") or 0.0)) if c.get("end") is not None else None
    chain = ["aresample=%d" % RATE, "aformat=sample_fmts=flt:channel_layouts=stereo"]
    if c.get("pitch"):
        # Rate-shift (pitch and speed together), then back to the bank rate.
        chain += ["asetrate=%d" % round(RATE * float(c["pitch"])), "aresample=%d" % RATE]
        if dur:
            dur = dur / float(c["pitch"])
    chain += filter_chain(c)
    if c.get("fadeIn"):
        chain.append("afade=t=in:d=%g" % c["fadeIn"])
    if c.get("fadeOut") and dur:
        chain.append("afade=t=out:st=%g:d=%g" % (max(0.0, dur - c["fadeOut"]), c["fadeOut"]))
    # Pad and extra filters come after the fades so an echo tail rings past the segment's own fade-out.
    if c.get("pad"):
        chain.append("apad=pad_dur=%g" % c["pad"])
    if c.get("filters"):
        chain.append(c["filters"])
    res = ffmpeg(*args, "-af", ",".join(chain), "-c:a", "pcm_f32le", str(out))
    if res.returncode != 0:
        raise SystemExit("ffmpeg failed for %s: %s" % (c.get("source", "generated layer"), res.stderr[-600:]))



# ADR-111 addendum: every kind is normalised on the true float peak. volumedetect measures through 16-bit and
# saturates at 0 dBFS, so a layered float mix louder than full scale used to read as 0 dB and ship up to ~5 dB hot.
def peak_db(path: Path, float_peak: bool = True) -> float:
    res = ffmpeg("-i", str(path), "-af", "volumedetect", "-f", "null", "-")
    m = re.search(r"max_volume: (-?[0-9.]+) dB", res.stderr)
    level = float(m.group(1)) if m else 0.0
    if float_peak and level > -0.05:
        # volumedetect measures through 16-bit and saturates at full scale; a layered float mix can exceed it.
        res = ffmpeg("-i", str(path), "-af", "astats=measure_perchannel=none:measure_overall=Peak_level", "-f", "null", "-")
        m = re.search(r"Peak level dB: (-?[0-9.]+)", res.stderr)
        level = float(m.group(1)) if m else level
    return level


def build_clip(c: dict, out: Path, tmp: Path) -> None:
    base = tmp / "base.wav"
    render_segment(c, base)
    inputs = [base]
    for i, layer in enumerate(c.get("layers", [])):
        layer_out = tmp / ("layer%d.wav" % i)
        render_segment(layer, layer_out)
        if layer.get("delay"):
            delayed = tmp / ("layer%d_d.wav" % i)
            ms = int(layer["delay"] * 1000)
            res = ffmpeg("-i", str(layer_out), "-af", "adelay=%d|%d" % (ms, ms), "-c:a", "pcm_f32le", str(delayed))
            if res.returncode != 0:
                raise SystemExit(res.stderr[-400:])
            layer_out = delayed
        inputs.append(layer_out)
    mixed = base
    if len(inputs) > 1:
        mixed = tmp / "mixed.wav"
        args = []
        for p in inputs:
            args += ["-i", str(p)]
        res = ffmpeg(*args, "-filter_complex", "amix=inputs=%d:normalize=0:duration=longest" % len(inputs),
                     "-c:a", "pcm_f32le", str(mixed))
        if res.returncode != 0:
            raise SystemExit(res.stderr[-400:])
    post = []
    if c.get("length"):
        post.append("atrim=end=%g" % c["length"])
        if c.get("tailFade"):
            post.append("afade=t=out:st=%g:d=%g" % (max(0.0, c["length"] - c["tailFade"]), c["tailFade"]))
    if post:
        shaped = tmp / "shaped.wav"
        res = ffmpeg("-i", str(mixed), "-af", ",".join(post), "-c:a", "pcm_f32le", str(shaped))
        if res.returncode != 0:
            raise SystemExit(res.stderr[-400:])
        mixed = shaped
    gain = PEAK_DB.get(c["kind"], -3.0) - peak_db(mixed)
    res = ffmpeg("-i", str(mixed), "-af", "volume=%gdB" % gain, "-ar", str(RATE), "-ac", "2", "-c:a", "pcm_s16le",
                 "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact", str(out))
    if res.returncode != 0:
        raise SystemExit(res.stderr[-400:])


def build_music(m: dict, out: Path, tmp: Path) -> None:
    seg = tmp / "music.wav"
    render_segment(m, seg)
    res = ffmpeg("-i", str(seg), "-af", "loudnorm=I=%g:TP=-1.5:LRA=11" % MUSIC_LUFS, "-ar", str(RATE), "-ac", "2",
                 "-c:a", "libvorbis", "-q:a", "6", "-map_metadata", "-1", "-fflags", "+bitexact", str(out))
    if res.returncode != 0:
        raise SystemExit(res.stderr[-400:])


def source_note(recipe: dict, rel: str) -> tuple[str, str]:
    for s in recipe["sources"]:
        stem = s["file"]
        unpacked = str(Path(stem).with_suffix("")).replace("\\", "/")
        if rel == stem or rel.startswith(unpacked + "/"):
            return (s["note"] + " (" + s["url"] + ")" if s.get("url") else s["note"]), s["license"]
    raise SystemExit("clip source not listed in sources[]: " + rel)


def build(recipe: dict) -> None:
    tmp = SRC / "_tmp"
    tmp.mkdir(parents=True, exist_ok=True)
    COMBAT_OUT.mkdir(parents=True, exist_ok=True)
    keep = {"manifest.json"}
    entries = []
    seen = set()
    old_manifest = COMBAT_OUT / "manifest.json"
    previous = {}
    if old_manifest.exists():
        for e in json.loads(old_manifest.read_text(encoding="utf-8"))["clips"]:
            previous[(e["kind"], e["variation"])] = e
    for c in recipe["clips"]:
        if c.get("keep"):
            # Retain an already-shipped clip byte-for-byte with its recorded provenance.
            key = (c["kind"], c["variation"])
            old = previous.get(key)
            if old is None or sha256(COMBAT_OUT / old["file"]) != old["sha256"]:
                raise SystemExit("cannot keep %s/%d: not in the current bank" % key)
            if key in seen:
                raise SystemExit("duplicate clip %s/%d" % key)
            seen.add(key)
            entries.append(old)
            keep.add(old["file"])
            continue
        key = (c["kind"], c["variation"])
        if key in seen:
            raise SystemExit("duplicate clip %s/%d" % key)
        seen.add(key)
        if c["kind"] not in PEAK_DB:
            raise SystemExit("unknown kind " + c["kind"])
        name = "%s_%02d.wav" % key
        build_clip(c, COMBAT_OUT / name, tmp)
        note, lic = source_note(recipe, c["source"])
        generated = set()
        for layer in c.get("layers", []):
            if layer.get("generate"):
                generated.add(layer["generate"]["type"])
                continue
            ln, ll = source_note(recipe, layer["source"])
            if len(c["layers"]) > 3 and ln in note:
                continue  # many-layer recipes (ADR-111 blasts) list each source once
            note += " + " + ln
            lic = lic if ll == lic else lic + " + " + ll
        for kind in sorted(generated):
            note += " + generated %s layer (Tools/build_audio_bank.py)" % kind
        entries.append({"kind": c["kind"], "variation": c["variation"], "file": name,
                        "sha256": sha256(COMBAT_OUT / name), "source": note, "license": lic})
        keep.add(name)
        print("clip", name)
    for stale in COMBAT_OUT.glob("*.wav"):
        if stale.name not in keep:
            stale.unlink()
    (COMBAT_OUT / "manifest.json").write_text(json.dumps(
        {"bankId": BANK_ID, "sampleRate": RATE, "channels": 2, "clips": entries}, indent=2) + "\n", encoding="utf-8", newline="\n")

    if recipe.get("music"):
        MUSIC_OUT.mkdir(parents=True, exist_ok=True)
        tracks, keep_music = [], {"manifest.json"}
        for m in recipe["music"]:
            if m["cue"] not in CUES:
                raise SystemExit("unknown music cue " + m["cue"])
            name = m["cue"] + ".ogg"
            build_music(m, MUSIC_OUT / name, tmp)
            note, lic = source_note(recipe, m["source"])
            tracks.append({"cue": m["cue"], "file": name, "sha256": sha256(MUSIC_OUT / name),
                           "loopStartSeconds": m.get("loopStartSeconds", 0), "loopEndSeconds": m.get("loopEndSeconds", 0),
                           "source": note, "license": lic})
            keep_music.add(name)
            print("music", name)
        for stale in MUSIC_OUT.glob("*.ogg"):
            if stale.name not in keep_music:
                stale.unlink()
        (MUSIC_OUT / "manifest.json").write_text(json.dumps({"bankId": MUSIC_BANK_ID, "tracks": tracks}, indent=2) + "\n",
                                                encoding="utf-8", newline="\n")
    shutil.rmtree(tmp, ignore_errors=True)


# ---------------------------------------------------------------- review / check

def review() -> None:
    REVIEW_OUT.mkdir(parents=True, exist_ok=True)
    rows = []
    for folder, key in ((COMBAT_OUT, "clips"), (MUSIC_OUT, "tracks")):
        manifest = folder / "manifest.json"
        if not manifest.exists():
            continue
        for e in json.loads(manifest.read_text(encoding="utf-8"))[key]:
            label = e.get("kind", e.get("cue")) + (" #%d" % e["variation"] if "variation" in e else "")
            uri = (folder / e["file"]).resolve().as_uri()
            rows.append("<tr><td>%s</td><td><audio controls preload=none src='%s'></audio></td><td>%s</td></tr>"
                        % (label, uri, e["source"].replace("<", "&lt;")))
    html = ("<!doctype html><meta charset=utf-8><title>Audio review</title><style>body{font:14px system-ui;"
            "margin:16px;background:#111;color:#ddd}td{padding:4px 8px;border-bottom:1px solid #333}"
            "audio{height:28px}</style><h1>Audio bank review</h1><table>" + "".join(rows) + "</table>")
    (REVIEW_OUT / "index.html").write_text(html, encoding="utf-8")
    print(REVIEW_OUT / "index.html")


def check() -> int:
    bad = 0
    for folder, key in ((COMBAT_OUT, "clips"), (MUSIC_OUT, "tracks")):
        manifest = folder / "manifest.json"
        if not manifest.exists():
            continue
        for e in json.loads(manifest.read_text(encoding="utf-8"))[key]:
            path = folder / e["file"]
            if not path.exists() or sha256(path) != e["sha256"]:
                print("MISMATCH", path)
                bad += 1
    print("ok" if bad == 0 else "%d mismatches" % bad)
    return 1 if bad else 0


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    cmd = argv[1]
    if cmd == "fetch":
        fetch(load_recipe())
    elif cmd == "analyze" and len(argv) >= 3:
        ms = float(argv[argv.index("--min-silence") + 1]) if "--min-silence" in argv else 0.08
        analyze(argv[2], ms)
    elif cmd == "build":
        build(load_recipe())
    elif cmd == "review":
        review()
    elif cmd == "check":
        return check()
    else:
        print(__doc__)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
