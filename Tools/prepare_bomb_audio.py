"""Prepare short, click-free PCM bomb cues from the project's supplied sounds."""
from pathlib import Path
import subprocess

audio = Path(__file__).resolve().parents[1] / "Assets" / "_Game" / "Audio"
cues = [
    ("bomb sound.mp3", "BombExplosion.wav",
     "highpass=f=55,lowpass=f=11500,afftdn=nf=-48:nr=8,atrim=0:1.98,asetpts=PTS-STARTPTS,afade=t=in:d=0.003,afade=t=out:st=1.90:d=0.08"),
    ("Whoosh.wav", "BombDrop.wav",
     "atrim=0:0.22,asetpts=PTS-STARTPTS,highpass=f=100,lowpass=f=7000,afade=t=in:d=0.008,afade=t=out:st=0.15:d=0.07"),
]
for source, output, filters in cues:
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(audio / source),
                    "-af", filters, "-ar", "48000", "-ac", "1", "-c:a", "pcm_s16le", str(audio / output)], check=True)
    print(output)
