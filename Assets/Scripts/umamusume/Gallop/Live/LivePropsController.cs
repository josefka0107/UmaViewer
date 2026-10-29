using Gallop.Live.Cutt;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Character props of a live (bowls, cooking tools...). The timeline data lists them in propsSettings with the
    /// character positions that use them; the worksheet's propsList says when each one is shown (and which clip
    /// animates it) and propsAttachList which joint holds it.
    /// Some lists come in variations (_applyVariation/_variationId); how the game picks one is unknown, so a
    /// fixed variation is used.
    /// </summary>
    public class LivePropsController
    {
        class PropInstance
        {
            public GameObject Root;
            /// <summary> Index among the props of this position, which is what the timeline keys refer to. </summary>
            public int PropIndex;
            public int Position;
            public Transform Chara;
            public LiveTimelineKeyPropsAttachData AttachKey;
        }

        readonly List<PropInstance> _props = new List<PropInstance>();
        readonly List<LiveTimelinePropsData> _propsLists = new List<LiveTimelinePropsData>();
        readonly List<LiveTimelinePropsAttachData> _attachLists = new List<LiveTimelinePropsAttachData>();
        readonly Dictionary<(Transform, string), Transform> _jointCache = new Dictionary<(Transform, string), Transform>();

        public int Count => _props.Count;

        public LivePropsController(LiveTimelineData data, IList<UmaContainerCharacter> charas, int variationId = 0)
        {
            var groups = data.propsSettings?.propsDataGroup;
            if (groups == null) return;

            // Timeline keys number the props of each position separately, in propsDataGroup order
            // (1181: position 6 has [1033_00, 1183_06, 7018_00, ...] and key propsID 1 is 1183_06).
            var prefabs = new Dictionary<int, GameObject>();
            for (int position = 0; position < charas.Count; position++)
            {
                var chara = charas[position];
                if (!chara) continue;

                int localIndex = 0;
                for (int i = 0; i < groups.Length; i++)
                {
                    var group = groups[i];
                    if (group == null || !MatchesConditions(group, position, chara)) continue;
                    int propIndex = localIndex++;
                    if (!group.isCharaProps) continue;

                    if (!prefabs.TryGetValue(i, out var prefab))
                    {
                        string name = $"prop{group.charaPropsMajorId}_{group.charaPropsMinorId:00}";
                        if (UmaViewerMain.Instance.AbList.TryGetValue($"3d/chara/prop/{name}/pfb_chr_{name}", out var entry))
                            prefab = entry.Get<GameObject>();
                        else
                            Debug.LogWarning($"[LiveProps] {name} not found");
                        prefabs[i] = prefab;
                    }
                    if (!prefab) continue;

                    var root = Object.Instantiate(prefab, chara.transform);
                    root.name = prefab.name;
                    root.SetActive(false);
                    var prop = new PropInstance { Root = root, PropIndex = propIndex, Position = position, Chara = chara.transform };
                    Attach(prop, group.attachJointNames?.FirstOrDefault(), Vector3.zero, Vector3.zero, Vector3.one);
                    _props.Add(prop);
                }
            }

            foreach (var sheet in data.worksheetList)
            {
                if (sheet == null) continue;
                if (sheet.propsList != null)
                    _propsLists.AddRange(sheet.propsList.Where(l => l?.keys != null && (!l._applyVariation || l._variationId == variationId)));
                if (sheet.propsAttachList != null)
                    _attachLists.AddRange(sheet.propsAttachList.Where(l => l?.keys != null && (!l._applyVariation || l._variationId == variationId)));
            }
            Debug.Log($"[LiveProps] {_props.Count} props, {_propsLists.Count} visibility lists, {_attachLists.Count} attach lists (variation {variationId})");
        }

        /// <summary> Condition groups: any group may match; inside a group all or any condition must match. </summary>
        static bool MatchesConditions(LiveTimelinePropsSettings.PropsDataGroup group, int position, UmaContainerCharacter chara)
        {
            var conditionGroups = group.propsConditionGroup?.Where(g => g != null && !g.IsInvalid && g.propsConditionData?.Length > 0).ToList();
            if (conditionGroups == null || conditionGroups.Count == 0) return true;

            return conditionGroups.Any(g =>
            {
                bool Matches(LiveTimelinePropsSettings.PropsConditionData c)
                {
                    switch (c.Type)
                    {
                        case LiveTimelinePropsSettings.PropsConditionType.CharaPosition: return c.Value == position;
                        case LiveTimelinePropsSettings.PropsConditionType.CharaId: return chara.CharaEntry != null && c.Value == chara.CharaEntry.Id;
                        default: return true; // Default, and DressId: the viewer does not keep the dress id
                    }
                }
                return g.satisfiesAllConditions ? g.propsConditionData.All(Matches) : g.propsConditionData.Any(Matches);
            });
        }

        static T CurrentKey<T>(List<T> keys, float frame) where T : LiveTimelineKey
        {
            T current = null;
            foreach (var key in keys)
            {
                if (key.frame > frame) break;
                current = key;
            }
            return current;
        }

        static bool HasPosition(int flags, int position) => position < 32 && (flags & (1 << position)) != 0;

        public void AlterUpdate(float frame)
        {
            foreach (var prop in _props)
            {
                if (!prop.Root) continue;

                // The latest key that targets this prop wins; propsID -1 targets every prop of the positions
                LiveTimelineKeyPropsData visibility = null;
                foreach (var list in _propsLists)
                {
                    var key = CurrentKey(list.keys.thisList, frame);
                    if (key == null || !HasPosition(key.settingFlags, prop.Position)) continue;
                    if (key.propsID != prop.PropIndex && !(key.propsID < 0 && !key.rendererEnable)) continue;
                    if (visibility == null || key.frame >= visibility.frame) visibility = key;
                }

                LiveTimelineKeyPropsAttachData attach = null;
                foreach (var list in _attachLists)
                {
                    var key = CurrentKey(list.keys.thisList, frame);
                    if (key == null || key._propsId != prop.PropIndex || !HasPosition(key._settingFlags, prop.Position)) continue;
                    if (attach == null || key.frame >= attach.frame) attach = key;
                }

                if (attach != null && attach != prop.AttachKey)
                {
                    Attach(prop, attach._attachJointName, attach._offsetPosition, attach.OffsetRotate, attach.OffsetScale);
                    prop.AttachKey = attach;
                }

                bool visible = visibility != null && visibility.propsID == prop.PropIndex && visibility.rendererEnable;
                if (prop.Root.activeSelf != visible) prop.Root.SetActive(visible);

                if (visible && visibility.IsApplyAnimation && visibility.AnimationClip)
                {
                    var clip = visibility.AnimationClip;
                    float time = visibility.StartAnimationTime + (frame - visibility.frame) / 60f;
                    time = clip.isLooping ? Mathf.Repeat(time, clip.length) : Mathf.Clamp(time, 0, clip.length);
                    clip.SampleAnimation(prop.Root, time);
                }
            }
        }

        void Attach(PropInstance prop, string jointName, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            if (string.IsNullOrEmpty(jointName)) return;

            var joint = FindJoint(prop.Chara, jointName);
            if (!joint)
            {
                Debug.LogWarning($"[LiveProps] Joint {jointName} not found for {prop.Root.name} at position {prop.Position}");
                return;
            }
            prop.Root.transform.SetParent(joint, false);
            prop.Root.transform.localPosition = position;
            prop.Root.transform.localRotation = Quaternion.Euler(rotation);
            prop.Root.transform.localScale = scale;
        }

        /// <summary> A joint of the character, or of the stage (Mic_Attach_00 is used with stage-sized offsets). </summary>
        Transform FindJoint(Transform chara, string jointName)
        {
            if (_jointCache.TryGetValue((chara, jointName), out var joint) && joint) return joint;

            joint = chara.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == jointName)
                ?? GameObject.Find(jointName)?.transform;
            _jointCache[(chara, jointName)] = joint;
            return joint;
        }
    }
}
