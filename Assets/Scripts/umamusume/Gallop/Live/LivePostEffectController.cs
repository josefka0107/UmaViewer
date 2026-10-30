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
        readonly FilmPass _filmPass;
        readonly BloomPass _bloomPass;
        readonly List<LiveTimelineKeyPostEffectBloomDiffusionData> _bloomKeys;
        bool _bloomLogged;
        readonly List<LiveTimelineKeyPostFilmData>[] _filmKeys;
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
                _sunPass = new SunShaftsPass(new Material(shader) { hideFlags = HideFlags.HideAndDontSave }, data.sunShaftsSettings);
            else if (_sunKeys.Count > 0)
                Debug.LogWarning("[LivePostEffect] shader Hidden/UmaViewer/LiveSunShafts not found: no sun shafts");

            // The three screen overlay layers, drawn with the game's own film shader
            var sheets = data.worksheetList.Where(s => s != null).ToList();
            _filmKeys = new[]
            {
                FilmKeys(sheets.Select(s => s.postFilmKeys)),
                FilmKeys(sheets.Select(s => s.postFilm2Keys)),
                FilmKeys(sheets.Select(s => s.postFilm3Keys)),
            };
            if (_filmKeys.Any(k => k.Count > 0))
            {
                var filmShader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.PostBlit_Rich);
                if (filmShader != null)
                    _filmPass = new FilmPass(new Material(filmShader) { hideFlags = HideFlags.HideAndDontSave });
                else
                    Debug.LogWarning("[LivePostEffect] game shader PostBlit_Rich not found: no post film");
            }

            // Bloom from the worksheet's postEffectBloomDiffusionKeys, drawn with the game's FastBloom
            _bloomKeys = sheets
                .Select(s => s.postEffectBloomDiffusionKeys)
                .Where(l => l != null && !l.HasAttribute(LiveTimelineKeyDataListAttr.Disable))
                .SelectMany(l => l.thisList)
                .OrderBy(k => k.frame)
                .ToList();
            if (_bloomKeys.Count > 0)
            {
                var bloomShader = Gallop.ShaderManager.GetShader(Gallop.ShaderManager.ShaderKinds.FastBloom);
                if (bloomShader != null)
                    _bloomPass = new BloomPass(new Material(bloomShader) { hideFlags = HideFlags.HideAndDontSave },
                        data.hdrBloomSettings != null ? data.hdrBloomSettings.bloomBlurIterations : 1);
                else
                    Debug.LogWarning("[LivePostEffect] game shader FastBloom not found: no bloom");
            }

            if (_sunPass != null || _filmPass != null || _bloomPass != null)
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

            Debug.Log($"[LivePostEffect] {_colorKeys.Count} color correction keys, {_sunKeys.Count} sun shaft keys, " +
                      $"post film keys {string.Join("/", _filmKeys.Select(k => k.Count))}, {_bloomKeys.Count} bloom keys");
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
            UpdateFilm(frame);
            UpdateBloom(frame);
        }

        void UpdateBloom(float frame)
        {
            if (_bloomPass == null) return;
            int index = -1;
            for (int i = 0; i < _bloomKeys.Count && _bloomKeys[i].frame <= frame; i++) index = i;
            var key = index >= 0 ? _bloomKeys[index] : null;
            if (key == null || !key.IsEnabledBloom || key.intensity <= 0)
            {
                _bloomPass.Enabled = false;
                return;
            }

            var next = index + 1 < _bloomKeys.Count ? _bloomKeys[index + 1] : null;
            float t = 0;
            if (next != null && next.interpolateType != LiveCameraInterpolateType.None && next.frame > key.frame && next.IsEnabledBloom)
                t = Mathf.Clamp01(LiveTimelineControl.CalculateInterpolationValue(key, next, frame));
            else
                next = key;

            _bloomPass.Enabled = true;
            _bloomPass.Threshold = Mathf.Lerp(key.threshold, next.threshold, t);
            _bloomPass.Intensity = Mathf.Lerp(key.intensity, next.intensity, t);
            _bloomPass.BlurSize = Mathf.Lerp(key.BloomBlurSize, next.BloomBlurSize, t);
            _bloomPass.BlendMode = (int)key.BloomBlendMode;

            if (!_bloomLogged)
            {
                _bloomLogged = true;
                Debug.Log($"[LivePostEffect] bloom on at frame {(int)frame}: threshold={_bloomPass.Threshold:0.00} intensity={_bloomPass.Intensity:0.00} blur={_bloomPass.BlurSize:0.00} blend={key.BloomBlendMode}");
            }
        }

        static List<LiveTimelineKeyPostFilmData> FilmKeys(IEnumerable<LiveTimelineKeyPostFilmDataList> lists)
        {
            return lists
                .Where(l => l != null && !l.HasAttribute(LiveTimelineKeyDataListAttr.Disable))
                .SelectMany(l => l.thisList)
                .OrderBy(k => k.frame)
                .ToList();
        }

        // Attribute bit the 1054 film keys carry on all but a few; read as "use depth" (unconfirmed)
        const int FilmAttrEnableDepth = 0x100000;

        void UpdateFilm(float frame)
        {
            if (_filmPass == null) return;
            for (int layer = 0; layer < 3; layer++)
            {
                var keys = _filmKeys[layer];
                int index = -1;
                for (int i = 0; i < keys.Count && keys[i].frame <= frame; i++) index = i;
                var param = Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default();
                if (index >= 0)
                {
                    var key = keys[index];
                    var next = index + 1 < keys.Count ? keys[index + 1] : null;
                    float t = 0;
                    if (next != null && next.interpolateType != LiveCameraInterpolateType.None && next.frame > key.frame)
                        t = Mathf.Clamp01(LiveTimelineControl.CalculateInterpolationValue(key, next, frame));
                    else
                        next = key;

                    param.PostFilmMode = (Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode)(int)key.filmMode;
                    param.PostFilmPower = Mathf.Lerp(key.filmPower, next.filmPower, t);
                    param.DepthPower = Mathf.Lerp(key.depthPower, next.depthPower, t);
                    param.DepthClip = Mathf.Lerp(key.DepthClip, next.DepthClip, t);
                    param.PostFilmOffsetParam = Vector2.Lerp(key.filmOffsetParam, next.filmOffsetParam, t);
                    param.PostFilmOptionParam = Vector4.Lerp(key.filmOptionParam, next.filmOptionParam, t);
                    var c0 = Color.Lerp(key.color0, next.color0, t);
                    if (key.colorType == PostColorType.ColorAll)
                    {
                        param.PostFilmColor0 = param.PostFilmColor1 = param.PostFilmColor2 = param.PostFilmColor3 = c0;
                    }
                    else
                    {
                        param.PostFilmColor0 = c0;
                        param.PostFilmColor1 = Color.Lerp(key.color1, next.color1, t);
                        param.PostFilmColor2 = Color.Lerp(key.color2, next.color2, t);
                        param.PostFilmColor3 = Color.Lerp(key.color3, next.color3, t);
                    }
                    param.IsEnableDepth = ((int)key.attribute & FilmAttrEnableDepth) != 0;
                    param.SetRollAngle(Mathf.Lerp(key.RollAngle, next.RollAngle, t) * Mathf.Deg2Rad);
                    var scale = Vector2.Lerp(key.FilmScale, next.FilmScale, t);
                    param.SetScale(new Vector2(scale.x == 0 ? 1 : scale.x, scale.y == 0 ? 1 : scale.y));
                }
                _filmPass.Layers[layer] = param;
            }

            if (!_filmLogged && _filmPass.Layers.Any(p => p.IsValidity))
            {
                _filmLogged = true;
                var p = _filmPass.Layers.First(l => l.IsValidity);
                Debug.Log($"[LivePostEffect] post film on at frame {(int)frame}: mode={p.PostFilmMode} power={p.PostFilmPower:0.00} color={p.PostFilmColor0} offset={p.PostFilmOffsetParam} option={p.PostFilmOptionParam}");
            }
        }

        bool _filmLogged;

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
            if (!_cameras.Contains(camera)) return;
            var renderer = camera.GetUniversalAdditionalCameraData()?.scriptableRenderer;
            if (renderer == null) return;
            if (_sunPass != null && _sunPass.Enabled) renderer.EnqueuePass(_sunPass);
            if (_bloomPass != null && _bloomPass.Enabled) renderer.EnqueuePass(_bloomPass);
            if (_filmPass != null && _filmPass.Layers.Any(p => p.IsValidity)) renderer.EnqueuePass(_filmPass);
        }

        /// <summary>
        /// The worksheet's bloom through the game's FastBloom, in the order of Unity's legacy FastBloom whose passes it
        /// keeps (0 Bloom, 1 DownSample, 2 BlurVertical, 3 BlurHorizontal); the blur size is taken as quarter
        /// resolution texels (x0.5, +1 per iteration, as the legacy one); _BloomIsScreenBlend 0 screen / 1 add.
        /// 1054: threshold 0.85, intensity 8, blur 3.7, hdrBloomSettings.bloomBlurIterations 1.
        /// </summary>
        class BloomPass : ScriptableRenderPass
        {
            const int Downsample = 4;
            const float WidthMod = 0.5f;
            static readonly int SourceId = Shader.PropertyToID("_LiveBloomSource");
            static readonly int ResultId = Shader.PropertyToID("_LiveBloomResult");
            static readonly int ParameterId = Shader.PropertyToID("_Parameter");
            static readonly int BloomId = Shader.PropertyToID("_Bloom");
            static readonly int BloomIsScreenBlendId = Shader.PropertyToID("_BloomIsScreenBlend");

            readonly Material _material;
            readonly int _iterations;
            RenderTexture _bloomA, _bloomB;
            public bool Enabled;
            public float Threshold, Intensity, BlurSize;
            public int BlendMode;

            public BloomPass(Material material, int iterations)
            {
                _material = material;
                _iterations = Mathf.Clamp(iterations, 1, 4);
                // Between the sun shafts and the film layers, which the game composites after its bloom
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1;
            }

            RenderTexture Ensure(RenderTexture rt, RenderTextureDescriptor desc, string name)
            {
                if (rt != null && rt.IsCreated() && rt.width == desc.width && rt.height == desc.height) return rt;
                if (rt != null) rt.Release();
                rt = new RenderTexture(desc) { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                rt.Create();
                return rt;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                var small = desc;
                small.width = Mathf.Max(1, desc.width / Downsample);
                small.height = Mathf.Max(1, desc.height / Downsample);
                // The composite binds _Bloom through a property block, which needs real textures
                _bloomA = Ensure(_bloomA, small, "LiveBloomA");
                _bloomB = Ensure(_bloomB, small, "LiveBloomB");

                RenderTargetIdentifier target = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cmd = CommandBufferPool.Get("LiveBloom");
                cmd.GetTemporaryRT(SourceId, desc, FilterMode.Bilinear);
                cmd.GetTemporaryRT(ResultId, desc, FilterMode.Bilinear);
                cmd.Blit(target, SourceId);

                // FastBloom declares only _MainTex, so these reach it as command buffer globals, in draw order.
                // Its programs (disassembled): DownSample = (average of 4 taps - _Parameter.z) * .w; BlurVertical
                // steps .y and also subtracts .z; BlurHorizontal steps .x and multiplies by .w; the steps are uv
                // offsets, not texels (passing texels drew screen-wide streaks). So threshold and intensity go in the
                // downsample and the blurs only spread it.
                cmd.SetGlobalFloat(BloomIsScreenBlendId, BlendMode);
                cmd.SetGlobalVector(ParameterId, new Vector4(0, 0, Threshold, Intensity));
                DrawSize = desc;
                DrawFullscreen(cmd, SourceId, _bloomA, _material, 1, false, -1, null);
                DrawSize = small;
                for (int i = 0; i < _iterations; i++)
                {
                    float texels = BlurSize * WidthMod + i;
                    cmd.SetGlobalVector(ParameterId, new Vector4(0, texels / small.height, 0, 1));
                    DrawFullscreen(cmd, _bloomA, _bloomB, _material, 2, false, -1, null);
                    cmd.SetGlobalVector(ParameterId, new Vector4(texels / small.width, 0, 0, 1));
                    DrawFullscreen(cmd, _bloomB, _bloomA, _material, 3, false, -1, null);
                }
                DrawSize = desc;
                DrawFullscreen(cmd, SourceId, ResultId, _material, 0, false, BloomId, _bloomA);
                RestoreCameraMatrices(cmd, ref renderingData);
                cmd.Blit(ResultId, target);

                cmd.ReleaseTemporaryRT(SourceId);
                cmd.ReleaseTemporaryRT(ResultId);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        /// <summary>
        /// The worksheet's three post film layers through the reconstructed ScreenOverlayRender.PostFilmBlit and
        /// the game's PostBlit_Rich (pass 1 = first layer, 3 = second and third; +1 is the inverse vignette).
        /// </summary>
        class FilmPass : ScriptableRenderPass
        {
            const int FilmPass1st = 1;
            const int FilmPass2nd = 3;
            static readonly int SourceId = Shader.PropertyToID("_LivePostFilmSource");
            static readonly int ResultId = Shader.PropertyToID("_LivePostFilmResult");

            readonly Material _material;
            readonly Gallop.RenderPipeline.ScreenOverlayRender _render = new Gallop.RenderPipeline.ScreenOverlayRender();
            public readonly Gallop.RenderPipeline.ScreenOverlayRender.Parameter[] Layers =
            {
                Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default(),
                Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default(),
                Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default(),
            };

            public FilmPass(Material material)
            {
                _material = material;
                // After the sun shafts (BeforeRenderingPostProcessing) and the bloom (+1)
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 2;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderTargetIdentifier target = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var source = Gallop.RenderPipeline.RenderTextureHandle.Make(SourceId, desc.width, desc.height);
                var result = Gallop.RenderPipeline.RenderTextureHandle.Make(ResultId, desc.width, desc.height);

                var cmd = CommandBufferPool.Get("LivePostFilm");
                cmd.GetTemporaryRT(SourceId, desc, FilterMode.Bilinear);
                cmd.GetTemporaryRT(ResultId, desc, FilterMode.Bilinear);
                cmd.Blit(target, SourceId);
                DrawSize = desc;
                Gallop.RenderPipeline.ScreenOverlayRender.MaterialBlitOverride = DrawFullscreen;
                _render.PostFilmBlit(context, cmd, source, result, _material,
                    ref Layers[0], ref Layers[1], ref Layers[2], -1, FilmPass1st, FilmPass2nd);
                RestoreCameraMatrices(cmd, ref renderingData);
                cmd.Blit(ResultId, target);
                cmd.ReleaseTemporaryRT(SourceId);
                cmd.ReleaseTemporaryRT(ResultId);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public void Release()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        static Mesh _fullscreenQuad;

        /// <summary>
        /// A material pass drawn as a clip-space quad with the GPU projection for render textures, which keeps
        /// texture-to-texture draws upright on D3D (1054 recordings: with an identity projection every film layer
        /// flipped the image; cmd.Blit with a material flipped too). Callers restore the camera matrices afterwards.
        /// </summary>
        static void DrawFullscreen(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int pass)
        {
            // Film layers: negative texel y (the shader flips its depth uv). Upright in 1054 recording 013; with a
            // positive one (recording 014) the image came out upside down again, unexplained
            DrawFullscreen(cmd, source, destination, material, pass, true, -1, null);
        }

        /// <param name="negativeTexelY">Pass _MainTex_TexelSize with a negative y: the film shader then flips its depth
        /// uv (needed for film), but FastBloom's composite flips its _Bloom uv on it (not wanted); both disassembled</param>
        /// <param name="extraTextureId">An extra texture the pass samples (FastBloom's _Bloom), or -1</param>
        static void DrawFullscreen(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int pass,
            bool negativeTexelY, int extraTextureId, Texture extraTexture)
        {
            if (_fullscreenQuad == null)
            {
                _fullscreenQuad = new Mesh { name = "LivePostEffectQuad", hideFlags = HideFlags.HideAndDontSave };
                _fullscreenQuad.SetVertices(new List<Vector3> { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) });
                _fullscreenQuad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                _fullscreenQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            }
            // The shaders declare _MainTex, so the material's own value beats a global one: bind the source through
            // a property block, which needs a real texture, hence the plain (non-flipping) copy into a scratch one
            var scratch = Scratch(DrawSize);
            cmd.Blit(source, scratch);
            var block = new MaterialPropertyBlock();
            block.SetTexture(MainTexId, scratch);
            // The game's film shader flips its depth uv when _MainTex_TexelSize.y < 0 (its vertex program, seen with
            // D3DDisassemble). The camera depth stays in flipped storage while this copy is upright: without it the
            // depth-weighted layers drew mirrored silhouettes (1054, frames 1200 and 1680)
            float sign = negativeTexelY ? -1f : 1f;
            block.SetVector(MainTexTexelSizeId, new Vector4(1f / scratch.width, sign / scratch.height, scratch.width, sign * scratch.height));
            if (extraTexture != null) block.SetTexture(extraTextureId, extraTexture);
            cmd.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.identity, true));
            cmd.DrawMesh(_fullscreenQuad, Matrix4x4.identity, material, 0, pass, block);
        }

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int MainTexTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");

        // Size of the scratch copy for the next DrawFullscreen calls; commands run in order, so one per size is enough
        static RenderTextureDescriptor DrawSize;
        static readonly Dictionary<(int, int), RenderTexture> _scratch = new Dictionary<(int, int), RenderTexture>();

        static RenderTexture Scratch(RenderTextureDescriptor desc)
        {
            if (_scratch.TryGetValue((desc.width, desc.height), out var rt) && rt != null && rt.IsCreated()) return rt;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            rt = new RenderTexture(desc) { name = $"LivePostEffectScratch{desc.width}x{desc.height}", hideFlags = HideFlags.HideAndDontSave };
            rt.Create();
            _scratch[(desc.width, desc.height)] = rt;
            return rt;
        }

        static void RestoreCameraMatrices(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cmd.SetViewProjectionMatrices(renderingData.cameraData.GetViewMatrix(), renderingData.cameraData.GetProjectionMatrix());
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
            // 1/10 washed 1054's frame 9000 (power 30, blurRadius 10) out to white; the game's is bright yellow
            const float PowerScale = 1f / 20f;
            const int Downsample = 4;

            static readonly int SourceId = Shader.PropertyToID("_LiveSunShaftsSource");
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
                cmd.GetTemporaryRT(SourceId, desc, FilterMode.Bilinear);
                cmd.GetTemporaryRT(MaskId, small, FilterMode.Bilinear);
                cmd.GetTemporaryRT(BlurId, small, FilterMode.Bilinear);
                cmd.GetTemporaryRT(CompositeId, desc, FilterMode.Bilinear);
                // Read a temporary copy of the camera target, as the film pass does: reading the target directly into
                // the draw's scratch texture left this pass flipped (1054 recordings 007 and 008)
                cmd.Blit(source, SourceId);

                _material.SetVector(SunPositionId, new Vector4(sun.x, sun.y, MaxRadius, sun.z));
                _material.SetFloat(ThresholdId, _blackLevel);
                _material.SetFloat(CenterBrightnessId, _centerBrightness);
                _material.SetFloat(BlendAddId, _blendAdd ? 1f : 0f);
                _material.SetColor(SunColorId, Color * (Power * _intensity * PowerScale));
                _material.SetFloat(ScreenColorPowerId, ScreenColorPower);
                _material.SetFloat(EffectColorPowerId, EffectColorPower);

                DrawSize = desc;
                DrawFullscreen(cmd, SourceId, MaskId, _material, 0);
                // Legacy blur schedule: each pass reaches further towards the sun
                DrawSize = small;
                int from = MaskId, to = BlurId;
                for (int i = 0; i < _iterations; i++)
                {
                    float ofs = BlurRadius * ((i * 2f + 1f) * 6f) / 768f;
                    cmd.SetGlobalVector(BlurRadiusId, new Vector4(ofs, ofs, 0, 0));
                    DrawFullscreen(cmd, from, to, _material, 1);
                    (from, to) = (to, from);
                }

                cmd.SetGlobalTexture(ShaftsTexId, from);
                DrawSize = desc;
                DrawFullscreen(cmd, SourceId, CompositeId, _material, 2);
                RestoreCameraMatrices(cmd, ref renderingData);
                cmd.Blit(CompositeId, source);

                cmd.ReleaseTemporaryRT(SourceId);
                cmd.ReleaseTemporaryRT(MaskId);
                cmd.ReleaseTemporaryRT(BlurId);
                cmd.ReleaseTemporaryRT(CompositeId);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
