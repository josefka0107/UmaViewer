using Gallop.Live.Cutt;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Particle effects of a live (ramen steam, fire bursts, smoke...). The worksheet's effectList names each effect
    /// prefab (3d/effect/live[/son&lt;id&gt;]/pfb_&lt;name&gt;) and its keys say when it plays or stops and where it sits.
    /// Meanings were read from 1181's data, not from the game code: attribute 0x80000 plays, 0x100000 stops and
    /// clears, both restart (fire bursts on the beat); owner 18 is the world, other owners are character positions.
    /// </summary>
    public class LiveEffectsController
    {
        const int PlayBit = 0x80000;
        const int StopBit = 0x100000;
        const int WorldOwner = 18;

        class EffectInstance
        {
            public string Name;
            public GameObject Root;
            public ParticleSystem[] Particles;
            public List<LiveTimelineKeyEffectData> Keys;
            public int KeyIndex = -1;
            public bool Logged;
        }

        readonly List<EffectInstance> _effects = new List<EffectInstance>();
        readonly IList<UmaContainerCharacter> _charas;
        readonly LivePropsController _props;
        readonly Transform _worldRoot;
        readonly Dictionary<(Transform, string), Transform> _boneCache = new Dictionary<(Transform, string), Transform>();
        float _lastFrame = -1;

        public LiveEffectsController(LiveTimelineData data, IList<UmaContainerCharacter> charas, LivePropsController props, Transform worldRoot, int variationId = 0)
        {
            _charas = charas;
            _props = props;
            _worldRoot = worldRoot;

            var prefabs = new Dictionary<string, GameObject>();
            int missing = 0;
            foreach (var sheet in data.worksheetList)
            {
                if (sheet?.effectList == null) continue;
                foreach (var entry in sheet.effectList)
                {
                    if (entry?.keys == null || entry.keys.thisList.Count == 0 || string.IsNullOrEmpty(entry.name)) continue;
                    if (entry._applyVariation && entry._variationId != variationId) continue;

                    if (!prefabs.TryGetValue(entry.name, out var prefab))
                    {
                        prefab = LoadPrefab(entry.name);
                        prefabs[entry.name] = prefab;
                        if (!prefab)
                        {
                            missing++;
                            Debug.LogWarning($"[LiveEffects] Effect prefab not found: {entry.name}");
                        }
                    }
                    if (!prefab) continue;

                    var root = Object.Instantiate(prefab, worldRoot);
                    root.name = entry.name;
                    var effect = new EffectInstance
                    {
                        Name = entry.name,
                        Root = root,
                        Particles = root.GetComponentsInChildren<ParticleSystem>(true),
                        Keys = entry.keys.thisList.OrderBy(k => k.frame).ToList(),
                    };
                    Stop(effect);
                    _effects.Add(effect);
                }
            }
            Debug.Log($"[LiveEffects] {_effects.Count} effects ({prefabs.Count} prefabs, {missing} missing, variation {variationId})");
        }

        static GameObject LoadPrefab(string name)
        {
            string fileName = "/pfb_" + name;
            var entry = UmaViewerMain.Instance.AbList.Values.FirstOrDefault(e =>
                e != null && e.Name.StartsWith("3d/effect/live/") && e.Name.EndsWith(fileName));
            return entry?.Get<GameObject>();
        }

        public void AlterUpdate(float frame)
        {
            // Seeking back (or a big jump) replays from the current key instead of keeping old particles
            bool seek = frame < _lastFrame || frame - _lastFrame > 30;
            _lastFrame = frame;

            foreach (var effect in _effects)
            {
                if (!effect.Root) continue;

                int index = -1;
                for (int i = 0; i < effect.Keys.Count && effect.Keys[i].frame <= frame; i++) index = i;
                if (seek && index != effect.KeyIndex)
                {
                    Stop(effect);
                    effect.KeyIndex = -1;
                }
                if (index == effect.KeyIndex) continue;
                effect.KeyIndex = index;
                if (index < 0)
                {
                    Stop(effect);
                    continue;
                }

                var key = effect.Keys[index];
                int attribute = (int)key.attribute;
                Place(effect, key);

                if ((attribute & StopBit) != 0)
                    Stop(effect);
                if ((attribute & PlayBit) != 0)
                {
                    Play(effect, restart: (attribute & StopBit) != 0);
                    if (!effect.Logged)
                    {
                        effect.Logged = true;
                        var t = effect.Root.transform;
                        Debug.Log($"[LiveEffects] {effect.Name} plays at frame {(int)frame} on {t.parent?.name} pos={t.position} ({effect.Particles.Length} particle systems)");
                    }
                }
            }
        }

        void Place(EffectInstance effect, LiveTimelineKeyEffectData key)
        {
            var t = effect.Root.transform;
            var parent = ResolveParent(key) ?? _worldRoot;
            if (t.parent != parent) t.SetParent(parent, false);
            t.localPosition = key.offset;
            t.localRotation = Quaternion.Euler(key.offsetAngle);
            t.localScale = key.offsetScale == Vector3.zero ? Vector3.one : key.offsetScale;
        }

        /// <summary> Where a key places its effect: the world, a body point of a character, or one of its props. </summary>
        Transform ResolveParent(LiveTimelineKeyEffectData key)
        {
            if (key.owner == WorldOwner || key.owner < 0 || key.owner >= _charas.Count || !_charas[key.owner])
                return _worldRoot;

            var chara = _charas[key.owner].transform;
            if (key.IsAttachProps && _props != null)
            {
                var prop = _props.GetPropRoot(key.owner, key.PropsIndex);
                if (prop) return prop;
            }
            switch (key.occurrenceSpot)
            {
                case 1:
                case 6: return FindBone(chara, "Head") ?? chara;
                case 0: return FindBone(chara, "Position") ?? chara;
                default: return FindBone(chara, "Chest") ?? chara;
            }
        }

        Transform FindBone(Transform chara, string name)
        {
            if (_boneCache.TryGetValue((chara, name), out var bone) && bone) return bone;
            bone = chara.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == name);
            _boneCache[(chara, name)] = bone;
            return bone;
        }

        static void Play(EffectInstance effect, bool restart)
        {
            foreach (var ps in effect.Particles)
            {
                if (!ps) continue;
                if (restart) ps.Clear(false);
                if (restart || !ps.isPlaying) ps.Play(false);
            }
        }

        static void Stop(EffectInstance effect)
        {
            foreach (var ps in effect.Particles)
                if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
