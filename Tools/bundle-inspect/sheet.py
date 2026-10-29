# usage: python sheet.py <frames dir> <out.jpg> <frame>...  -> 3x2 grid of the given frames (f<frame>.jpg)
import os
import sys
import cv2
import numpy as np

src, out = sys.argv[1], sys.argv[2]
tiles = []
for f in sys.argv[3:9]:
    img = cv2.imread(os.path.join(src, f"f{int(f):05d}.jpg"))
    tiles.append(cv2.resize(img, (640, 360)) if img is not None else np.zeros((360, 640, 3), np.uint8))
while len(tiles) < 6:
    tiles.append(np.zeros((360, 640, 3), np.uint8))
grid = np.vstack([np.hstack(tiles[0:3]), np.hstack(tiles[3:6])])
cv2.imwrite(out, grid, [cv2.IMWRITE_JPEG_QUALITY, 85])
