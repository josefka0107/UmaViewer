# usage: python vidframes.py <video> <offset seconds> <out dir> <timeline frame>...
# Saves the video frame matching each timeline frame (60 per second) as <out>/f<frame>.jpg, labeled.
import os
import sys
import cv2

video, offset, out = sys.argv[1], float(sys.argv[2]), sys.argv[3]
frames = sorted({int(f) for f in sys.argv[4:]})
os.makedirs(out, exist_ok=True)
cap = cv2.VideoCapture(video)
fps = cap.get(cv2.CAP_PROP_FPS)
for f in frames:
    cap.set(cv2.CAP_PROP_POS_FRAMES, round((f / 60 + offset) * fps))
    ok, img = cap.read()
    if not ok:
        print("missing", f)
        continue
    cv2.putText(img, f"GAME frame {f}  {int(f / 60) // 60}:{(f / 60) % 60:05.2f}", (20, 50), cv2.FONT_HERSHEY_SIMPLEX, 1.4, (0, 0, 0), 6)
    cv2.putText(img, f"GAME frame {f}  {int(f / 60) // 60}:{(f / 60) % 60:05.2f}", (20, 50), cv2.FONT_HERSHEY_SIMPLEX, 1.4, (255, 255, 255), 2)
    cv2.imwrite(os.path.join(out, f"f{f:05d}.jpg"), cv2.resize(img, (960, 540)), [cv2.IMWRITE_JPEG_QUALITY, 85])
print(len(frames), "frames ->", out)
