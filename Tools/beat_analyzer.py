"""
RhythmRogue Beat Map Analyzer
=============================
Analyzes an audio file and produces a JSON beat map compatible with
Unity's SongBeatMap format.

Usage:
    python beat_analyzer.py path/to/song.ogg
    python beat_analyzer.py path/to/song.wav --bpm 120
    python beat_analyzer.py path/to/song.mp3 --output my_beatmap.json

Requirements:
    pip install librosa soundfile numpy
    Also needs ffmpeg installed for OGG/MP3 support.

Marker Types (match Unity MarkerType enum):
    0 = Kick      Low frequency dominant vs song average
    1 = Snare     Mid frequency dominant, percussive
    2 = HiHat     High frequency dominant
    3 = Melodic   Balanced spectrum, tonal content
    4 = Accent    Strong onset, no clear band dominance
    5 = Drop      First strong onset after quiet gap
    6 = Break     Silence gap marker
    7 = Fill      Rapid cluster near section boundary

Hold Detection:
    After each onset, the analyzer measures how long the audio energy
    stays sustained above a threshold. If the sustained duration is
    0.5+ beats, the marker gets a holdBeats value. Holds are capped
    at 80% of the gap to the next note so they never overlap.
"""

import argparse
import json
import os
import sys
import numpy as np

try:
    import librosa
except ImportError:
    print("ERROR: librosa not installed. Run: pip install librosa soundfile numpy")
    sys.exit(1)


MARKER_KICK = 0
MARKER_SNARE = 1
MARKER_HIHAT = 2
MARKER_MELODIC = 3
MARKER_ACCENT = 4
MARKER_DROP = 5
MARKER_BREAK = 6
MARKER_FILL = 7

MARKER_NAMES = {
    0: "Kick", 1: "Snare", 2: "HiHat", 3: "Melodic",
    4: "Accent", 5: "Drop", 6: "Break", 7: "Fill"
}

SECTION_INTRO = 0
SECTION_VERSE = 1
SECTION_CHORUS = 2
SECTION_BRIDGE = 3
SECTION_BREAK = 4
SECTION_OUTRO = 5

HIGHWAY_ENEMY = 0
HIGHWAY_PLAYER = 1
HIGHWAY_BOTH = 2


