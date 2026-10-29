using Gallop.Live.Cutt;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// The worksheet's AdditionalLightList as Unity lights: the character toon shaders are compiled with URP's
    /// _ADDITIONAL_LIGHTS, so a plain spot or point light lights them as the game's does. Stage shaders ignore them.
    /// Keys interpolate towards the next one when that one is interpolated (1181 fades its spot out at the end).
    /// </summary>
    public class LiveAdditionalLightController
    {
        class TimelineLight
        {
            public Light Light;
            public List<LiveTimelineKeyAdditionalLightData> Keys;
            public bool Logged;
        }

        readonly List<TimelineLight> _lights = new List<TimelineLight>();

        public LiveAdditionalLightController(LiveTimelineData data, Transform root)
        {
            foreach (var sheet in data.worksheetList)
            {
                if (sheet?.AdditionalLightList == null) continue;
                foreach (var entry in sheet.AdditionalLightList)
                {
                    if (entry?.keys == null || entry.keys.thisList.Count == 0) continue;
                    var go = new GameObject($"LiveAdditionalLight_{_lights.Count}_{entry.name}");
                    go.transform.SetParent(root, false);
                    var light = go.AddComponent<Light>();
                    light.enabled = false;
                    light.shadows = LightShadows.None;
                    light.renderMode = LightRenderMode.ForcePixel;
                    _lights.Add(new TimelineLight { Light = light, Keys = entry.keys.thisList.OrderBy(k => k.frame).ToList() });
                }
            }
            Debug.Log($"[LiveLights] {_lights.Count} additional lights");
        }

        public void AlterUpdate(float frame)
        {
            foreach (var timelineLight in _lights)
            {
                var keys = timelineLight.Keys;
                int index = -1;
                for (int i = 0; i < keys.Count && keys[i].frame <= frame; i++) index = i;
                var light = timelineLight.Light;
                if (index < 0 || !keys[index].IsEnable || keys[index].Type == 0)
                {
                    light.enabled = false;
                    continue;
                }

                var key = keys[index];
                var next = index + 1 < keys.Count ? keys[index + 1] : null;
                float t = 0;
                if (next != null && next.interpolateType != LiveCameraInterpolateType.None && next.frame > key.frame)
                    t = Mathf.Clamp01((frame - key.frame) / (next.frame - key.frame));
                else
                    next = null;

                light.type = key.Type == 1 ? LightType.Spot : LightType.Point;
                light.transform.localPosition = next == null ? key.Position : Vector3.Lerp(key.Position, next.Position, t);
                light.transform.localRotation = next == null ? Quaternion.Euler(key.Rotate) : Quaternion.Slerp(Quaternion.Euler(key.Rotate), Quaternion.Euler(next.Rotate), t);
                light.range = next == null ? key.Range : Mathf.Lerp(key.Range, next.Range, t);
                light.spotAngle = Mathf.Clamp(next == null ? key.SpotAngle : Mathf.Lerp(key.SpotAngle, next.SpotAngle, t), 1, 179);
                light.intensity = next == null ? key.Strength : Mathf.Lerp(key.Strength, next.Strength, t);
                light.bounceIntensity = key.IndirectMultiplier;
                light.enabled = light.intensity > 0;

                if (light.enabled && !timelineLight.Logged)
                {
                    timelineLight.Logged = true;
                    Debug.Log($"[LiveLights] {light.name} on at frame {(int)frame}: {light.type} pos={light.transform.position} range={light.range:0.0} angle={light.spotAngle:0} intensity={light.intensity:0.00}");
                }
            }
        }
    }
}
