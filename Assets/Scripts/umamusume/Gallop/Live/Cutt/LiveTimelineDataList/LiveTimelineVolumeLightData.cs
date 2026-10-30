using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field names follow the game's serialized data (type tree of cutt/cutt_son*/son*_camera, volumeLightKeys).

    [Serializable]
    public class LiveTimelineKeyVolumeLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVolumeLightData>
    {

    }

    /// <summary>
    /// Screen-space light shafts ("SunShafts"): rays blurred out from sunPosition (world space), tinted by color1 and
    /// scaled by power. 1054 puts the sun behind the stage (0, 0.5, -23.8) with power 30-50 and blurRadius 3-5.
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyVolumeLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.VolumeLight; }
        }
        public Vector3 sunPosition;
        public Color color1;
        public float power;
        public float komorebi;
        public float blurRadius;
        public float ColorRate;
        public float ScreenColorPower;
        public float EffectColorPower;
        public int enable;
        public int isEnabledBorderClear;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public int IsAdjustedBlinkLightColor;
    }

    [Serializable]
    public class LiveTimelineVolumeLightData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyVolumeLightDataList keys;
    }
}
