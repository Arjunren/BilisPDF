# BilisPDF commercial

30-second motion-graphics commercial for BilisPDF, with Higgsfield Seed Audio narration and an original instrumental electronic score. See [production-brief.md](production-brief.md) for the creative brief and [voiceover.txt](voiceover.txt) for the spoken script.

The animation uses the existing BilisPDF icon, illustrative document cards, and supported product features. The interface elements are stylized illustrations, not recorded application footage. The commercial makes no Windows verified-publisher claim.

## Re-render in the Higgsfield media sandbox

The sandbox supplies Python 3, Pillow, numpy, ffmpeg, Montserrat ExtraBold, and DejaVu Sans. This script does not install software or change BilisPDF.

1. Copy `assets/BilisPDF.png` into the render directory as `logo.png`.
2. Generate `voiceover.txt` with Higgsfield Seed Audio using the Grady preset, WAV at 48 kHz and speech rate `8`; save it as `voice-raw.wav`.
3. Run `python3 render.py --preview` and inspect `storyboard.jpg`.
4. Run `python3 render.py` for the 1920 × 1080, 30 fps H.264/AAC MP4 and audio stems.
5. Follow Higgsfield's upload workflow to export created media, then verify the finished file before displaying it.

The soundtrack is synthesized by `render.py` from original notes and percussion, with no imported music samples. Its 96 BPM, twelve-bar arrangement is exactly 30 seconds. Narration is delayed 300 ms; scene timings follow the generated delivery. A different delivery may require updating `STARTS` and scene timings.

Generated media and temporary files are ignored by Git. No social posting or release-asset upload is part of this render workflow.
