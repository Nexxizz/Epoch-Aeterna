"""Assemble rendered frames with Pillow; run with ordinary Python, not Blender."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/previews/settler'
font_path='C:/Windows/Fonts/segoeui.ttf'
font=ImageFont.truetype(font_path,20)
small=ImageFont.truetype(font_path,16)
manifest=json.loads((ROOT/'.godot/settler-reel/manifest.json').read_text())
frames=[];durations=[];death=[]
for i,item in enumerate(manifest):
    frame=Image.new('RGB',(480,532),(25,30,35))
    frame.paste(Image.open(item['path']).convert('RGB'),(0,0))
    draw=ImageDraw.Draw(frame)
    draw.text((18,489),item['label'],font=font,fill=(235,226,208))
    draw.text((355,493),f"{item['frame']/30:.1f} s",font=small,fill=(166,179,188))
    frame=frame.quantize(colors=128,method=Image.Quantize.MEDIANCUT)
    frames.append(frame)
    durations.append(100 if i+1<len(manifest) and manifest[i+1]['clip']==item['clip'] else 350)
    if item['clip']=='Death': death.append(frame)
durations[-1]=1600
frames[0].save(OUT/'settler_animations.gif',save_all=True,append_images=frames[1:],
               duration=durations,loop=0,optimize=False,disposal=2)
death[0].save(OUT/'settler_death.gif',save_all=True,append_images=death[1:],
             duration=[100]*(len(death)-1)+[1800],loop=0,optimize=False,disposal=2)
poses=[('Idle',0,'Ruhe'),('Walk',8,'Gehen'),('Run',6,'Laufen'),('Carry_Walk',9,'Tragen'),
       ('Gather_Food',24,'Sammeln'),('Gather_Chop',16,'Ausholen'),('Gather_Chop',22,'Holz hacken'),
       ('Gather_Mine',25,'Abbauen'),('Attack',24,'Angreifen'),('Build',22,'Bauen'),
       ('Death',18,'Einknicken'),('Death',72,'Sterben / Endpose')]
sheet=Image.new('RGB',(1000,862),(25,30,35));draw=ImageDraw.Draw(sheet)
draw.text((18,15),'EPOCH AETERNA  /  Steinzeitsiedler',font=font,fill=(235,226,208))
for i,(clip,n,label) in enumerate(poses):
    x=i%4*250;y=54+i//4*268
    picture=Image.open(OUT/f'{clip}_{n:03}.png').convert('RGB').resize((250,250),Image.Resampling.LANCZOS)
    sheet.paste(picture,(x,y))
    draw.text((x+10,y+246),label,font=small,fill=(235,226,208))
sheet.save(OUT/'settler_overview.jpg',quality=92)
Image.open(OUT/'Idle_000.png').save(ROOT/'docs/previews/settler_final.png')
print(f'Wrote {len(frames)} animation frames and contact sheet to {OUT}')
