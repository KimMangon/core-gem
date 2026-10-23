"""Original synthesized stone fracture: heavy impact, brittle cracks and falling grit."""
import math
import random
import struct
import wave
from pathlib import Path

rate = 44100
duration = 1.12
rng = random.Random(17123)
samples = [0.0] * int(rate * duration)
low = 0.0
previous = 0.0
for i in range(len(samples)):
    t = i / rate
    noise = rng.uniform(-1, 1)
    low += .09 * (noise - low)
    high = noise - previous
    previous = noise
    attack = min(1.0, t / .002)
    thud = math.sin(2 * math.pi * (105 * t - 25 * t * t)) * math.exp(-t * 17)
    samples[i] = attack * (.52 * thud + .85 * low * math.exp(-t * 8)
                          + .12 * high * math.exp(-t * 34))

# A tightly grouped initial split followed by quieter irregular gravel snaps.
for start, strength, decay in [(0.003, .70, 260), (.021, .48, 180), (.057, .44, 145),
                               (.10, .33, 130), (.16, .25, 150), (.24, .17, 160),
                               (.33, .12, 175), (.46, .08, 210), (.61, .05, 230)]:
    begin = int(start * rate)
    memory = 0.0
    for j in range(int(.12 * rate)):
        index = begin + j
        if index >= len(samples):
            break
        t = j / rate
        noise = rng.uniform(-1, 1)
        memory += .3 * (noise - memory)
        snap = .72 * memory + .28 * noise
        samples[index] += strength * snap * math.exp(-t * decay)

peak = max(abs(s) for s in samples)
pcm = bytearray()
for i, sample in enumerate(samples):
    fade = min(1, (len(samples) - 1 - i) / (rate * .06))
    pcm.extend(struct.pack('<h', round(sample / peak * .87 * fade * 32767)))

target = Path(__file__).resolve().parents[1] / 'Assets/Resources/Sfx/BoulderBreak.wav'
target.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(target), 'wb') as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes(pcm)
print(target)
