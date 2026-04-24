"""
Beat Map Generator for RhythmRogue
===================================
Analyzes an audio file and exports beat markers as JSON
that can be imported into a SongBeatMap ScriptableObject.

Requirements:
    pip install librosa soundfile numpy

Usage:
    python beatmap_generator.py my_song.wav --bpm 135

If you omit --bpm, librosa will auto-detect it.

Output: my_song_beatmap.json with markers and sections.
"""

import argparse
import json
import numpy as np

try:
    import librosa
except ImportError:
    print("Install librosa first: pip install librosa soundfile numpy")
    exit(1)


def analyze(audio_path, forced_bpm=None, sensitivity=0.5):
    """
    Analyze audio and return beat markers + onset markers.
    
    sensitivity: 0.0 = only strongest onsets, 1.0 = everything
    """
    print(f"Loading {audio_path}...")
    y, sr = librosa.load(audio_path, sr=22050, mono=True)
    duration = librosa.get_duration(y=y, sr=sr)
    print(f"Duration: {duration:.1f}s, Sample rate: {sr}")

    # --- BPM Detection ---
    if forced_bpm:
        bpm = forced_bpm
        print(f"Using forced BPM: {bpm}")
    else:
        tempo_estimate = librosa.beat.beat_track(y=y, sr=sr, sparse=True)[0]
        bpm = float(np.round(tempo_estimate, 1))
        print(f"Detected BPM: {bpm}")

    sec_per_beat = 60.0 / bpm

    # --- Beat Tracking (quarter notes) ---
    _, beat_frames = librosa.beat.beat_track(y=y, sr=sr, bpm=bpm, sparse=True)
    beat_times = librosa.frames_to_time(beat_frames, sr=sr)
    print(f"Detected {len(beat_times)} beats")

    # --- Onset Detection (all transients) ---
    onset_env = librosa.onset.onset_strength(y=y, sr=sr)
    onset_frames = librosa.onset.onset_detect(
        y=y, sr=sr, onset_envelope=onset_env,
        backtrack=False
    )
    onset_times = librosa.frames_to_time(onset_frames, sr=sr)
    onset_strengths = onset_env[onset_frames] if len(onset_frames) > 0 else np.array([])
    
    # Normalize onset strengths to 0-1
    if len(onset_strengths) > 0 and onset_strengths.max() > 0:
        onset_strengths = onset_strengths / onset_strengths.max()

    print(f"Detected {len(onset_times)} onsets")

    # --- Spectral Analysis for marker type hints ---
    # Low frequencies = kick, mid = snare, high = hi-hat
    S = np.abs(librosa.stft(y))
    freqs = librosa.fft_frequencies(sr=sr)

    # Frequency bands
    low_mask = freqs < 150       # Kick range
    mid_mask = (freqs >= 150) & (freqs < 2000)  # Snare range
    high_mask = freqs >= 2000    # Hi-hat range

    # --- Build markers ---
    markers = []
    beat_set = set()

    # Convert beat times to beat positions
    for t in beat_times:
        beat_pos = round(t / sec_per_beat, 2)
        beat_set.add(round(beat_pos, 1))

        # Determine marker type from spectral content at this time
        frame_idx = librosa.time_to_frames(t, sr=sr)
        if frame_idx < S.shape[1]:
            low_energy = np.mean(S[low_mask, frame_idx]) if low_mask.any() else 0
            mid_energy = np.mean(S[mid_mask, frame_idx]) if mid_mask.any() else 0
            high_energy = np.mean(S[high_mask, frame_idx]) if high_mask.any() else 0

            total = low_energy + mid_energy + high_energy
            if total > 0:
                low_ratio = low_energy / total
                mid_ratio = mid_energy / total
            else:
                low_ratio = mid_ratio = 0.33

            if low_ratio > 0.45:
                marker_type = "Kick"
            elif mid_ratio > 0.4:
                marker_type = "Snare"
            else:
                marker_type = "Accent"
        else:
            marker_type = "Kick"

        markers.append({
            "beat": beat_pos,
            "type": marker_type,
            "intensity": 0.7,
            "direction": 0.0,
            "holdBeats": 0.0
        })

    # Add onsets that aren't already beats
    threshold = 1.0 - sensitivity  # Higher sensitivity = lower threshold
    for i, t in enumerate(onset_times):
        beat_pos = round(t / sec_per_beat, 2)
        rounded = round(beat_pos, 1)

        # Skip if too close to an existing beat marker
        if rounded in beat_set:
            continue

        strength = onset_strengths[i] if i < len(onset_strengths) else 0.5

        # Skip weak onsets based on sensitivity
        if strength < threshold:
            continue

        # Determine type from spectral content
        frame_idx = librosa.time_to_frames(t, sr=sr)
        if frame_idx < S.shape[1]:
            low_energy = np.mean(S[low_mask, frame_idx]) if low_mask.any() else 0
            mid_energy = np.mean(S[mid_mask, frame_idx]) if mid_mask.any() else 0
            high_energy = np.mean(S[high_mask, frame_idx]) if high_mask.any() else 0

            total = low_energy + mid_energy + high_energy
            if total > 0:
                high_ratio = high_energy / total
                mid_ratio = mid_energy / total
            else:
                high_ratio = mid_ratio = 0.33

            if high_ratio > 0.4:
                marker_type = "HiHat"
            elif mid_ratio > 0.4:
                marker_type = "Snare"
            else:
                marker_type = "Melodic"
        else:
            marker_type = "HiHat"

        # Scale intensity: onsets are secondary to beats
        intensity = round(float(strength) * 0.6, 2)
        intensity = max(0.15, min(0.65, intensity))

        markers.append({
            "beat": beat_pos,
            "type": marker_type,
            "intensity": intensity,
            "direction": 0.0,
            "holdBeats": 0.0
        })

    # Sort by beat position
    markers.sort(key=lambda m: m["beat"])
    print(f"Total markers: {len(markers)}")

    # --- Auto-detect sections via onset envelope energy ---
    # Simple approach: divide song into segments and classify energy
    total_beats = round(duration / sec_per_beat, 1)
    section_length_beats = 16  # 4 bars of 4/4
    sections = []
    
    num_sections = max(1, int(total_beats / section_length_beats))
    
    for i in range(num_sections):
        start_beat = round(i * section_length_beats, 1)
        end_beat = round(min((i + 1) * section_length_beats, total_beats), 1)
        
        # Calculate average energy in this section
        start_time = start_beat * sec_per_beat
        end_time = end_beat * sec_per_beat
        
        start_frame = librosa.time_to_frames(start_time, sr=sr)
        end_frame = librosa.time_to_frames(end_time, sr=sr)
        end_frame = min(end_frame, len(onset_env) - 1)
        
        if start_frame < end_frame:
            section_energy = np.mean(onset_env[start_frame:end_frame])
            max_energy = np.max(onset_env) if np.max(onset_env) > 0 else 1
            normalized_energy = section_energy / max_energy
        else:
            normalized_energy = 0.5
        
        # Classify section type by energy
        if i == 0:
            section_type = "Intro"
            highway = "EnemyOnly"
        elif i == num_sections - 1:
            section_type = "Outro"
            highway = "EnemyOnly"
        elif normalized_energy > 0.7:
            section_type = "Chorus"
            highway = "Both"
        elif normalized_energy < 0.3:
            section_type = "Break"
            highway = "EnemyOnly"
        else:
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
        "_comment": "Generated by beatmap_generator.py — review and adjust in Unity inspector",
        "songName": audio_path.rsplit("/", 1)[-1].rsplit(".", 1)[0],
        "bpm": bpm,
        "totalBeats": round(total_beats, 1),
        "durationSeconds": round(duration, 2),
        "leadInBeats": 4.0,
        "tailBeats": 2.0,
        "sections": sections,
        "markers": markers,
        "stats": {
            "totalMarkers": len(markers),
            "beats": len(beat_times),
            "onsets": len(onset_times),
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
    parser.add_argument("--output", type=str, default=None, help="Output JSON path")
    args = parser.parse_args()

    result = analyze(args.audio, args.bpm, args.sensitivity)

    output_path = args.output or args.audio.rsplit(".", 1)[0] + "_beatmap.json"
    with open(output_path, "w") as f:
        json.dump(result, f, indent=2)

    print(f"\nExported to: {output_path}")
    print(f"  Markers: {result['stats']['totalMarkers']}")
    print(f"  Sections: {len(result['sections'])}")
    print(f"  Notes at easy (0.3): ~{result['stats']['estimatedNotesAtDifficulty0.3']}")
    print(f"  Notes at normal (0.5): ~{result['stats']['estimatedNotesAtDifficulty0.5']}")
    print(f"  Notes at hard (0.8): ~{result['stats']['estimatedNotesAtDifficulty0.8']}")
    print(f"\nNext: create a SongBeatMap asset in Unity and copy these values in,")
    print(f"or use the JSON importer (if built) to auto-populate the ScriptableObject.")


if __name__ == "__main__":
    main()