def analyze_audio(filepath, forced_bpm=None):
    print(f"Loading: {filepath}")
    y, sr = librosa.load(filepath, sr=22050, mono=True)
    duration = librosa.get_duration(y=y, sr=sr)
    print(f"Duration: {duration:.1f}s, Sample rate: {sr}")

    # ---- BPM ----
    if forced_bpm:
        bpm = forced_bpm
    else:
        tempo, _ = librosa.beat.beat_track(y=y, sr=sr)
        bpm = float(np.atleast_1d(tempo)[0])
    print(f"BPM: {bpm:.1f}")

    sec_per_beat = 60.0 / bpm
    total_beats = duration / sec_per_beat

    # ---- Spectrogram and bands ----
    S = np.abs(librosa.stft(y))
    freqs = librosa.fft_frequencies(sr=sr)

    low_band = freqs < 200
    mid_band = (freqs >= 200) & (freqs < 2000)
    high_band = freqs >= 2000

    # ---- Onset detection ----
    onset_env = librosa.onset.onset_strength(y=y, sr=sr)
    onset_frames = librosa.onset.onset_detect(
        onset_envelope=onset_env, sr=sr,
        units='frames', backtrack=False, delta=0.15)
    onset_times = librosa.frames_to_time(onset_frames, sr=sr)
    print(f"Raw onsets: {len(onset_frames)}")

    # ---- Intensity (percentile normalization) ----
    onset_strengths = np.array([
        float(onset_env[f]) if f < len(onset_env) else 0.0
        for f in onset_frames
    ])

    if len(onset_strengths) > 0 and onset_strengths.max() > 0:
        p10 = np.percentile(onset_strengths, 10)
        p95 = np.percentile(onset_strengths, 95)
        denom = p95 - p10 if p95 > p10 else 1.0
        normalized = np.clip((onset_strengths - p10) / denom, 0.0, 1.0)
        intensities = 0.15 + normalized * 0.85
    else:
        intensities = np.full(len(onset_frames), 0.5)

    # ---- Band ratios for classification ----
    low_ratios = []
    mid_ratios = []
    high_ratios = []

    for frame in onset_frames:
        if frame >= S.shape[1]:
            low_ratios.append(0.33)
            mid_ratios.append(0.33)
            high_ratios.append(0.33)
            continue

        spectrum = S[:, frame]
        total = spectrum.sum()
        if total <= 0:
            low_ratios.append(0.33)
            mid_ratios.append(0.33)
            high_ratios.append(0.33)
            continue

        low_ratios.append(spectrum[low_band].sum() / total)
        mid_ratios.append(spectrum[mid_band].sum() / total)
        high_ratios.append(spectrum[high_band].sum() / total)

    low_ratios = np.array(low_ratios)
    mid_ratios = np.array(mid_ratios)
    high_ratios = np.array(high_ratios)

    avg_low = low_ratios.mean()
    avg_mid = mid_ratios.mean()
    avg_high = high_ratios.mean()
    print(f"Avg band ratios - Low: {avg_low:.3f}, Mid: {avg_mid:.3f}, High: {avg_high:.3f}")

    # ---- Classify onsets ----
    onset_types = classify_onsets_relative(
        onset_frames, low_ratios, mid_ratios, high_ratios,
        avg_low, avg_mid, avg_high)

    # ---- RMS for hold/drop/break detection ----
    rms = librosa.feature.rms(y=y)[0]
    rms_times = librosa.frames_to_time(np.arange(len(rms)), sr=sr)

    # ---- Detect holds (sustained energy after onset) ----
    hold_durations = detect_holds(
        onset_times, onset_types, rms, rms_times, sr, sec_per_beat)

    # ---- Mark fills ----
    section_boundaries = detect_section_boundaries(sec_per_beat, total_beats)
    mark_fills(onset_types, onset_times, sec_per_beat, section_boundaries)

    # ---- Mark drops ----
    mark_drops(onset_types, onset_times, intensities, rms, sr, sec_per_beat)

    # ---- Build markers ----
    markers = []
    seen_beats = set()

    for i in range(len(onset_frames)):
        time_sec = onset_times[i]
        beat_pos = time_sec / sec_per_beat

        quantized = round(beat_pos * 4) / 4
        if abs(beat_pos - quantized) < 0.12:
            beat_pos = quantized
        beat_pos = round(beat_pos, 3)

        if beat_pos in seen_beats:
            for existing in markers:
                if existing["beat"] == beat_pos:
                    if intensities[i] > existing["intensity"]:
                        existing["intensity"] = round(float(intensities[i]), 3)
                        existing["type"] = onset_types[i]
                        existing["holdBeats"] = round(float(hold_durations[i]), 3)
                    break
            continue

        seen_beats.add(beat_pos)
        markers.append({
            "beat": beat_pos,
            "type": onset_types[i],
            "intensity": round(float(intensities[i]), 3),
            "direction": 0.0,
            "holdBeats": round(float(hold_durations[i]), 3),
        })

    markers.sort(key=lambda m: m["beat"])

    # ---- Clamp holds so they don't overlap next note ----
    for i in range(len(markers) - 1):
        if markers[i]["holdBeats"] > 0:
            gap = markers[i + 1]["beat"] - markers[i]["beat"]
            max_hold = gap * 0.8  # 80% of gap, leave breathing room
            markers[i]["holdBeats"] = round(min(markers[i]["holdBeats"], max_hold), 3)

    # ---- Insert breaks ----
    markers = insert_breaks(markers, sec_per_beat, total_beats)

    print(f"Markers after processing: {len(markers)}")

    # ---- Sections ----
    sections = detect_sections(y, sr, bpm, total_beats, duration)

    # ---- Hold summary ----
    hold_count = sum(1 for m in markers if m["holdBeats"] > 0)
    if hold_count > 0:
        hold_lengths = [m["holdBeats"] for m in markers if m["holdBeats"] > 0]
        print(f"Holds: {hold_count} (avg {sum(hold_lengths)/len(hold_lengths):.2f}b, "
              f"max {max(hold_lengths):.2f}b)")
    else:
        print("Holds: 0")

    print(f"\nResult: {len(markers)} markers, {len(sections)} sections, "
          f"{bpm:.1f} BPM, {total_beats:.0f} beats")

    return {
        "bpm": round(bpm, 2),
        "totalBeats": round(total_beats, 2),
        "audioOffsetSeconds": 0.0,
        "markers": markers,
        "sections": sections,
    }


