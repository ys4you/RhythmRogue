"""
Beat Map Generator for RhythmRogue
===================================
Analyzes an audio file and exports beat markers as JSON
that can be imported into a SongBeatMap ScriptableObject.

Requirements:
    pip install librosa soundfile numpy   (scipy ships with librosa)

Usage:
    python beatmap_generator.py my_song.wav --bpm 135

If you omit --bpm, librosa will auto-detect it.

Output: my_song_beatmap.json with markers and sections.

INSTRUMENT STEMS
----------------
By default the generator splits the track into instrument stems and tags every
marker with the stem it came from (Drums / Bass / Melody). All streams are written
into the one JSON. In Unity, an enemy's `chartInstrument` selector then chooses which
stream drives its chart - so the chart can "follow the bass", "follow the drums", etc.,
and you can switch instruments in the Inspector with no regeneration. Selecting 'All'
(the default) uses every marker.

Separation uses librosa HPSS (percussive vs harmonic) plus a low/high split of the
harmonic part for Bass vs Melody. This is an approximation, not true per-instrument
isolation. Pass --no-stems to fall back to the original single blended stream (every
marker tagged 'All'), which is the proven behaviour if a track separates poorly.
"""

import argparse
import json
import numpy as np

try:
    import librosa
except ImportError:
    print("Install librosa first: pip install librosa soundfile numpy")
    exit(1)


# =====================================================================
# SPECTRAL TYPING (cosmetic marker.type hint: Kick / Snare / HiHat / ...)
# =====================================================================

def _drum_type(S, freqs, frame_idx, low_mask, mid_mask, high_mask):
    """Classify a drum-ish onset by dominant frequency band at its frame."""
    if frame_idx >= S.shape[1]:
        return "Kick"
    low = float(np.mean(S[low_mask, frame_idx])) if low_mask.any() else 0.0
    mid = float(np.mean(S[mid_mask, frame_idx])) if mid_mask.any() else 0.0
    high = float(np.mean(S[high_mask, frame_idx])) if high_mask.any() else 0.0
    total = low + mid + high
    if total <= 0:
        return "Accent"
    lr, mr, hr = low / total, mid / total, high / total
    if lr > 0.45:
        return "Kick"
    if mr > 0.4:
        return "Snare"
    if hr > 0.4:
        return "HiHat"
    return "Accent"


# =====================================================================
# STEM SEPARATION
# =====================================================================

def _separate_stems(y, sr, bass_cutoff_hz=250.0):
    """
    Split into three signals via HPSS + a low/high split of the harmonic part:
        percussive  -> Drums
        harmonic low -> Bass
        harmonic high -> Melody
    Returns (y_drums, y_bass, y_melody). y_bass may be None if scipy is missing,
    in which case the full harmonic signal is returned as Melody.
    """
    print("Separating stems (HPSS)...")
    y_harm, y_perc = librosa.effects.hpss(y)
    try:
        from scipy.signal import butter, sosfiltfilt

        def band(sig, btype):
            sos = butter(4, bass_cutoff_hz, btype=btype, fs=sr, output="sos")
            return sosfiltfilt(sos, sig).astype(np.float32)

        y_bass = band(y_harm, "low")
        y_mel = band(y_harm, "high")
    except Exception as e:  # scipy should ship with librosa; degrade gracefully if not
        print(f"  Band split unavailable ({e}); harmonic used as Melody, no separate Bass.")
        y_bass = None
        y_mel = y_harm
    return y_perc, y_bass, y_mel


# =====================================================================
# PER-SIGNAL ONSET MARKERS (tagged with an instrument)
# =====================================================================

def _onset_markers(y_sig, sr, sec_per_beat, sensitivity, instrument, fixed_type=None):
    """
    Detect onsets in one signal and return marker dicts tagged `instrument`.
    If fixed_type is None the marker type is classified per-onset by band (for drums);
    otherwise every marker uses fixed_type (e.g. 'Melodic' for the melody stem).

    Intensity is mapped from normalised onset strength over a wide range so the RUNTIME
    difficulty filter (and minGap) does most of the thinning per enemy. A gentle
    sensitivity gate pre-drops the weakest onsets.
    """
    onset_env = librosa.onset.onset_strength(y=y_sig, sr=sr)
    frames = librosa.onset.onset_detect(
        y=y_sig, sr=sr, onset_envelope=onset_env, backtrack=False
    )
    if len(frames) == 0:
        return []

    times = librosa.frames_to_time(frames, sr=sr)
    strengths = onset_env[frames].astype(float)
    if strengths.max() > 0:
        strengths = strengths / strengths.max()

    classify = fixed_type is None
    if classify:
        S = np.abs(librosa.stft(y_sig))
        freqs = librosa.fft_frequencies(sr=sr)
        low_mask = freqs < 150
        mid_mask = (freqs >= 150) & (freqs < 2000)
        high_mask = freqs >= 2000

    gate = 0.5 * (1.0 - sensitivity)  # gentle: keep most onsets, difficulty thins later
    markers = []
    for i, t in enumerate(times):
        s = float(strengths[i])
        if s < gate:
            continue
        beat_pos = round(t / sec_per_beat, 2)
        if classify:
            mtype = _drum_type(S, freqs, librosa.time_to_frames(t, sr=sr),
                               low_mask, mid_mask, high_mask)
        else:
            mtype = fixed_type
        intensity = round(0.15 + 0.85 * s, 2)
        intensity = max(0.15, min(1.0, intensity))
        markers.append({
            "beat": beat_pos,
            "type": mtype,
            "intensity": intensity,
            "direction": 0.0,
            "holdBeats": 0.0,
            "instrument": instrument,
        })
    return markers


