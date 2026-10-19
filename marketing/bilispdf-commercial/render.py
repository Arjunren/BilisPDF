"""Render the BilisPDF commercial in Higgsfield's media sandbox.

Requires Pillow, numpy, ffmpeg, the existing logo.png, and generated voice-raw.wav.
Run: python3 render.py --preview or python3 render.py
"""
import argparse
import functools
import math
import subprocess
import wave
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parent
S, FPS, SECONDS = 2, 30, 30
W, H = 960 * S, 540 * S
TEAL, INK, CORAL, PALE = '#0F766E', '#19313E', '#F97354', '#F3F7F8'
BOLD = '/usr/share/fonts/truetype/higgsfield/Montserrat-ExtraBold.ttf'
BODY = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
LOGO = Image.open(ROOT / 'logo.png').convert('RGBA')
STARTS = [0, 3, 7, 12.5, 15.5, 19.8, 22.5, 30]


def ease(x):
    return 1 - (1 - max(0, min(1, x))) ** 3


@functools.lru_cache(maxsize=64)
def font(size, bold=True):
    return ImageFont.truetype(BOLD if bold else BODY, round(size * S))


def text(im, xy, value, size=24, color=INK, bold=True, anchor='la'):
    ImageDraw.Draw(im).text(tuple(round(v * S) for v in xy), value,
                            font=font(size, bold), fill=color, anchor=anchor)


def box(im, xy, fill, radius=16, outline=None, width=1):
    ImageDraw.Draw(im).rounded_rectangle(tuple(round(v*S) for v in xy),
        radius=round(radius*S), fill=fill, outline=outline, width=width*S)


def line(im, points, fill, width=2):
    ImageDraw.Draw(im).line([(round(x*S), round(y*S)) for x, y in points],
                            fill=fill, width=round(width*S), joint='curve')


def logo(im, x, y, size):
    a = LOGO.resize((round(size*S), round(size*S)), Image.Resampling.LANCZOS)
    im.paste(a, (round(x*S), round(y*S)), a)


def pill(im, x, y, value, dark=False):
    tw = ImageDraw.Draw(im).textlength(value, font=font(12)) / S
    box(im, (x, y, x+tw+28, y+30), '#246F6B' if dark else '#E3EFEC', 15)
    text(im, (x+14, y+8), value, 12, 'white' if dark else TEAL)
    return tw+40


@functools.lru_cache(maxsize=16)
def paper(label='PDF', color=CORAL):
    im = Image.new('RGBA', (180*S, 220*S))
    sh = Image.new('RGBA', im.size)
    ImageDraw.Draw(sh).rounded_rectangle((22*S, 18*S, 158*S, 202*S),
                                         12*S, fill=(15, 55, 53, 32))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10*S)))
    box(im, (22, 10, 158, 194), 'white', 12, '#DFE7EB')
    box(im, (38, 28, 104, 57), color, 6)
    text(im, (46, 34), label, 14, 'white')
    for j in range(5):
        box(im, (38, 78+j*17, 138-(j%2)*22, 83+j*17), '#DFE7EB', 2)
    return im


def doc(im, x, y, label='PDF', color=CORAL, scale=1, angle=0):
    a = paper(label, color)
    if scale != 1:
        a = a.resize((round(a.width*scale), round(a.height*scale)), Image.Resampling.BICUBIC)
    if abs(angle) > .1:
        a = a.rotate(angle, Image.Resampling.BICUBIC, expand=True)
    im.paste(a, (round(x*S-a.width/2), round(y*S-a.height/2)), a)


def background(dark=False):
    im = Image.new('RGB', (W, H), TEAL if dark else PALE)
    d = ImageDraw.Draw(im)
    c = '#227F77' if dark else '#E6EFEE'
    for r in (210, 285, 360):
        d.ellipse(((777-r)*S, (246-r)*S, (777+r)*S, (246+r)*S), outline=c, width=S)
    return im


LIGHT, DARK = background(), background(True)


def header(im, section, dark=False):
    logo(im, 48, 34, 28)
    text(im, (87, 39), 'BilisPDF', 15, 'white' if dark else INK)
    text(im, (910, 40), section.upper(), 10, '#B8DBD5' if dark else '#58736F', anchor='ra')


def tick(im, x, y, color='white', scale=1):
    line(im, [(x,y+8*scale),(x+8*scale,y+16*scale),(x+25*scale,y-3*scale)],color,3*scale)