def detect_holds(onset_times, onset_types, rms, rms_times, sr, sec_per_beat):
    """
    Detect sustained energy after each onset to determine hold durations.

    For each onset:
      1. Measure the RMS energy at the onset (peak energy)
      2. Walk forward in time, tracking how long energy stays above
         a decay threshold (40% of peak)
      3. If the sustained duration is 0.5+ beats, it's a hold
      4. Percussive types (Kick, Snare, HiHat) get shorter holds
         or none, since they're transient by nature

    Returns array of hold durations in beats (0.0 = no hold).
    """
    hold_durations = np.zeros(len(onset_times))

    # Minimum hold to be worth charting
    min_hold_beats = 0.5
    min_hold_sec = min_hold_beats * sec_per_beat

    # Maximum hold duration
    max_hold_beats = 4.0
    max_hold_sec = max_hold_beats * sec_per_beat

    # How much energy must be sustained relative to onset peak
    decay_threshold = 0.35

    # Percussive onsets are less likely to sustain
    percussive_types = {MARKER_KICK, MARKER_SNARE, MARKER_HIHAT}

    for i in range(len(onset_times)):
        onset_sec = onset_times[i]

        # Find the RMS frame closest to this onset
        onset_frame_idx = np.searchsorted(rms_times, onset_sec)
        if onset_frame_idx >= len(rms):
            continue

        # Get peak energy in a small window around the onset (3 frames)
        window_start = max(0, onset_frame_idx - 1)
        window_end = min(len(rms), onset_frame_idx + 2)
        peak_energy = rms[window_start:window_end].max()

        if peak_energy <= 0:
            continue

        threshold = peak_energy * decay_threshold

        # Walk forward until energy drops below threshold
        sustain_end_frame = onset_frame_idx
        search_limit = onset_frame_idx + int(max_hold_sec / (rms_times[1] - rms_times[0]) if len(rms_times) > 1 else 100)
        search_limit = min(search_limit, len(rms))

        for f in range(onset_frame_idx + 1, search_limit):
            if rms[f] >= threshold:
                sustain_end_frame = f
            else:
                # Allow a small dip (1-2 frames) before giving up
                if f + 1 < search_limit and rms[f + 1] >= threshold:
                    continue
                break

        sustain_sec = rms_times[sustain_end_frame] - onset_sec if sustain_end_frame > onset_frame_idx else 0.0

        # Skip if too short
        if sustain_sec < min_hold_sec:
            continue

        # Percussive types need longer sustain to qualify as a hold
        if onset_types[i] in percussive_types:
            if sustain_sec < min_hold_sec * 2:
                continue

        # Cap the hold at the gap to next onset (if there is one)
        if i < len(onset_times) - 1:
            gap_sec = onset_times[i + 1] - onset_sec
            sustain_sec = min(sustain_sec, gap_sec * 0.8)

        sustain_sec = min(sustain_sec, max_hold_sec)
        hold_beats = sustain_sec / sec_per_beat

        # Quantize to nearest quarter beat
        hold_beats = round(hold_beats * 4) / 4

        if hold_beats >= min_hold_beats:
            hold_durations[i] = hold_beats

    return hold_durations


def classify_onsets_relative(onset_frames, low_ratios, mid_ratios, high_ratios,
                              avg_low, avg_mid, avg_high):
    types = []
    dominance_factor = 1.25

    for i in range(len(onset_frames)):
        lr = low_ratios[i]
        mr = mid_ratios[i]
        hr = high_ratios[i]

        low_dominant = lr > avg_low * dominance_factor
        mid_dominant = mr > avg_mid * dominance_factor
        high_dominant = hr > avg_high * dominance_factor

        dominant_count = int(low_dominant) + int(mid_dominant) + int(high_dominant)

        if dominant_count == 0:
            types.append(MARKER_MELODIC)
        elif dominant_count >= 2:
            if low_dominant and mid_dominant:
                types.append(MARKER_SNARE)
            elif mid_dominant and high_dominant:
                types.append(MARKER_HIHAT)
            else:
                types.append(MARKER_ACCENT)
        else:
            if low_dominant:
                types.append(MARKER_KICK)
            elif high_dominant:
                types.append(MARKER_HIHAT)
            elif mid_dominant:
                types.append(MARKER_SNARE)
            else:
                types.append(MARKER_ACCENT)

    return types


