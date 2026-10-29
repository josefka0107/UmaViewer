using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field names follow the game's serialized data (type tree of cutt/cutt_son*/son*_camera);
    // fields the viewer does not use are left out and skipped by Unity.

    [Serializable]
    public class LiveTimelineKeyPropsDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsData>
    {

    }

    /// <summary> Shows or hides a prop (index in propsSettings.propsDataGroup) for the character positions in settingFlags. </summary>
    [Serializable]
    public class LiveTimelineKeyPropsData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.Props; }
        }
        /// <summary> Bit mask of character positions (bit 0 = position 0). </summary>
        public int settingFlags;
        /// <summary> Index in propsDataGroup, -1 for every prop of the positions. </summary>
        public int propsID;
        public bool rendererEnable;
        public bool IsApplyAnimation;
        public AnimationClip AnimationClip;
        public float StartAnimationTime;
        public int AnimationHeadFrame;
    }

    [Serializable]
    public class LiveTimelinePropsData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsDataList keys;
        public bool _applyVariation;
        public int _variationId;
    }

    [Serializable]
    public class LiveTimelineKeyPropsAttachDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsAttachData>
    {

    }

    /// <summary> Attaches a prop to a joint of the characters at the positions in _settingFlags. </summary>
    [Serializable]
    public class LiveTimelineKeyPropsAttachData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.PropsAttach; }
        }
        public string _attachJointName;
        /// <summary> Bit mask of character positions (bit 0 = position 0). </summary>
        public int _settingFlags;
        /// <summary> Index in propsDataGroup, -1 for none. </summary>
        public int _propsId;
        public Vector3 _offsetPosition;
        public Vector3 OffsetRotate;
        public Vector3 OffsetScale;
    }

    [Serializable]
    public class LiveTimelinePropsAttachData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsAttachDataList keys;
        public bool _applyVariation;
        public int _variationId;
    }
}
