using Gallop.Live.Cutt;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.Live
{
    /// <summary>
    /// Timeline post effects the worksheet carries but the viewer dropped: colorCorrectionDataLists as URP color
    /// curves on a global volume, and volumeLightKeys ("SunShafts") as a screen-space pass enqueued on the live
    /// cameras. The live cameras ship with post-processing off, so it is switched on for them here.
    /// </summary>
    public class LivePostEffectController
    {
        readonly List<LiveTimelineKeyColorCorrectionData> _colorKeys;
        readonly List<LiveTimelineKeyVolumeLightData> _sunKeys;
        readonly Transform _root;
        readonly Volume _volume;
        readonly ColorCurves _curves;
        readonly ColorAdjustments _adjustments;
        readonly SunShaftsPass _sunPass;
        readonly HashSet<Camera> _cameras = new HashSet<Camera>();
        int _colorKeyIndex = -2;
        bool _sunLogged;

        public LivePostEffectController(LiveTimelineData data, Transform root)
        {
            _root = root;
            _colorKeys = data.worksheetList
                .Where(s => s?.colorCorrectionDataLists != null)
                .SelectMany(s => s.colorCorrectionDataLists)
                .Where(e => e?.keys != null)
                .SelectMany(e => e.keys.thisList)
                .OrderBy(k => k.frame)
                .ToList();
            _sunKeys = data.worksheetList
                .Where(s => s?.volumeLightKeys != null)
                .SelectMany(s => s.volumeLightKeys)
                .Where(e => e?.keys != null)
                .SelectMany(e => e.keys.thisList)
                .OrderBy(k => k.frame)
                .ToList();

            var go = new GameObject("LivePostEffectVolume");
            go.transform.SetParent(root, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50;
            _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _curves = _volume.profile.Add<ColorCurves>(true);
            _curves.active = false;
            _adjustments = _volume.profile.Add<ColorAdjustments>(true);
            _adjustments.active = false;

            var shader = Shader.Find("Hidden/UmaViewer/LiveSunShafts");
            if (_sunKeys.Count > 0 && shader != null)
            {
                _sunPass = new SunShaftsPass(new Material(shader) { hideFlags = HideFlags.HideAndDontSave }, data.sunShaftsSettings);
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }
            else if (_sunKeys.Count > 0)
                Debug.LogWarning("[LivePostEffect] shader Hidden/UmaViewer/LiveSunShafts not found: no sun shafts");

            Debug.Log($"[LivePostEffect] {_colorKeys.Count} color correction keys, {_sunKeys.Count} sun shaft keys");
        }

        public void AlterUpdate(float frame, IEnumerable<Camera> cameras)
        {
            if (_volume == null)
            {
                Release();
                return;
            }

            foreach (var camera in cameras)
            {
                if (camera == null || _cameras.Contains(camera)) continue;
                _cameras.Add(camera);
                var cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData != null) cameraData.renderPostProcessing = true;
            }

            UpdateColorCorrection(frame);
            UpdateSunShafts(frame);
        }

        void UpdateColorCorrection(float frame)
        {
            int index = -1;
            for (int i = 0; i < _colorKeys.Count && _colorKeys[i].frame <= frame; i++) index = i;
            if (index == _colorKeyIndex) return;
            _colorKeyIndex = index;

            var key = index >= 0 ? _colorKeys[index] : null;
            bool on = key != null && key.enable != 0;
            _curves.active = on;
            _adjustments.active = on;
            if (!on) return;

            SetCurve(_curves.red, key.redCurve);
            SetCurve(_curves.green, key.greenCurve);
            SetCurve(_curves.blue, key.blueCurve);
            // Legacy saturation is a factor (1 = unchanged); URP's is an offset in -100..100
            _adjustments.saturation.Override(Mathf.Clamp((key.saturation - 1f) * 100f, -100f, 100f));
        }

        static void SetCurve(TextureCurveParameter parameter, AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) curve = AnimationCurve.Linear(0, 0, 1, 1);
            parameter.value?.Release();
            parameter.Override(new TextureCurve(curve, 0f, false, new Vector2(0f, 1f)));
        }

        void UpdateSunShafts(float frame)
        {
            if (_sunPass == null) return;

            int index = -1;
            for (int i = 0; i < _sunKeys.Count && _sunKeys[i].frame <= frame; i++) index = i;
            var key = index >= 0 ? _sunKeys[index] : null;
            if (key == null || key.enable == 0)
            {
                _sunPass.Enabled = false;
                return;
            }

            var next = index + 1 < _sunKeys.Count ? _sunKeys[index + 1] : null;
            float t = 0;
            if (next != null && next.interpolateType != LiveCameraInterpolateType.None && next.frame > key.frame)
                t = Mathf.Clamp01((frame - key.frame) / (next.frame - key.frame));
            else
                next = key;

            _sunPass.Enabled = true;
            _sunPass.SunWorld = _root.TransformPoint(Vector3.Lerp(key.sunPosition, next.sunPosition, t));
            _sunPass.Color = Color.Lerp(key.color1, next.color1, t);
            _sunPass.Power = Mathf.Lerp(key.power, next.power, t);
            _sunPass.BlurRadius = Mathf.Lerp(key.blurRadius, next.blurRadius, t);
            _sunPass.ScreenColorPower = key.ScreenColorPower;
            _sunPass.EffectColorPower = key.EffectColorPower;

            if (!_sunLogged)
            {
                _sunLogged = true;
                Debug.Log($"[LivePostEffect] sun shafts on at frame {(int)frame}: sun={_sunPass.SunWorld} color={_sunPass.Color} power={_sunPass.Power} blur={_sunPass.BlurRadius}");
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_volume == null)
            {
                Release();
                return;
            }
            if (!_sunPass.Enabled || !_cameras.Contains(camera)) return;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData?.scriptableRenderer?.EnqueuePass(_sunPass);
        }

        public void Release()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        /// <summary>
        /// Deduced, not confirmed: the live's sunShaftsSettings (1054: intensity 0.5, sunCenterBrightness 1.5,
        /// blackLevel 0.1, screenBlendMode 1, blurIterations 2) scale every key, and the key's power multiplies the
        /// intensity (1054: 10 for the warm sun above Oguri at 2330-2482, 30-50 for the one behind the stage).
        /// </summary>
        class SunShaftsPass : ScriptableRenderPass
        {
            // Legacy SunShafts radius; PowerScale brings power x intensity near the legacy default (1.15)
            const float MaxRadius = 0.75f;
            const float PowerScale = 1f / 10f;
            const int Downsample = 4;

            static readonly int MaskId = Shader.PropertyToID("_LiveSunShaftsMask");
            static readonly int BlurId = Shader.PropertyToID("_LiveSunShaftsBlur");
            static readonly int CompositeId = Shader.PropertyToID("_LiveSunShaftsComposite");
            static readonly int ShaftsTexId = Shader.PropertyToID("_ShaftsTex");
            static readonly int SunPositionId = Shader.PropertyToID("_SunPosition");
            static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius4");
            static readonly int SunColorId = Shader.PropertyToID("_SunColor");
            static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
            static readonly int CenterBrightnessId = Shader.PropertyToID("_CenterBrightness");
            static readonly int BlendAddId = Shader.PropertyToID("_BlendAdd");
            static readonly int ScreenColorPowerId = Shader.PropertyToID("_ScreenColorPower");
            static readonly int EffectColorPowerId = Shader.PropertyToID("_EffectColorPower");

            readonly Material _material;
            readonly float _intensity = 1f;
            readonly float _centerBrightness = 1f;
            readonly float _blackLevel = 0.5f;
            readonly int _iterations = 2;
            readonly bool _blendAdd;
            public bool Enabled;
            public Vector3 SunWorld;
            public Color Color;
            public float Power;
            public float BlurRadius;
            public float ScreenColorPower = 1;
            public float EffectColorPower = 1;

            public SunShaftsPass(Material material, LiveTimelineSunShaftsSettings settings)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                if (settings == null) return;
                _intensity = settings.intensity;
                _centerBrightness = settings.sunCenterBrightness;
                _blackLevel = settings.blackLevel;
                _iterations = Mathf.Clamp(settings.blurIterations, 1, 4);
                _blendAdd = settings.screenBlendMode == 1;
                Debug.Log($"[LivePostEffect] sun shafts settings: intensity={_intensity} centerBrightness={_centerBrightness} blackLevel={_blackLevel} iterations={_iterations} blend={(_blendAdd ? "add" : "screen")}");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                Vector3 sun = camera.WorldToViewportPoint(SunWorld);
                if (sun.z <= 0) return;

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                var small = desc;
                small.width = Mathf.Max(1, desc.width / Downsample);
                small.height = Mathf.Max(1, desc.height / Downsample);

                var cmd = CommandBufferPool.Get("LiveSunShafts");
                cmd.GetTemporaryRT(MaskId, small, FilterMode.Bilinear);
                cmd.GetTemporaryRT(BlurId, small, FilterMode.Bilinear);
                cmd.GetTemporaryRT(CompositeId, desc, FilterMode.Bilinear);

                _material.SetVector(SunPositionId, new Vector4(sun.x, sun.y, MaxRadius, sun.z));
                _material.SetFloat(ThresholdId, _blackLevel);
                _material.SetFloat(CenterBrightnessId, _centerBrightness);
                _material.SetFloat(BlendAddId, _blendAdd ? 1f : 0f);
                _material.SetColor(SunColorId, Color * (Power * _intensity * PowerScale));
                _material.SetFloat(ScreenColorPowerId, ScreenColorPower);
                _material.SetFloat(EffectColorPowerId, EffectColorPower);

                cmd.Blit(source, MaskId, _material, 0);
                // Legacy blur schedule: each pass reaches further towards the sun
                int from = MaskId, to = BlurId;
                for (int i = 0; i < _iterations; i++)
                {
                    float ofs = BlurRadius * ((i * 2f + 1f) * 6f) / 768f;
                    cmd.SetGlobalVector(BlurRadiusId, new Vector4(ofs, ofs, 0, 0));
                    cmd.Blit(from, to, _material, 1);
                    (from, to) = (to, from);
                }

                cmd.SetGlobalTexture(ShaftsTexId, from);
                cmd.Blit(source, CompositeId, _material, 2);
                cmd.Blit(CompositeId, source);

                cmd.ReleaseTemporaryRT(MaskId);
                cmd.ReleaseTemporaryRT(BlurId);
                cmd.ReleaseTemporaryRT(CompositeId);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
