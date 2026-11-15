import os, sys
from PIL import Image, ImageDraw
out=sys.argv[1]; names=sys.argv[2:]
ims=[Image.open(os.path.join(os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")), "Shots", "%s.png" % n)).convert("RGB") for n in names]
w=800; h=int(ims[0].height*w/ims[0].width)
cols=2; rows=(len(ims)+1)//2
sheet=Image.new("RGB",(w*cols,h*rows))
for i,(im,n) in enumerate(zip(ims,names)):
    im=im.resize((w,h)); d=ImageDraw.Draw(im); d.rectangle((0,0,180,18),fill=(0,0,0)); d.text((4,3),n,fill=(255,255,0))
    sheet.paste(im,((i%cols)*w,(i//cols)*h))
sheet.save(out,quality=88)