def scene(idx, u):
    dark = idx >= 5
    im = (DARK if dark else LIGHT).copy()
    move = 25 * (1-ease(u/0.7))
    if idx == 0:
        header(im, 'A little less paperwork')
        pill(im, 64, 151+move, 'WORK SMARTER')
        text(im, (60, 205+move), 'Too many', 57)
        text(im, (60, 272+move), 'files?', 80, TEAL)
        text(im, (64, 385+move), 'Not enough time?', 22, '#58736F', False)
        align = ease((u-1.8)/1.05)
        for j,(x,y,a,label,c) in enumerate([(635,220,15,'DOCX',TEAL),(802,303,-16,'PDF',CORAL),(651,390,-8,'IMG','#548896')]):
            doc(im, x+(720-x)*align, y+(265+j*13-y)*align,
                label,c,.94,a*(1-align)+2*math.sin(u+j)*(1-align))
    elif idx == 1:
        header(im, 'Free PDF tools for Windows')
        text(im, (64, 155+move), 'Meet', 26, '#58736F', False)
        text(im, (60, 199+move), 'BilisPDF', 67, TEAL)
        text(im, (64, 299+move), 'Your everyday PDF toolkit.', 23, INK, False)
        text(im, (64, 339+move), 'Right on your Windows PC.', 20, '#58736F', False)
        x=64
        for p in ['FREE', 'OFFLINE', 'OPEN SOURCE']:
            x += pill(im,x,408+move,p)
        logo(im, 635, 162+7*math.sin(u*1.6), 220)
        box(im,(676,420,815,425),CORAL,2)
    elif idx == 2:
        header(im, 'One toolkit. Less friction.')
        text(im,(64,112+move),'Make every file',42)
        text(im,(64,165+move),'work for you.',42,TEAL)
        labels=['Convert','Merge','Extract','Rotate','Images to PDF']
        active = min(4, int(max(0,u-.12)/1.04))
        for j,label in enumerate(labels):
            x=54+j*173
            rise = 18*(1-ease((u-j*.07)/.5))
            box(im,(x,252+rise,x+160,445+rise),TEAL if j==active else 'white',15,
                None if j==active else '#DFE7EB')
            col='white' if j==active else TEAL
            cx=x+80; yy=320+rise
            if j==0:
                text(im,(cx,yy-18),'DOC',22,col,anchor='ma')
                line(im,[(cx-22,yy+18),(cx+23,yy+18)],col,3)
                line(im,[(cx+15,yy+10),(cx+23,yy+18),(cx+15,yy+26)],col,3)
            elif j==1:
                box(im,(cx-35,yy-25,cx-1,yy+19),None,3,col,2)
                box(im,(cx+4,yy-14,cx+38,yy+30),None,3,col,2)
                text(im,(cx,yy+43),'+',18,col,anchor='ma')
            elif j==2:
                for k in [2,1,0]:
                    box(im,(cx-25+k*8,yy-24-k*7,cx+19+k*8,yy+30-k*7),TEAL if j==active else 'white',3,col,2)
            elif j==3:
                ImageDraw.Draw(im).arc(((cx-32)*S,(yy-33)*S,(cx+32)*S,(yy+31)*S),30,310,fill=col,width=3*S)
                line(im,[(cx+15,yy-32),(cx+27,yy-27),(cx+25,yy-41)],col,3)
                text(im,(cx,yy-8),'90°',16,col,anchor='ma')
            else:
                box(im,(cx-36,yy-24,cx+36,yy+28),None,5,col,2)
                line(im,[(cx-28,yy+19),(cx-8,yy-4),(cx+6,yy+8),(cx+18,yy-7),(cx+30,yy+19)],col,3)
            text(im,(cx,397+rise),label,15,col,anchor='ma')
        text(im,(64,477),'Office conversion requires Microsoft Office or LibreOffice.',11,'#58736F',False)
    elif idx == 3:
        header(im,'Fits the way you work')
        text(im,(64,162+move),'Right-click.',48)
        text(im,(64,222+move),'Drag. Drop.',48,TEAL)
        text(im,(64,292+move),'Done.',64,TEAL)
        # A stylized illustration of the existing shell action and drop-zone copy.
        box(im,(565,125,859,228),'#213B45',18)
        text(im,(585,140),'Open with',13,'#B8CED2',False)
        box(im,(578,174,846,215),TEAL,8)
        logo(im,590,179,28)
        text(im,(630,184),'BilisPDF',17,'white')
        text(im,(827,184),'›',20,'white')
        box(im,(545,298,880,449),'#E3EFEC',16,TEAL,2)
        text(im,(712,391),'Drop your files here',18,TEAL,anchor='ma')
        p=ease((u-.4)/1.65)
        doc(im,720,240+95*p,'PDF',CORAL,.48,-8*(1-p))
        if u>2.1:
            box(im,(815,315,854,354),TEAL,20)
            tick(im,825,327,scale=.7)
    elif idx == 4:
        header(im,'Local by design')
        text(im,(64,156+move),'Your files.',51)
        text(im,(64,218+move),'Your computer.',47,TEAL)
        text(im,(64,300+move),'Keep your workflow close.',19,'#58736F',False)
        x=pill(im,64,365,'NO UPLOADS')
        pill(im,64+x,365,'NO ACCOUNTS')
        box(im,(584,166,869,363),INK,18)
        box(im,(595,178,858,347),'#E3EFEC',10)
        for j in range(3):
            doc(im,654+j*61,264+5*math.sin(u*1.6+j),'PDF',TEAL,.42,(j-1)*6)
        box(im,(562,364,892,383),'#58736F',6)
        box(im,(674,364,779,370),'#B8D8D3',3)
        tick(im,714,410,TEAL,1)
        text(im,(755,406),'100% local',14,TEAL)
    elif idx == 5:
        header(im,'Make room for progress',True)
        text(im,(64,164+move),'Less paperwork.',55,'#B8DBD5')
        text(im,(60,237+move),'More progress.',65,'white')
        width=710*ease(u/.85)
        box(im,(64,338,64+max(3,width),345),CORAL,3)
        for j in range(3):
            box(im,(70+j*67,393,112+j*67,435),'#2B8780',12)
            tick(im,79+j*67,405,'white',.9)
    else:
        header(im,'Free. Offline. Open source.',True)
        logo(im,414,98+move,132)
        text(im,(480,248+move),'BilisPDF',59,'white',anchor='ma')
        text(im,(480,325+move),'Your files. Your computer.',23,'#D9EDE9',False,anchor='ma')
        if u>.45:
            y=386+12*(1-ease((u-.45)/.6))
            box(im,(362,y,598,y+49),CORAL,25)
            text(im,(480,y+12),'Download free  ↓',18,INK,anchor='ma')
            text(im,(480,460),'github.com/Arjunren/BilisPDF',20,'white',False,anchor='ma')
        text(im,(480,509),'Unsigned preview · Office files require Office or LibreOffice.',10,'#B8DBD5',False,anchor='ma')
    return im