# =====================================================================
# BLENDED MARKERS (original single-stream path, --no-stems)
# =====================================================================

def _build_blended_markers(y, sr, bpm, sec_per_beat, sensitivity):
    """
    Original behaviour: quarter-note beat grid markers (intensity 0.7) plus extra
    onset markers above a sensitivity threshold. Every marker tagged 'All'. This is
    the proven path; kept as a fallback when stem separation gives poor results.
    """
    _, beat_frames = librosa.beat.beat_track(y=y, sr=sr, bpm=bpm, sparse=True)
    beat_times = librosa.frames_to_time(beat_frames, sr=sr)

    onset_env = librosa.onset.onset_strength(y=y, sr=sr)
    onset_frames = librosa.onset.onset_detect(
        y=y, sr=sr, onset_envelope=onset_env, backtrack=False
    )
    onset_times = librosa.frames_to_time(onset_frames, sr=sr)
    onset_strengths = onset_env[onset_frames] if len(onset_frames) > 0 else np.array([])
    if len(onset_strengths) > 0 and onset_strengths.max() > 0:
        onset_strengths = onset_strengths / onset_strengths.max()

    S = np.abs(librosa.stft(y))
    freqs = librosa.fft_frequencies(sr=sr)
    low_mask = freqs < 150
    mid_mask = (freqs >= 150) & (freqs < 2000)
    high_mask = freqs >= 2000

    markers = []
    beat_set = set()

    for t in beat_times:
        beat_pos = round(t / sec_per_beat, 2)
        beat_set.add(round(beat_pos, 1))
        mtype = _drum_type(S, freqs, librosa.time_to_frames(t, sr=sr),
                           low_mask, mid_mask, high_mask)
        # blended beats lean kick/snare/accent; treat HiHat-class as Accent here
        if mtype == "HiHat":
            mtype = "Accent"
        markers.append({
            "beat": beat_pos, "type": mtype, "intensity": 0.7,
            "direction": 0.0, "holdBeats": 0.0, "instrument": "All",
        })

    threshold = 1.0 - sensitivity
    for i, t in enumerate(onset_times):
        beat_pos = round(t / sec_per_beat, 2)
        if round(beat_pos, 1) in beat_set:
            continue
        strength = onset_strengths[i] if i < len(onset_strengths) else 0.5
        if strength < threshold:
            continue
        fi = librosa.time_to_frames(t, sr=sr)
        if fi < S.shape[1]:
            low = float(np.mean(S[low_mask, fi])) if low_mask.any() else 0.0
            mid = float(np.mean(S[mid_mask, fi])) if mid_mask.any() else 0.0
            high = float(np.mean(S[high_mask, fi])) if high_mask.any() else 0.0
            total = low + mid + high
            hr = high / total if total > 0 else 0.33
            mr = mid / total if total > 0 else 0.33
            mtype = "HiHat" if hr > 0.4 else ("Snare" if mr > 0.4 else "Melodic")
        else:
            mtype = "HiHat"
        intensity = max(0.15, min(0.65, round(float(strength) * 0.6, 2)))
        markers.append({
            "beat": beat_pos, "type": mtype, "intensity": intensity,
            "direction": 0.0, "holdBeats": 0.0, "instrument": "All",
        })

    return markers


# =====================================================================
# MAIN ANALYSIS
# =====================================================================

