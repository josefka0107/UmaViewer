# usage: python vidcompare.py <out prefix> <frames csv> <label=video@offset> [<label=video@offset> ...]
# Side-by-side sheets (3 timeline frames per image, one column per video) of the same timeline frames, each video
# shifted by its offset in seconds (vidalign.py prints it). Timeline frames run at 60 per second.
# OpenCV on Windows cannot open paths with non-ASCII characters: copy such videos to a plain name first.
import sys
import cv2
import numpy as np

out, frames = sys.argv[1], [int(f) for f in sys.argv[2].split(",")]
cols = []
for a in sys.argv[3:]:
    label, rest = a.split("=", 1)
    path, off = rest.rsplit("@", 1)
    cols.append((label, cv2.VideoCapture(path), float(off)))


def grab(label, cap, off, f):
    cap.set(cv2.CAP_PROP_POS_FRAMES, round((f / 60 + off) * cap.get(cv2.CAP_PROP_FPS)))
    ok, img = cap.read()
    img = cv2.resize(img, (640, 360)) if ok else np.zeros((360, 640, 3), np.uint8)
    cv2.putText(img, f"{label} f{f}", (8, 350), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 255), 2)
    return img


for i in range(0, len(frames), 3):
    rows = [np.hstack([grab(l, c, o, f) for l, c, o in cols]) for f in frames[i:i + 3]]
    cv2.imwrite(f"{out}_{i // 3}.jpg", np.vstack(rows), [cv2.IMWRITE_JPEG_QUALITY, 80])
