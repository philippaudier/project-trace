"""Temporary voice placeholders for TRACE Voice V0.1.

Synthesises short, clearly artificial vocal bursts (a glottal pulse train through three formant resonances
plus breath noise) so the voice system can be heard and tested before any recording exists. One file per
variant, named TMP_VO_<Prefix>_<Category>_NN.wav in Assets/TRACE/Audio/Voice/Temp, with a Unity .meta whose
GUID is derived from the file name (stable across regenerations and project copies).

Replace them by recording VO_<Prefix>_<Category>_NN.wav into the character folder and re-running
TRACE/Apply Voice V0.1: real files win over placeholders automatically.

Run from the repository root:  python Docs/Audio/voice_placeholders.py
"""
import hashlib
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "TRACE", "Audio", "Voice", "Temp")
RATE = 44100

# Character timbre: fundamental (Hz), formants (Hz), breath mix, overall softness.
CHARACTERS = {
    "TW": dict(f0=115.0, formants=(600.0, 1150.0, 2500.0), breath=0.10, soft=0.0),
    "Control": dict(f0=205.0, formants=(760.0, 1350.0, 2800.0), breath=0.08, soft=0.15),
    "Support": dict(f0=235.0, formants=(700.0, 1250.0, 2700.0), breath=0.16, soft=0.35),
}

# Category: variants, duration (s), attack (s), pitch contour (start, end multipliers), intensity, two-syllable.
CATEGORIES = {
    "AttackLight": (3, 0.20, 0.010, (1.08, 0.92), 1.0, False),
    "Dodge": (2, 0.16, 0.008, (1.00, 0.85), 0.7, False),
    "HurtLight": (3, 0.24, 0.006, (1.15, 0.95), 0.9, False),
    "HurtHeavy": (2, 0.45, 0.005, (1.20, 0.80), 1.0, False),
    "SkillPrimary": (2, 0.34, 0.015, (1.00, 1.06), 0.9, True),
    "SwitchIn": (2, 0.40, 0.025, (0.96, 1.04), 0.7, True),
    "LowHealth": (1, 0.50, 0.040, (0.95, 0.88), 0.55, True),
    "AllyDown": (1, 0.48, 0.012, (1.10, 0.90), 0.95, True),
    "CombatStart": (1, 0.38, 0.015, (1.00, 1.05), 0.8, True),
}


def resonator(signal, freq, bandwidth):
    """Second-order IIR band-pass (formant) filter."""
    r = np.exp(-np.pi * bandwidth / RATE)
    theta = 2.0 * np.pi * freq / RATE
    a1, a2 = -2.0 * r * np.cos(theta), r * r
    gain = (1.0 - r) * np.sqrt(1.0 - 2.0 * r * np.cos(2.0 * theta) + r * r)
    out = np.zeros_like(signal)
    y1 = y2 = 0.0
    for i, x in enumerate(signal):
        y = gain * x - a1 * y1 - a2 * y2
        out[i] = y
        y2, y1 = y1, y
    return out


def burst(character, category, variant, rng):
    c = CHARACTERS[character]
    count, duration, attack, (p0, p1), intensity, two = CATEGORIES[category]
    n = int(duration * RATE)
    t = np.arange(n) / RATE
    shift = 1.0 + rng.uniform(-0.06, 0.06)
    f0 = c["f0"] * shift * np.linspace(p0, p1, n) * (1.0 + 0.004 * np.sin(2 * np.pi * 5.5 * t))
    phase = np.cumsum(2 * np.pi * f0 / RATE)
    # Glottal-like pulse train: a sharpened saw with a few harmonics.
    pulse = sum(np.sin(k * phase) / k for k in range(1, 12))
    pulse = np.sign(pulse) * np.abs(pulse) ** 0.8
    voiced = np.zeros(n)
    for freq, bw in zip(c["formants"], (90.0, 120.0, 180.0)):
        voiced += resonator(pulse, freq * (1.0 + rng.uniform(-0.05, 0.05)), bw)
    breath = resonator(rng.standard_normal(n), 2200.0, 1400.0) * c["breath"] * 6.0
    signal = voiced + breath
    # Envelope: fast attack, sustain, release; two syllables when asked.
    env = np.ones(n)
    a = max(1, int(attack * RATE)); r = max(1, int(0.35 * n))
    env[:a] = np.linspace(0, 1, a)
    env[-r:] *= np.linspace(1, 0, r) ** (1.4 if c["soft"] < 0.2 else 2.0)
    if two:
        dip = int(0.45 * n); w = int(0.08 * n)
        env[dip - w:dip + w] *= np.concatenate([np.linspace(1, 0.25, w), np.linspace(0.25, 1, w)])
    signal *= env * (1.0 - 0.35 * c["soft"])
    # Soft characters get a gentle low-pass by averaging.
    if c["soft"] > 0:
        k = int(1 + 6 * c["soft"]); signal = np.convolve(signal, np.ones(k) / k, mode="same")
    peak = np.max(np.abs(signal)) or 1.0
    return signal / peak * (10 ** (-12 / 20)) * (0.75 + 0.25 * intensity)


def write(path, signal):
    data = (np.clip(signal, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 0
    compressionFormat: 2
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main():
    os.makedirs(OUT, exist_ok=True)
    made = 0
    for prefix in CHARACTERS:
        for category, (count, *_rest) in CATEGORIES.items():
            for variant in range(1, count + 1):
                name = f"TMP_VO_{prefix}_{category}_{variant:02d}"
                rng = np.random.default_rng(int(hashlib.md5(name.encode()).hexdigest()[:8], 16))
                write(os.path.join(OUT, name + ".wav"), burst(prefix, category, variant, rng))
                guid = hashlib.md5(("trace-voice-" + name).encode()).hexdigest()
                with open(os.path.join(OUT, name + ".wav.meta"), "w", newline="\n") as f:
                    f.write(META.format(guid=guid))
                made += 1
    print(f"{made} placeholder clips written to {OUT}")


if __name__ == "__main__":
    main()
