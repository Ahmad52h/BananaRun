"""Create original, replaceable BananaRun starter audio with no external dependencies."""
from array import array
from pathlib import Path
import math
import wave

RATE = 22050
ROOT = Path(__file__).resolve().parents[1] / 'Assets' / '_Game' / 'Audio'
ROOT.mkdir(parents=True, exist_ok=True)


def write(name, samples):
    data = array('h', (int(max(-1, min(1, s)) * 32767) for s in samples))
    with wave.open(str(ROOT / name), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(data.tobytes())


def bell(t, frequency):
    if t < 0:
        return 0
    attack = min(1, t / 0.006)
    return attack * math.exp(-7 * t) * (math.sin(math.tau * frequency * t)
        + 0.22 * math.sin(math.tau * frequency * 2 * t) * math.exp(-8 * t))


def pickup(t):
    return 0.32 * (bell(t, 880) + bell(t - 0.055, 1318.51) + bell(t - 0.11, 1760)) * min(1, (0.48 - t) / 0.05)


write('BananaCollect.wav', (pickup(i / RATE) for i in range(int(RATE * 0.48))))

notes = [72, 76, 79, 76, 74, 79, 81, 79, 72, 76, 79, 84, 81, 79, 76, 74]
chords = [(48, 55, 60), (45, 52, 57), (41, 48, 53), (43, 50, 55)]


def music(t):
    step = int(t / 0.375)
    tone = 0
    for previous in range(max(0, step - 3), step + 1):
        frequency = 440 * 2 ** ((notes[previous % len(notes)] - 69) / 12)
        tone += 0.16 * bell(t - previous * 0.375, frequency)
    chord_time = t % 6
    envelope = min(1, chord_time / 0.7) * min(1, (6 - chord_time) / 0.8)
    for midi in chords[int(t / 6) % 4]:
        tone += 0.055 * math.sin(math.tau * 440 * 2 ** ((midi - 69) / 12) * t) * envelope
    beat = t % 0.75
    tone += 0.07 * math.sin(math.tau * 95 * beat) * math.exp(-24 * beat)
    return tone * min(1, t / 0.02) * min(1, (24 - t) / 0.12)


write('WoodlandLoop.wav', (music(i / RATE) for i in range(RATE * 24)))
print('Generated WoodlandLoop.wav (24s) and BananaCollect.wav (0.48s).')
