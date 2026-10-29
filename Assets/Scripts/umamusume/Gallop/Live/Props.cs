using System;
using System.Linq;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// The game's component on common live props (stand mics, taiko: 3d/env/live/common/prop). Field names match the
    /// serialized data, so Unity fills them when the prefab loads. Without it the stand mic stays folded with the mic
    /// at floor level: its "adjust" pole has to be raised to the character's head.
    /// </summary>
    public class Props : MonoBehaviour
    {
        [Serializable]
        public class AdjustmentData
        {
            public Transform Transform;
            /// <summary> Allowed local offset of Transform (0 to 0.715 m up for the stand mics). </summary>
            public Vector3 OffsetRange;
            public Vector3 TransformRate;
            /// <summary> Character node to reach; 0 in every common prop, taken as the head. </summary>
            public int TargetNode;
            /// <summary> Where the part's node goes relative to the target (0.098 m below it). </summary>
            public Vector3 TargetOffset;
        }

        [SerializeField] private AdjustmentData[] _adjustmentDataArray;
        [SerializeField] private bool _isInfluenceOfCharaHeight;

        public bool IsInfluenceOfCharaHeight => _isInfluenceOfCharaHeight;

        /// <summary>
        /// Raises each adjustable part so its node (mic_node, taiko_node) sits TargetOffset from <paramref name="target"/>,
        /// within OffsetRange. The exact rule of the game is unknown; this places the mic just below the head.
        /// </summary>
        public void Adjust(Transform target)
        {
            if (_adjustmentDataArray == null || !target) return;

            foreach (var data in _adjustmentDataArray)
            {
                if (data == null || !data.Transform || !data.Transform.parent) continue;

                var node = data.Transform.Cast<Transform>().FirstOrDefault(c => c.name.EndsWith("_node"));
                float nodeHeight = node ? node.localPosition.y : 0;
                float targetHeight = data.Transform.parent.InverseTransformPoint(target.position).y;
                float rate = data.TransformRate.y == 0 ? 1 : data.TransformRate.y;
                float y = (targetHeight + data.TargetOffset.y - nodeHeight) * rate;

                var position = data.Transform.localPosition;
                position.y = Mathf.Clamp(y, Mathf.Min(0, data.OffsetRange.y), Mathf.Max(0, data.OffsetRange.y));
                data.Transform.localPosition = position;
            }
        }
    }
}
