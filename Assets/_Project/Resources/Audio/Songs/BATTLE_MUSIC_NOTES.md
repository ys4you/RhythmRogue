# Battle Music — Placeholder Assignments

Working notes for wiring the seven synthwave tracks into the prototype as placeholders.
All assignments are provisional and exist to make playtesting feel right — final music is a
post-playtest decision (see caveats at the bottom).

Analysis was done on the actual audio (tempo, beat steadiness, pulse clarity, spectral
brightness, bass weight, major/minor mode), not just the track names. Names mislead here:
"Stellar Saga" sounds spacey but is actually the DARKEST track; "Bionic Beatdown" sounds
aggressive but is the BRIGHTEST and only solidly major-key one.

---

## The data

| Track                 | BPM  | Beat | Pulse | Brightness | Mode  | Feel |
|-----------------------|------|------|-------|------------|-------|------|
| Stellar Saga          | ~99  | tight| mid   | darkest    | minor | slow, heavy, ominous |
| Fight For The Future  | ~112 | tight| mid   | mid        | minor | mid-tempo, driving |
| Digital Driving Force | ~112 | tight| low   | dark       | major | mid-tempo, slightly brighter |
| Neuro-Synth           | ~117 | tight| HIGH  | mid        | minor | punchiest, loudest, very tappable |
| Crash Runner          | ~144 | tight| mid   | dark       | minor | fast, dark |
| Bionic Beatdown       | ~144 | tight| mid   | brightest  | major | fast but upbeat — least on-vibe |
| Obsidian Overdrive    | ~161 | tight| high  | mid        | minor | fastest, aggressive |

Every track has a steady, clearly detectable beat — all are chartable and will lock to the
Conductor cleanly. No technical duds.

---

## Placeholder assignments by enemy type

Enemy archetypes (GDD) sort by BPM band, which lines up almost perfectly with these tracks.

### Slow enemy (GDD 80–100, sparse/ritualistic)
- **Stellar Saga (~99)** — primary. Darkest of the set, minor key, slow and heavy. Reads as
  ominous despite the name. Best dark-fit overall.

### Trick enemy (GDD 110–130, deceptive/unsettling)
- **Fight For The Future (~112)** — primary. Minor key, mid-tempo, tense.
- **Digital Driving Force (~112)** — alternate (major key, a little brighter / "off").

### Speed enemy (GDD 140–180, dense/aggressive)
- **Obsidian Overdrive (~161)** — primary. Fastest, most aggressive, minor key.
- **Crash Runner (~144)** — secondary. Fast and dark.
- **Bionic Beatdown (~144)** — last resort only. Brightest/major, least gothic. Audition for
  tonal clash before using.

### Chaos enemy (GDD variable)
- No fixed pick — pull from any per phase. Obsidian Overdrive or Crash Runner fit best.

### Default / tutorial / first battle
- **Neuro-Synth** — highest pulse clarity, loudest, most obviously "tap to this." Best first
  impression. Use as the fallback battle track when an enemy has none assigned.

### Boss (placeholder)
- **Obsidian Overdrive** — most intense available. A real boss eventually wants something
  bespoke with phase/tempo changes per the GDD; treat this as temporary.

---

## Aesthetic caveat (before shipping)

These are SYNTHWAVE / cyberpunk, not gothic/occult. They work technically and the minor-key
ones read as "dark and driving," which can support an occult battle — but the genre leans
sci-fi, not Castlevania-to-Inscryption. Acceptable placeholder mismatch for a feel-test, and
a real question to put to playtesters: "did the music fit the game?" Lean on the minor-key
tracks (Stellar Saga, Crash Runner, Obsidian Overdrive, Neuro-Synth, Fight For The Future);
the two major-key ones (Bionic Beatdown, Digital Driving Force) clash most.

## Licensing (before shipping)
Likely a royalty-free library set ("psychronic" artist, numeric IDs — probably Pixabay-ish).
Fine for a playtest build. Before shipping, confirm each track's license
(CC0 / CC-BY-with-credit / purchased) and record the source per track.

## Wiring notes
- Place the audio files in this folder (Resources/Audio/Songs/).
- Tracks are BPM-agnostic to the chart system, but each needs its CONSTANT BPM set correctly
  so the Conductor / RuntimeBeatAnalyzer lock on. Use the BPM column above as the starting
  value; fine-tune in-engine if the first beat drifts.
- OGG preferred for the final pipeline (GDD: OGG/WAV for precise timing); high-quality MP3 is
  fine as a placeholder.