def mark_fills(onset_types, onset_times, sec_per_beat, section_boundaries):
    fill_window_sec = 4.0 * sec_per_beat
    min_fill_count = 4
    max_fill_span_sec = 2.0 * sec_per_beat

    for boundary_sec in section_boundaries:
        window_start = boundary_sec - fill_window_sec
        window_end = boundary_sec

        indices = [
            i for i, t in enumerate(onset_times)
            if window_start <= t < window_end
        ]

        if len(indices) < min_fill_count:
            continue

        for start in range(len(indices)):
            cluster = [indices[start]]
            for end in range(start + 1, len(indices)):
                span = onset_times[indices[end]] - onset_times[indices[start]]
                if span <= max_fill_span_sec:
                    cluster.append(indices[end])
                else:
                    break

            if len(cluster) >= min_fill_count:
                for idx in cluster:
                    if onset_types[idx] not in (MARKER_DROP, MARKER_BREAK):
                        onset_types[idx] = MARKER_FILL


def mark_drops(onset_types, onset_times, intensities, rms, sr, sec_per_beat):
    if len(onset_times) < 2:
        return

    intensity_threshold = np.percentile(intensities, 70)
    min_gap_sec = 2.0 * sec_per_beat

    hop_length = 512
    rms_times = librosa.frames_to_time(np.arange(len(rms)), sr=sr, hop_length=hop_length)
    overall_rms = rms.mean()

    for i in range(1, len(onset_times)):
        gap = onset_times[i] - onset_times[i - 1]
        if gap < min_gap_sec:
            continue
        if intensities[i] < intensity_threshold:
            continue

        gap_mask = (rms_times >= onset_times[i - 1]) & (rms_times < onset_times[i])
        if gap_mask.any():
            gap_rms = rms[gap_mask].mean()
            if gap_rms < overall_rms * 0.4:
                onset_types[i] = MARKER_DROP


def insert_breaks(markers, sec_per_beat, total_beats):
    if len(markers) < 2:
        return markers

    min_gap_beats = 3.0
    result = []

    for i in range(len(markers)):
        result.append(markers[i])
        if i < len(markers) - 1:
            gap = markers[i + 1]["beat"] - markers[i]["beat"]
            if gap >= min_gap_beats:
                break_beat = round(markers[i]["beat"] + gap * 0.5, 3)
                result.append({
                    "beat": break_beat,
                    "type": MARKER_BREAK,
                    "intensity": 0.0,
                    "direction": 0.0,
                    "holdBeats": 0.0,
                })

    result.sort(key=lambda m: m["beat"])
    return result


def detect_section_boundaries(sec_per_beat, total_beats):
    section_beats = 16.0
    boundaries = []
    beat = section_beats
    while beat < total_beats:
        boundaries.append(beat * sec_per_beat)
        beat += section_beats
    return boundaries


