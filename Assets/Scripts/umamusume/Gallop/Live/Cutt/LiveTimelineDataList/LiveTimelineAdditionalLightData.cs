using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field names follow the game's serialized data (type tree of cutt/cutt_son*/son*_camera, AdditionalLightList).

    [Serializable]
    public class LiveTimelineKeyAdditionalLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyAdditionalLightData>
    {

    }

    /// <summary>
    /// A realtime light the character shaders pick up (they are built with _ADDITIONAL_LIGHTS). Type 1 is a spot
    /// light (1181 and 1092 aim them down with 79-173 degree cones); keys with IsEnable off and Type 0 switch it off.
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyAdditionalLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.AdditionalLight; }
        }
        public Vector3 Position;
        public Vector3 Rotate;
        public bool IsEnable;
        public int Type;
        public float Range;
        public int SpotAngle;
        public float IndirectMultiplier;
        public int ShadowType;
        public float Strength;
    }

    [Serializable]
    public class LiveTimelineAdditionalLightData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyAdditionalLightDataList keys;
    }
}
