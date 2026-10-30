// Screen-space light shafts for the live timeline's volumeLightKeys ("SunShafts"), after Unity's legacy SunShafts
// image effect: bright pixels around the sun are blurred radially towards it and added back over the image.
Shader "Hidden/UmaViewer/LiveSunShafts"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _ShaftsTex;
    sampler2D_float _CameraDepthTexture;
    float4 _SunPosition;   // xy = sun in viewport uv, z = max radius, w = sun eye depth
    float4 _BlurRadius4;   // xy = step towards the sun
    float4 _SunColor;      // rgb = color1 * intensity
    float _Threshold;      // sunShaftsSettings.blackLevel
    float _CenterBrightness;
    float _BlendAdd;       // screenBlendMode 1 = add, 0 = screen
    float _ScreenColorPower;
    float _EffectColorPower;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord.xy;
        return o;
    }

    // What lies behind the sun glows (anything in front of it, such as the characters, blocks it and casts the
    // shafts), plus bright pixels above the black level; aspect-corrected distance keeps the falloff round
    half4 fragMask(v2f i) : SV_Target
    {
        half3 col = tex2D(_MainTex, i.uv).rgb;
        float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
        half behind = depth >= _SunPosition.w ? _CenterBrightness : 0;
        half3 bright = max(col - _Threshold, 0);
        float2 d = (_SunPosition.xy - i.uv) * float2(_ScreenParams.x / _ScreenParams.y, 1);
        half falloff = saturate(1 - length(d) / _SunPosition.z);
        return half4((behind + bright) * falloff * falloff, 1);
    }

    half4 fragRadialBlur(v2f i) : SV_Target
    {
        float2 step = (_SunPosition.xy - i.uv) * _BlurRadius4.xy;
        float2 uv = i.uv;
        half3 sum = 0;
        for (int s = 0; s < 6; s++)
        {
            sum += tex2D(_MainTex, uv).rgb;
            uv += step;
        }
        return half4(sum / 6.0, 1);
    }

    half4 fragComposite(v2f i) : SV_Target
    {
        half3 col = saturate(tex2D(_MainTex, i.uv).rgb * _ScreenColorPower);
        half3 shafts = saturate(tex2D(_ShaftsTex, i.uv).rgb * _SunColor.rgb * _EffectColorPower);
        half3 screen = 1 - (1 - col) * (1 - shafts);
        return half4(lerp(screen, col + shafts, _BlendAdd), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragMask
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragRadialBlur
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
}
