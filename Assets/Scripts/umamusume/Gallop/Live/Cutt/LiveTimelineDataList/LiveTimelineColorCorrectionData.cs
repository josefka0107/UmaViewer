using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field names follow the game's serialized data (type tree of cutt/cutt_son*/son*_camera, colorCorrectionDataLists).

    [Serializable]
    public class LiveTimelineKeyColorCorrectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyColorCorrectionData>
    {

    }

    /// <summary>
    /// Per-channel curves on the final image, like Unity's legacy ColorCorrectionCurves (mode 0 = simple; the depth
    /// curves and the selective key/target colors belong to its advanced mode). Keys hold until the next one.
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyColorCorrectionData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.ColorCorrection; }
        }
        public int enable;
        public float saturation;
        public int mode;
        public AnimationCurve redCurve;
        public AnimationCurve greenCurve;
        public AnimationCurve blueCurve;
        public AnimationCurve depthRedCurve;
        public AnimationCurve depthGreenCurve;
        public AnimationCurve depthBlueCurve;
        public AnimationCurve blendCurve;
        public int selective;
        public Color keyColor;
        public Color targetColor;
    }

    [Serializable]
    public class LiveTimelineColorCorrectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyColorCorrectionDataList keys;
    }
}