def frame(t):
    i=next(i for i in range(7) if STARTS[i]<=t<STARTS[i+1])
    u=t-STARTS[i]
    im=scene(i,u)
    if i and u<.23:
        im=Image.blend(scene(i-1,STARTS[i]-STARTS[i-1]),im,ease(u/.23))
    return im


def music():
    """Original 12-bar, 96 BPM instrumental; no samples or song imitation."""
    sr=48000; n=sr*SECONDS; out=np.zeros((n,2),dtype=np.float64)
    rng=np.random.default_rng(614)
    def add(start,v,pan=0):
        k=round(start*sr); count=min(len(v),n-k)
        if count>0:
            out[k:k+count,0]+=v[:count]*math.sqrt((1-pan)/2)
            out[k:k+count,1]+=v[:count]*math.sqrt((1+pan)/2)
    def tone(midi,duration,kind,amp):
        t=np.arange(round(duration*sr))/sr
        f=440*2**((midi-69)/12)
        wave=np.sin(2*np.pi*f*t)+.22*np.sin(4*np.pi*f*t)
        if kind=='pad': env=np.minimum(t/.24,1)*np.minimum((duration-t)/.5,1)*.5
        elif kind=='bass': env=np.minimum(t/.008,1)*np.exp(-t*5)
        else: env=np.minimum(t/.004,1)*np.exp(-t*7)
        return wave*env*amp
    beat=60/96
    chords=[(50,57,61,66),(45,52,59,64),(47,54,57,62),(43,50,57,59)]
    pattern=[0,2,1,3,2,1,3,1]
    for bar in range(12):
        start=bar*4*beat; notes=chords[bar%4]
        for j,m in enumerate(notes): add(start,tone(m+12,2.49,'pad',.055),-.65+j*.43)
        for step in range(8):
            add(start+step*.5*beat,tone(notes[pattern[step]]+24,.65,'pluck',.072),(-1)**step*.46)
        for b in [0,2]:
            add(start+b*beat,tone(notes[0]-12,.55,'bass',.17))
        if bar<11:
            for b in range(4):
                t=np.arange(round(.22*sr))/sr
                kick=np.sin(2*np.pi*(44*t+62*.025*(1-np.exp(-t/.025))))*np.exp(-t*22)*.23
                add(start+b*beat,kick)
                if b%2:
                    t=np.arange(round(.1*sr))/sr
                    noise=rng.normal(0,1,len(t)); noise=np.diff(noise,prepend=0)
                    add(start+b*beat,noise*np.exp(-t*50)*.026)
            for b in range(8):
                t=np.arange(round(.045*sr))/sr
                noise=rng.normal(0,1,len(t)); noise=np.diff(noise,prepend=0)
                add(start+b*.5*beat,noise*np.exp(-t*110)*.011,(-1)**b*.5)
    # Subtle original transition sounds and a two-note success chime.
    for start in STARTS[1:-1]:
        t=np.arange(round(.3*sr))/sr
        noise=rng.normal(0,1,len(t))
        smooth=np.convolve(noise,np.ones(28)/28,mode='same')
        add(start-.14,smooth*np.sin(np.pi*t/.3)**2*.1,.2)
    add(14.65,tone(86,.6,'pluck',.07),-.15)
    add(14.82,tone(90,.8,'pluck',.055),.15)
    times=np.arange(n)/sr
    envelope=np.minimum(times/.12,1)*np.clip((29.9-times)/1.25,0,1)
    out*=envelope[:,None]
    out=np.tanh(out*1.7)*.65
    assert np.isfinite(out).all() and out.shape==(1440000,2)
    with wave.open(str(ROOT/'music.wav'),'wb') as w:
        w.setparams((2,2,sr,0,'NONE','not compressed'))
        w.writeframes((out*32767).astype('<i2').tobytes())