def analyze(audio_path, forced_bpm=None, sensitivity=0.5, intro_beats=2.0, use_stems=True):
    """
    Analyze audio and return beat markers (instrument-tagged) + sections.

    sensitivity: 0.0 = only strongest onsets, 1.0 = everything
    intro_beats: length (in beats) of the short enemy-only intro at the very start.
                 Keeps the 'song settles in before player notes appear' feel without
                 burying the player under a long wait. Default 2 = half a bar; pass 4
                 for a full bar. NOTE: this is in BEATS, so a slow enemy (low effective
                 BPM) stretches it into more real-time seconds. The flat dead-air delay
                 before the song starts is separate - that's BattleManager._introDelay
                 in Unity, and the scene-saved value there overrides the code default.
    use_stems:   True (default) splits into Drums/Bass/Melody stems and tags markers so
                 Unity can follow one instrument. False = original single blended stream.
    """
    print(f"Loading {audio_path}...")
    y, sr = librosa.load(audio_path, sr=22050, mono=True)
    duration = librosa.get_duration(y=y, sr=sr)
    print(f"Duration: {duration:.1f}s, Sample rate: {sr}")

    # --- BPM ---
    if forced_bpm:
        bpm = forced_bpm
        print(f"Using forced BPM: {bpm}")
    else:
        tempo_estimate = librosa.beat.beat_track(y=y, sr=sr, sparse=True)[0]
        bpm = float(np.round(tempo_estimate, 1))
        print(f"Detected BPM: {bpm}")
    sec_per_beat = 60.0 / bpm

    # --- Full-mix onset envelope: used for SECTION detection regardless of stem mode ---
    onset_env_full = librosa.onset.onset_strength(y=y, sr=sr)

    # --- Markers ---
    if use_stems:
        y_drums, y_bass, y_mel = _separate_stems(y, sr)
        markers = []
        markers += _onset_markers(y_drums, sr, sec_per_beat, sensitivity, "Drums")
        if y_bass is not None:
            markers += _onset_markers(y_bass, sr, sec_per_beat, sensitivity, "Bass", fixed_type="Accent")
        markers += _onset_markers(y_mel, sr, sec_per_beat, sensitivity, "Melody", fixed_type="Melodic")
        markers.sort(key=lambda m: m["beat"])
    else:
        print("Stems disabled (--no-stems): single blended stream tagged 'All'.")
        markers = _build_blended_markers(y, sr, bpm, sec_per_beat, sensitivity)
        markers.sort(key=lambda m: m["beat"])

    # per-instrument counts (for the printout / stats)
    inst_counts = {}
    for m in markers:
        inst_counts[m["instrument"]] = inst_counts.get(m["instrument"], 0) + 1
    print(f"Total markers: {len(markers)}  {inst_counts}")

    # --- Sections via full-mix onset energy ---
    # The song opens with a SHORT enemy-only intro (intro_beats, half a bar by default)
    # so the music establishes itself and notes don't slam onto a lane the instant the
    # song starts - but it's short so the player isn't left waiting. After the intro the
    # rest is energy-classified, EXCEPT the first body section is forced playable (below)
    # so player notes always arrive right after the short intro.
    total_beats = round(duration / sec_per_beat, 1)
    section_length_beats = 16  # 4 bars of 4/4
    sections = []

    def raw_mean_energy(sb, eb):
        st = librosa.time_to_frames(sb * sec_per_beat, sr=sr)
        et = min(librosa.time_to_frames(eb * sec_per_beat, sr=sr), len(onset_env_full) - 1)
        return float(np.mean(onset_env_full[st:et])) if st < et else 0.0

    intro_end = round(min(intro_beats, total_beats), 1)
    body_start = intro_end
    remaining = max(0.0, total_beats - body_start)
    num_sections = max(1, int(remaining / section_length_beats))

    # Normalise each body section's mean energy against the MIN-MAX range across sections,
    # so quiet vs loud sections actually separate over 0-1 and the thresholds work. (The
    # old code divided each MEAN by the song PEAK, so everything scored ~0.1 and the whole
    # song became one EnemyOnly block - the player never got notes.)
    bounds = []
    for i in range(num_sections):
        sb = round(body_start + i * section_length_beats, 1)
        eb = round(min(body_start + (i + 1) * section_length_beats, total_beats), 1)
        bounds.append((sb, eb))
    raw_energies = [raw_mean_energy(sb, eb) for sb, eb in bounds]
    e_lo = min(raw_energies) if raw_energies else 0.0
    e_hi = max(raw_energies) if raw_energies else 1.0
    e_rng = (e_hi - e_lo) if e_hi > e_lo else 1.0

    def norm_energy(i):
        return (raw_energies[i] - e_lo) / e_rng

    if intro_end > 0:
        sections.append({
            "label": "Intro",
            "startBeat": 0.0,
            "endBeat": intro_end,
            "type": "Intro",
            "highway": "EnemyOnly",
            "intensityScale": round((norm_energy(0) if raw_energies else 0.5) + 0.5, 2)
        })

    for i in range(num_sections):
        start_beat, end_beat = bounds[i]
        if end_beat <= start_beat:
            continue
        normalized_energy = norm_energy(i)

        if i == num_sections - 1:
            section_type = "Outro"
            highway = "EnemyOnly"
        elif normalized_energy > 0.66:
            section_type = "Chorus"
            highway = "Both"
        elif normalized_energy < 0.2:
            section_type = "Break"
            highway = "EnemyOnly"
        else:
            section_type = "Verse"
            highway = "PlayerOnly"

        # Guarantee the FIRST body section is playable. Songs often open with their
        # quietest body section, which would classify as a Break (EnemyOnly) and stack
        # onto the short intro - a long stretch with no player notes. Forcing the first
        # body section to at least PlayerOnly means player notes always arrive right after
        # the short intro, never later. Applies even with a single body section.
        if i == 0 and highway == "EnemyOnly":
            section_type = "Verse"
            highway = "PlayerOnly"

        sections.append({
            "label": f"{section_type} {i + 1}",
            "startBeat": start_beat,
            "endBeat": end_beat,
            "type": section_type,
            "highway": highway,
            "intensityScale": round(float(normalized_energy) + 0.5, 2)
        })

    # --- Output ---
    result = {
        "_comment": "Generated by beatmap_generator.py - review and adjust in Unity inspector",
        "songName": audio_path.replace("\\", "/").rsplit("/", 1)[-1].rsplit(".", 1)[0],
        "bpm": bpm,
        "totalBeats": round(total_beats, 1),
        "durationSeconds": round(duration, 2),
        "stems": bool(use_stems),
        "leadInBeats": float(intro_beats),
        "tailBeats": 2.0,
        "sections": sections,
        "markers": markers,
        "stats": {
            "totalMarkers": len(markers),
            "markersByInstrument": inst_counts,
            "estimatedNotesAtDifficulty0.3": sum(1 for m in markers if m["intensity"] >= 0.7),
            "estimatedNotesAtDifficulty0.5": sum(1 for m in markers if m["intensity"] >= 0.5),
            "estimatedNotesAtDifficulty0.8": sum(1 for m in markers if m["intensity"] >= 0.2),
        }
    }
    return result