def detect_sections(y, sr, bpm, total_beats, duration):
    sec_per_beat = 60.0 / bpm
    section_beats = 16.0
    section_secs = section_beats * sec_per_beat

    rms = librosa.feature.rms(y=y)[0]
    rms_times = librosa.frames_to_time(np.arange(len(rms)), sr=sr)

    num_sections = max(1, int(np.ceil(total_beats / section_beats)))
    sections = []
    energies = []

    for i in range(num_sections):
        start_sec = i * section_secs
        end_sec = min((i + 1) * section_secs, duration)
        start_beat = round(start_sec / sec_per_beat, 2)
        end_beat = round(end_sec / sec_per_beat, 2)

        mask = (rms_times >= start_sec) & (rms_times < end_sec)
        energy = float(rms[mask].mean()) if mask.any() else 0.0
        energies.append(energy)
        sections.append({"startBeat": start_beat, "endBeat": end_beat, "energy": energy})

    max_energy = max(energies) if energies else 1.0
    if max_energy <= 0:
        max_energy = 1.0

    result = []
    for i, s in enumerate(sections):
        normalized = s["energy"] / max_energy

        if i == 0:
            label, stype, hw = "Intro", SECTION_INTRO, HIGHWAY_ENEMY
        elif i == len(sections) - 1:
            label, stype, hw = "Outro", SECTION_OUTRO, HIGHWAY_ENEMY
        elif normalized > 0.7:
            label, stype, hw = f"Chorus {i}", SECTION_CHORUS, HIGHWAY_BOTH
        elif normalized < 0.3:
            label, stype, hw = f"Break {i}", SECTION_BREAK, HIGHWAY_ENEMY
        else:
            label, stype, hw = f"Verse {i}", SECTION_VERSE, HIGHWAY_PLAYER

        result.append({
            "label": label,
            "startBeat": s["startBeat"],
            "endBeat": s["endBeat"],
            "type": stype,
            "highway": hw,
            "intensityScale": round(max(0.5, min(1.5, normalized + 0.5)), 3),
        })

    return result


def print_summary(data):
    print("\n" + "=" * 60)
    print(f"BPM: {data['bpm']}")
    print(f"Total beats: {data['totalBeats']}")
    print(f"Markers: {len(data['markers'])}")
    print(f"Sections: {len(data['sections'])}")

    type_counts = {}
    for m in data["markers"]:
        type_counts[m["type"]] = type_counts.get(m["type"], 0) + 1

    print("\nMarker breakdown:")
    for t in sorted(type_counts.keys()):
        print(f"  {MARKER_NAMES.get(t, '?'):8s}: {type_counts[t]}")

    non_break = [m for m in data["markers"] if m["type"] != MARKER_BREAK]
    intensities = [m["intensity"] for m in non_break]
    if intensities:
        print(f"\nIntensity: {min(intensities):.2f} - {max(intensities):.2f} "
              f"(avg {sum(intensities)/len(intensities):.2f})")
        low = sum(1 for i in intensities if i < 0.4)
        mid = sum(1 for i in intensities if 0.4 <= i < 0.7)
        high = sum(1 for i in intensities if i >= 0.7)
        print(f"  Low (<0.4): {low}  Mid (0.4-0.7): {mid}  High (>0.7): {high}")

    # Hold summary
    holds = [m for m in data["markers"] if m["holdBeats"] > 0]
    if holds:
        lengths = [m["holdBeats"] for m in holds]
        print(f"\nHolds: {len(holds)} notes with holds")
        print(f"  Length: {min(lengths):.2f} - {max(lengths):.2f}b "
              f"(avg {sum(lengths)/len(lengths):.2f}b)")
    else:
        print("\nHolds: none detected")

    print("\nSections:")
    sec_names = {0: "Intro", 1: "Verse", 2: "Chorus", 3: "Bridge", 4: "Break", 5: "Outro"}
    hw_names = {0: "Enemy", 1: "Player", 2: "Both"}
    for s in data["sections"]:
        beats = s["endBeat"] - s["startBeat"]
        print(f"  {s['label']:12s} [{s['startBeat']:6.1f} - {s['endBeat']:6.1f}] "
              f"({beats:.0f}b) {sec_names.get(s['type'],'?'):8s} -> {hw_names.get(s['highway'],'?')}")

    print("=" * 60)


def main():
    parser = argparse.ArgumentParser(description="RhythmRogue Beat Map Analyzer")
    parser.add_argument("audio", help="Path to audio file")
    parser.add_argument("--bpm", type=float, default=None, help="Force BPM")
    parser.add_argument("--output", "-o", type=str, default=None, help="Output JSON path")
    args = parser.parse_args()

    filepath = os.path.abspath(args.audio)
    if not os.path.exists(filepath):
        print(f"ERROR: File not found: {filepath}")
        sys.exit(1)

    data = analyze_audio(filepath, forced_bpm=args.bpm)
    print_summary(data)

    output_path = args.output or f"{os.path.splitext(os.path.basename(filepath))[0]}_beatmap.json"
    with open(output_path, "w") as f:
        json.dump(data, f, indent=2)
    print(f"\nSaved to: {output_path}")


if __name__ == "__main__":
    main()