"""Day -> night strobe cut (owner, 27 Nov 2026: "subeh ka video dikhaate hain or phir jhatke se same position pe raat
strobe krti hai or raat ka chlne lgta hai").

The day and night films come from the same timeline frame by frame (CinemaRunner records the director at s0 + i/60), so
at any time both show the camera at the same place: the day plays up to --at, then a short strobe flips between the two
(a white flash on the first night frame), then the night plays on.

  python Tools/pipeline/daynight_strobe.py DAY.mp4 NIGHT.mp4 OUT.mp4 --at 50 [--pattern 2,3,3,2,4,2,5,1] [--flash 0.6]

--pattern: frame counts at 60 fps, night first, then day, night, day...; after it the night stays.
"""
import argparse
import os
import shutil
import subprocess
import sys


def ffmpeg_path():
    f = shutil.which('ffmpeg')
    if f:
        return f
    pk = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Microsoft', 'WinGet', 'Packages')
    if os.path.isdir(pk):
        for d in os.listdir(pk):
            if d.startswith('Gyan.FFmpeg'):
                for r, _, files in os.walk(os.path.join(pk, d)):
                    if 'ffmpeg.exe' in files:
                        return os.path.join(r, 'ffmpeg.exe')
    return 'ffmpeg'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('day')
    ap.add_argument('night')
    ap.add_argument('out')
    ap.add_argument('--at', type=float, required=True, help='seconds: the first night frame')
    ap.add_argument('--pattern', default='2,3,3,2,4,2,5,1')
    ap.add_argument('--flash', type=float, default=0.6, help='white flash strength on the first night frames (0 = none)')
    ap.add_argument('--fps', type=int, default=60)
    a = ap.parse_args()

    fps = a.fps
    f0 = round(a.at * fps)
    counts = [int(x) for x in a.pattern.split(',') if x.strip()]
    # the day shows before f0 and in the strobe's day runs (odd entries); half-frame edges keep t exact
    day = [f'lt(t,{(f0 - 0.5) / fps:.6f})']
    f = f0
    for i, n in enumerate(counts):
        if i % 2 == 1:
            day.append(f'between(t,{(f - 0.5) / fps:.6f},{(f + n - 0.5) / fps:.6f})')
        f += n
    enable = '+'.join(day)
    flash_end = (f0 + 2 - 0.5) / fps
    vf = f"[1:v][0:v]overlay=0:0:enable='{enable}'"
    if a.flash > 0:
        vf += f",drawbox=x=0:y=0:w=iw:h=ih:color=white@{a.flash:.2f}:t=fill:enable='between(t,{(f0 - 0.5) / fps:.6f},{flash_end:.6f})'"
    vf += '[v]'
    cmd = [ffmpeg_path(), '-y', '-loglevel', 'error', '-stats', '-i', a.day, '-i', a.night, '-filter_complex', vf, '-map', '[v]',
           '-c:v', 'libx264', '-preset', 'medium', '-crf', '15', '-tune', 'film', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
           '-movflags', '+faststart', a.out]
    print(' '.join(cmd))
    sys.exit(subprocess.call(cmd))


if __name__ == '__main__':
    main()
