using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Analyzes an AudioClip at runtime and produces BeatMarkers for
    /// the marker-driven chart assembler.
    /// 
    /// Runs during the battle intro countdown (~2 seconds). For a typical
    /// 3-minute song at 44.1kHz, analysis completes in ~50-100ms.
    /// 
    /// Pipeline:
    ///   1. Read raw samples from AudioClip
    ///   2. Process in overlapping windows
    ///   3. Compute energy in 3 frequency bands (low/mid/high)
    ///   4. Detect onsets via spectral flux (energy increase between windows)
    ///   5. Classify onsets by dominant band (Kick/Snare/HiHat)
    ///   6. Quantize to nearest beat subdivision
    ///   7. Assign intensity from onset strength
    ///   8. Auto-detect sections from energy envelope
    /// 
    /// The output feeds directly into MarkerDrivenAssembler. When an enemy
    /// has no SongBeatMap, BattleManager calls this instead.
    /// 
    /// Pure C#, no MonoBehaviour. Deterministic (same audio = same markers).
    /// 
    /// SOLID:
    ///   S — Only analyzes audio. No chart assembly, no lane planning.
    ///   O — New analysis features (pitch tracking, etc.) add methods.
    ///   D — Returns generic BeatMarker/SongSection lists, no coupling.
    /// </summary>
    public static class RuntimeBeatAnalyzer
    {
        // =================================================================
        // CONFIG
        // =================================================================

        /// <summary>Window size in samples for energy analysis.</summary>
        private const int WindowSize = 1024;

        /// <summary>Hop between windows in samples (50% overlap).</summary>
        private const int HopSize = 512;

        /// <summary>
        /// How many standard deviations above the mean an onset must be.
        /// Lower = more sensitive (more markers). Higher = fewer, stronger markers.
        /// </summary>
        private const float OnsetThreshold = 1.2f;

        /// <summary>Minimum gap between onsets in seconds. Prevents double-triggers.</summary>
        private const float MinOnsetGapSeconds = 0.05f;

        /// <summary>Beat grid subdivision for quantization (4 = 16th notes, 2 = 8th notes).</summary>
        private const int BeatSubdivision = 4;

        /// <summary>Maximum quantization distance in beats. Onsets farther than this from
        /// the grid are kept at their raw position.</summary>
        private const float MaxQuantizeDistance = 0.12f;

        // Filter cutoff frequencies
        private const float LowCutoffHz = 200f;
        private const float MidCutoffHz = 2000f;

        // =================================================================
        // PUBLIC API
        // =================================================================

        /// <summary>
        /// Result of audio analysis — everything needed to build a chart.
        /// </summary>
        public struct AnalysisResult
        {
            /// <summary>Detected beat markers, sorted by beat position.</summary>
            public List<BeatMarker> Markers;

            /// <summary>Auto-detected song sections.</summary>
            public List<SongSection> Sections;

            /// <summary>Total duration in beats.</summary>
            public float TotalBeats;

            /// <summary>Whether the analysis succeeded.</summary>
            public bool Success;
        }

        /// <summary>
        /// Analyze an AudioClip and produce beat markers + sections.
        /// 
        /// Call during battle init (intro countdown). Typical runtime: 50-100ms.
        /// </summary>
        /// <param name="clip">The audio clip to analyze.</param>
        /// <param name="bpm">Known BPM of the song. Used for beat grid quantization.</param>
        /// <param name="sensitivity">
        /// 0.0 = only the strongest onsets, 1.0 = very sensitive.
        /// Maps to the onset detection threshold internally.
        /// </param>
        /// <returns>Analysis result with markers and sections.</returns>
        public static AnalysisResult Analyze(AudioClip clip, float bpm, float sensitivity = 0.5f)
        {
            var result = new AnalysisResult
            {
                Markers = new List<BeatMarker>(),
                Sections = new List<SongSection>(),
                Success = false
            };

            if (clip == null || bpm <= 0f)
            {
                Debug.LogError("[RuntimeBeatAnalyzer] Null clip or invalid BPM.");
                return result;
            }

            int sampleRate = clip.frequency;
            int channels = clip.channels;
            int totalSamples = clip.samples;

            // Read all samples (mono mixdown)
            float[] rawSamples = new float[totalSamples * channels];
            clip.GetData(rawSamples, 0);

            float[] mono = MixToMono(rawSamples, channels, totalSamples);

            float secPerBeat = 60f / bpm;
            float totalBeats = (totalSamples / (float)sampleRate) / secPerBeat;
            result.TotalBeats = totalBeats;

            // Compute energy per band per window
            int windowCount = (mono.Length - WindowSize) / HopSize + 1;
            if (windowCount <= 0)
            {
                Debug.LogWarning("[RuntimeBeatAnalyzer] Audio too short to analyze.");
                return result;
            }

            float[] lowEnergy = new float[windowCount];
            float[] midEnergy = new float[windowCount];
            float[] highEnergy = new float[windowCount];
            float[] totalEnergy = new float[windowCount];

            ComputeBandEnergies(mono, sampleRate, windowCount, lowEnergy, midEnergy, highEnergy, totalEnergy);

            // Detect onsets per band via spectral flux
            float adjustedThreshold = Mathf.Lerp(2.0f, 0.5f, sensitivity);
            float minGapWindows = MinOnsetGapSeconds * sampleRate / HopSize;

            var rawOnsets = DetectOnsets(
                lowEnergy, midEnergy, highEnergy, totalEnergy,
                windowCount, adjustedThreshold, minGapWindows);

            // Convert to beat markers with quantization
            result.Markers = ConvertToMarkers(rawOnsets, sampleRate, bpm, totalEnergy);

            // Auto-detect sections from energy envelope
            result.Sections = DetectSections(totalEnergy, windowCount, sampleRate, bpm, totalBeats);

            result.Success = true;

            Debug.Log($"[RuntimeBeatAnalyzer] Analyzed '{clip.name}': " +
                      $"{result.Markers.Count} markers, {result.Sections.Count} sections, " +
                      $"{totalBeats:F0} beats at {bpm} BPM (sensitivity {sensitivity:F1})");

            return result;
        }

        // =================================================================
        // MONO MIXDOWN
        // =================================================================

        private static float[] MixToMono(float[] raw, int channels, int sampleCount)
        {
            if (channels == 1) return raw;

            float[] mono = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                    sum += raw[i * channels + ch];
                mono[i] = sum / channels;
            }
            return mono;
        }

        // =================================================================
        // BAND ENERGY COMPUTATION
        // =================================================================

        /// <summary>
        /// Compute RMS energy per frequency band using simple IIR filters.
        /// Low: &lt;200Hz (kicks), Mid: 200-2000Hz (snares), High: &gt;2000Hz (hihats).
        /// </summary>
        private static void ComputeBandEnergies(
            float[] samples, int sampleRate, int windowCount,
            float[] lowEnergy, float[] midEnergy, float[] highEnergy, float[] totalEnergy)
        {
            // Precompute filter coefficients (single-pole IIR)
            float alphaLow = GetFilterAlpha(LowCutoffHz, sampleRate);
            float alphaMid = GetFilterAlpha(MidCutoffHz, sampleRate);

            for (int w = 0; w < windowCount; w++)
            {
                int startSample = w * HopSize;
                int endSample = Mathf.Min(startSample + WindowSize, samples.Length);

                float lowSum = 0f;
                float midSum = 0f;
                float highSum = 0f;
                float fullSum = 0f;

                // Apply filters within the window
                float lowState = 0f;
                float midState = 0f;

                for (int i = startSample; i < endSample; i++)
                {
                    float s = samples[i];

                    // Lowpass at 200Hz → kick band
                    lowState += alphaLow * (s - lowState);
                    float low = lowState;

                    // Lowpass at 2000Hz
                    midState += alphaMid * (s - midState);

                    // Mid = lowpass@2000 - lowpass@200
                    float mid = midState - low;

                    // High = original - lowpass@2000
                    float high = s - midState;

                    lowSum += low * low;
                    midSum += mid * mid;
                    highSum += high * high;
                    fullSum += s * s;
                }

                int count = endSample - startSample;
                lowEnergy[w] = Mathf.Sqrt(lowSum / count);
                midEnergy[w] = Mathf.Sqrt(midSum / count);
                highEnergy[w] = Mathf.Sqrt(highSum / count);
                totalEnergy[w] = Mathf.Sqrt(fullSum / count);
            }
        }

        /// <summary>Single-pole IIR filter coefficient for a given cutoff frequency.</summary>
        private static float GetFilterAlpha(float cutoffHz, int sampleRate)
        {
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float dt = 1f / sampleRate;
            return dt / (rc + dt);
        }

        // =================================================================
        // ONSET DETECTION
        // =================================================================

        private struct RawOnset
        {
            public int WindowIndex;
            public float Strength;       // How strong the onset is (0-1 normalized)
            public MarkerType Type;       // Classified by dominant band
        }

        /// <summary>
        /// Detect onsets via spectral flux (positive energy increase per band).
        /// An onset is flagged when the flux exceeds mean + threshold * stddev.
        /// </summary>
        private static List<RawOnset> DetectOnsets(
            float[] lowE, float[] midE, float[] highE, float[] totalE,
            int windowCount, float threshold, float minGapWindows)
        {
            // Compute spectral flux per band (only positive changes)
            float[] lowFlux = new float[windowCount];
            float[] midFlux = new float[windowCount];
            float[] highFlux = new float[windowCount];
            float[] totalFlux = new float[windowCount];

            for (int i = 1; i < windowCount; i++)
            {
                lowFlux[i] = Mathf.Max(0f, lowE[i] - lowE[i - 1]);
                midFlux[i] = Mathf.Max(0f, midE[i] - midE[i - 1]);
                highFlux[i] = Mathf.Max(0f, highE[i] - highE[i - 1]);
                totalFlux[i] = Mathf.Max(0f, totalE[i] - totalE[i - 1]);
            }

            // Compute adaptive threshold (mean + k * stddev over a sliding window)
            int adaptiveWindow = 40; // ~1 second of context
            float[] adaptiveThresholdValues = new float[windowCount];

            for (int i = 0; i < windowCount; i++)
            {
                int start = Mathf.Max(0, i - adaptiveWindow);
                int end = Mathf.Min(windowCount, i + adaptiveWindow);
                int count = end - start;

                float mean = 0f;
                for (int j = start; j < end; j++)
                    mean += totalFlux[j];
                mean /= count;

                float variance = 0f;
                for (int j = start; j < end; j++)
                {
                    float d = totalFlux[j] - mean;
                    variance += d * d;
                }
                float stddev = Mathf.Sqrt(variance / count);

                adaptiveThresholdValues[i] = mean + threshold * stddev;
            }

            // Peak picking with minimum gap
            var onsets = new List<RawOnset>();
            int lastOnsetWindow = -100;
            float maxFlux = 0f;

            // Find max flux for normalization
            for (int i = 0; i < windowCount; i++)
                if (totalFlux[i] > maxFlux) maxFlux = totalFlux[i];

            if (maxFlux <= 0f) return onsets;

            for (int i = 1; i < windowCount - 1; i++)
            {
                // Must be above adaptive threshold
                if (totalFlux[i] <= adaptiveThresholdValues[i]) continue;

                // Must be a local peak
                if (totalFlux[i] <= totalFlux[i - 1] || totalFlux[i] < totalFlux[i + 1]) continue;

                // Minimum gap between onsets
                if (i - lastOnsetWindow < minGapWindows) continue;

                // Classify by dominant band flux
                MarkerType type;
                float lf = lowFlux[i];
                float mf = midFlux[i];
                float hf = highFlux[i];
                float bandTotal = lf + mf + hf;

                if (bandTotal <= 0f)
                {
                    type = MarkerType.Accent;
                }
                else
                {
                    float lowRatio = lf / bandTotal;
                    float midRatio = mf / bandTotal;
                    float highRatio = hf / bandTotal;

                    if (lowRatio > 0.45f)
                        type = MarkerType.Kick;
                    else if (midRatio > 0.4f)
                        type = MarkerType.Snare;
                    else if (highRatio > 0.45f)
                        type = MarkerType.HiHat;
                    else
                        type = MarkerType.Accent;
                }

                float strength = totalFlux[i] / maxFlux;

                onsets.Add(new RawOnset
                {
                    WindowIndex = i,
                    Strength = strength,
                    Type = type
                });

                lastOnsetWindow = i;
            }

            return onsets;
        }

        // =================================================================
        // MARKER CONVERSION + QUANTIZATION
        // =================================================================

        /// <summary>
        /// Convert raw onsets to BeatMarkers with beat grid quantization.
        /// </summary>
        private static List<BeatMarker> ConvertToMarkers(
            List<RawOnset> onsets, int sampleRate, float bpm, float[] totalEnergy)
        {
            var markers = new List<BeatMarker>(onsets.Count);

            float secPerBeat = 60f / bpm;
            float secPerWindow = (float)HopSize / sampleRate;
            float subdivisionSize = 1f / BeatSubdivision; // e.g. 0.25 for 16th notes

            // Track used beat positions to avoid duplicates
            var usedBeats = new HashSet<float>();

            for (int i = 0; i < onsets.Count; i++)
            {
                RawOnset onset = onsets[i];

                // Convert window index to time, then to beat position
                float timeSec = onset.WindowIndex * secPerWindow;
                float rawBeat = timeSec / secPerBeat;

                // Quantize to nearest subdivision
                float quantized = Mathf.Round(rawBeat / subdivisionSize) * subdivisionSize;
                float quantizeError = Mathf.Abs(rawBeat - quantized);

                // Use quantized position if close enough, otherwise keep raw
                float finalBeat = quantizeError <= MaxQuantizeDistance ? quantized : rawBeat;
                finalBeat = Mathf.Round(finalBeat * 100f) / 100f; // Round to 2 decimals

                // Skip duplicate beat positions
                if (usedBeats.Contains(finalBeat)) continue;
                usedBeats.Add(finalBeat);

                // Map strength to intensity (0.2 to 1.0 range)
                float intensity = Mathf.Lerp(0.2f, 1f, onset.Strength);

                markers.Add(new BeatMarker(
                    beat: finalBeat,
                    type: onset.Type,
                    intensity: intensity
                ));
            }

            markers.Sort();
            return markers;
        }

        // =================================================================
        // SECTION DETECTION
        // =================================================================

        /// <summary>
        /// Auto-detect sections from the energy envelope.
        /// Divides the song into 8-beat chunks and classifies by energy level.
        /// </summary>
        private static List<SongSection> DetectSections(
            float[] totalEnergy, int windowCount, int sampleRate,
            float bpm, float totalBeats)
        {
            var sections = new List<SongSection>();

            float secPerBeat = 60f / bpm;
            float secPerWindow = (float)HopSize / sampleRate;
            float sectionLengthBeats = 16f; // 4 bars of 4/4

            int numSections = Mathf.Max(1, Mathf.FloorToInt(totalBeats / sectionLengthBeats));

            // Compute average energy per section
            float[] sectionEnergies = new float[numSections];
            float maxSectionEnergy = 0f;

            for (int s = 0; s < numSections; s++)
            {
                float startBeat = s * sectionLengthBeats;
                float endBeat = Mathf.Min((s + 1) * sectionLengthBeats, totalBeats);

                float startSec = startBeat * secPerBeat;
                float endSec = endBeat * secPerBeat;

                int startWindow = Mathf.FloorToInt(startSec / secPerWindow);
                int endWindow = Mathf.Min(Mathf.FloorToInt(endSec / secPerWindow), windowCount - 1);

                float sum = 0f;
                int count = 0;
                for (int w = startWindow; w <= endWindow; w++)
                {
                    sum += totalEnergy[w];
                    count++;
                }

                sectionEnergies[s] = count > 0 ? sum / count : 0f;
                if (sectionEnergies[s] > maxSectionEnergy)
                    maxSectionEnergy = sectionEnergies[s];
            }

            // Normalize and classify
            for (int s = 0; s < numSections; s++)
            {
                float startBeat = s * sectionLengthBeats;
                float endBeat = Mathf.Min((s + 1) * sectionLengthBeats, totalBeats);

                float normalized = maxSectionEnergy > 0f
                    ? sectionEnergies[s] / maxSectionEnergy
                    : 0.5f;

                SongSectionType type;
                SectionType highway;

                if (s == 0)
                {
                    type = SongSectionType.Intro;
                    highway = SectionType.EnemyOnly;
                }
                else if (s == numSections - 1)
                {
                    type = SongSectionType.Outro;
                    highway = SectionType.EnemyOnly;
                }
                else if (normalized > 0.7f)
                {
                    type = SongSectionType.Chorus;
                    highway = SectionType.Both;
                }
                else if (normalized < 0.25f)
                {
                    type = SongSectionType.Break;
                    highway = SectionType.EnemyOnly;
                }
                else
                {
                    type = SongSectionType.Verse;
                    highway = SectionType.PlayerOnly;
                }

                sections.Add(new SongSection
                {
                    label = $"{type} {s + 1}",
                    startBeat = startBeat,
                    endBeat = endBeat,
                    type = type,
                    highway = highway,
                    intensityScale = Mathf.Clamp(normalized + 0.5f, 0.5f, 1.5f)
                });
            }

            return sections;
        }
    }
}