def main():
    parser = argparse.ArgumentParser(description="Generate RhythmRogue beat map from audio")
    parser.add_argument("audio", help="Path to audio file (WAV, MP3, OGG, FLAC)")
    parser.add_argument("--bpm", type=float, default=None, help="Force BPM (auto-detect if omitted)")
    parser.add_argument("--sensitivity", type=float, default=0.5,
                        help="Onset sensitivity 0.0-1.0 (higher = more markers)")
    parser.add_argument("--intro-beats", type=float, default=2.0,
                        help="Length in beats of the short enemy-only intro before player "
                             "notes start (default 2 = half a bar; pass 4 for a full bar). "
                             "Set 0 to start immediately. This is in BEATS, so slow enemies "
                             "stretch it into more seconds; the flat pre-song dead air is "
                             "separate (BattleManager._introDelay in Unity).")
    parser.add_argument("--no-stems", dest="stems", action="store_false",
                        help="Disable instrument-stem separation; emit one blended marker "
                             "stream tagged 'All' (the original behaviour). Use if a track "
                             "separates poorly.")
    parser.set_defaults(stems=True)
    parser.add_argument("--output", type=str, default=None, help="Output JSON path")
    args = parser.parse_args()

    result = analyze(args.audio, args.bpm, args.sensitivity, args.intro_beats, args.stems)

    output_path = args.output or args.audio.rsplit(".", 1)[0] + "_beatmap.json"
    with open(output_path, "w") as f:
        json.dump(result, f, indent=2)

    print(f"\nExported to: {output_path}")
    print(f"  Stems: {'on (Drums/Bass/Melody)' if result['stems'] else 'off (blended, All)'}")
    print(f"  Markers: {result['stats']['totalMarkers']}  {result['stats']['markersByInstrument']}")
    print(f"  Sections: {len(result['sections'])}")
    print(f"  Notes at easy (0.3): ~{result['stats']['estimatedNotesAtDifficulty0.3']}")
    print(f"  Notes at normal (0.5): ~{result['stats']['estimatedNotesAtDifficulty0.5']}")
    print(f"  Notes at hard (0.8): ~{result['stats']['estimatedNotesAtDifficulty0.8']}")
    print(f"\nNext: RhythmRogue > Import Beat Map JSON in Unity, then set each enemy's")
    print(f"chartInstrument (All / Drums / Bass / Melody) in the Inspector.")


if __name__ == "__main__":
    main()
