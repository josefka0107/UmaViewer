# usage: python vidalign.py <video> <cutt camera bundle>
# Finds the offset between a live video and its timeline by matching the video's hard cuts with the timeline's
# camera cuts (a cut follows each camera key without interpolation). Prints offset and match quality.
import json
import sys
import cv2
import numpy as np
import UnityPy

video, bundle = sys.argv[1], sys.argv[2]
w = next(o for o in UnityPy.load(bundle).objects if o.type.name == "MonoBehaviour").read_typetree()
keys = w["cameraPosKeys"]["thisList"]
cuts = [k["frame"] / 60 for i, k in enumerate(keys) if i == 0 or keys[i - 1]["interpolateType"] == 0][1:]

cap = cv2.VideoCapture(video)
fps = cap.get(cv2.CAP_PROP_FPS)
prev, diffs = None, []
while True:
    ok, frame = cap.read()
    if not ok:
        break
    small = cv2.resize(frame, (160, 90))
    hist = cv2.calcHist([cv2.cvtColor(small, cv2.COLOR_BGR2HSV)], [0, 1], None, [16, 8], [0, 180, 0, 256])
    hist = cv2.normalize(hist, hist).flatten()
    diffs.append(0 if prev is None else cv2.compareHist(prev, hist, cv2.HISTCMP_BHATTACHARYYA))
    prev = hist
diffs = np.array(diffs)
thr = max(0.3, np.percentile(diffs, 97))
vcuts = [i / fps for i in range(1, len(diffs) - 1) if diffs[i] > thr and diffs[i] >= diffs[i - 1] and diffs[i] >= diffs[i + 1]]

best = None
for off in np.arange(-3, 8, 1 / fps / 2):
    m = sum(1 for c in cuts if min(abs(c + off - v) for v in vcuts) <= 1.5 / fps)
    if best is None or m > best[1]:
        best = (off, m)
off, matched = best
print(json.dumps({"fps": fps, "video_cuts": len(vcuts), "timeline_cuts": len(cuts), "offset_s": round(float(off), 3),
                  "matched": matched, "threshold": round(float(thr), 3)}))