def render():
    music()
    cmd=['ffmpeg','-y','-v','warning','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}',
         '-r',str(FPS),'-i','-','-an','-c:v','libx264','-preset','fast','-crf','19',
         '-pix_fmt','yuv420p',str(ROOT/'motion.mp4')]
    p=subprocess.Popen(cmd,stdin=subprocess.PIPE)
    try:
        for k in range(FPS*SECONDS):
            p.stdin.write(frame(k/FPS).tobytes())
            if k%150==0: print(f'Rendered {k}/{FPS*SECONDS}',flush=True)
    finally: p.stdin.close()
    assert p.wait()==0,'Video encoder failed'
    subprocess.run(['ffmpeg','-y','-v','warning','-i',str(ROOT/'voice-raw.wav'),
        '-af','adelay=300:all=1,apad,atrim=duration=30,loudnorm=I=-17:TP=-2:LRA=7',
        '-ar','48000','-ac','2',str(ROOT/'voice.wav')],check=True)
    for stem in ['voice','music']:
        subprocess.run(['ffmpeg','-y','-v','warning','-i',str(ROOT/f'{stem}.wav'),
            '-c:a','libmp3lame','-b:a','192k',str(ROOT/f'{stem}.mp3')],check=True)
    subprocess.run(['ffmpeg','-y','-v','warning','-i',str(ROOT/'motion.mp4'),
        '-i',str(ROOT/'voice.wav'),'-i',str(ROOT/'music.wav'),'-filter_complex',
        '[2:a]volume=0.19[m];[1:a][m]amix=inputs=2:normalize=0:duration=longest,'
        'loudnorm=I=-16:TP=-1.5:LRA=7,afade=t=out:st=29.6:d=0.4[a]',
        '-map','0:v','-map','[a]','-c:v','copy','-c:a','aac','-b:a','192k','-ar','48000',
        '-t','30','-movflags','+faststart',str(ROOT/'BilisPDF-Commercial-30s.mp4')],check=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--preview',action='store_true')
    args=parser.parse_args()
    assert W==1920 and H==1080 and FPS*SECONDS==900
    if args.preview:
        sheets=Image.new('RGB',(960,810),'white')
        for i,t in enumerate([1.8,5,9.8,14.5,18,26]):
            thumb=frame(t).resize((480,270),Image.Resampling.LANCZOS)
            sheets.paste(thumb,((i%2)*480,(i//2)*270))
        sheets.save(ROOT/'storyboard.jpg',quality=86)
    else: render()
