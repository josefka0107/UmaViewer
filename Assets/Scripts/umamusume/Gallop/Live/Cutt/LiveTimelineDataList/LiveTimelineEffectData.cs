using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field names follow the game's serialized data (type tree of cutt/cutt_son*/son*_camera, effectList);
    // fields the viewer does not use are left out and skipped by Unity.

    [Serializable]
    public class LiveTimelineKeyEffectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEffectData>
    {

    }

    /// <summary>
    /// One state change of a particle effect. Attribute bits: 0x80000 plays, 0x100000 stops and clears, both restart.
    /// owner 18 places the effect in the world at offset; other owners are character positions, with
    /// occurrenceSpot naming the body point (1 head, 6 face, 0 root, 9 a prop when IsAttachProps).
    /// </summary>
    [Serializable]
    public class LiveTimelineKeyEffectData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get { return LiveTimelineKeyDataType.Effect; }
        }
        public Color color;
        public float colorPower;
        public int owner;
        public int occurrenceSpot;
        public string ParentStageObjectName;
        public bool IsAttachProps;
        public int PropsIndex;
        public Vector3 offset;
        public Vector3 offsetAngle;
        public Vector3 offsetScale;
        public bool IsLinkOwnerPositionX;
        public bool IsLinkOwnerPositionY;
        public bool IsLinkOwnerPositionZ;
        public bool IsLinkOwnerRotate;
    }

    [Serializable]
    public class LiveTimelineEffectData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyEffectDataList keys;
        public bool _applyVariation;
        public int _variationId;
    }
}
