Shader "Custom/TriplanarConcrete"
{
    Properties
    {
        // Albedo textures per surface type
        _MainTexRoad        ("Road Albedo",           2D) = "gray" {}
        _MainTexFootpath    ("Footpath Albedo",       2D) = "gray" {}
        _MainTexCurbFace    ("Curb Face Albedo",      2D) = "gray" {}
        _MainTexGutterDrop  ("Gutter Drop Albedo",    2D) = "gray" {}
        _MainTexGutterRun   ("Gutter Run Albedo",     2D) = "gray" {}

        _Tiling             ("World Tiling (per meter)", Float) = 0.5
        _Color              ("Global Tint", Color) = (1,1,1,1)

        // Per-surface brightness controls
        _RoadBrightness        ("Road Brightness",        Range(0,2)) = 1
        _FootpathBrightness    ("Footpath Brightness",    Range(0,2)) = 1
        _CurbFaceBrightness    ("Curb Face Brightness",   Range(0,2)) = 1
        _GutterDropBrightness  ("Gutter Drop Brightness", Range(0,2)) = 1
        _GutterRunBrightness   ("Gutter Run Brightness",  Range(0,2)) = 1

        // Footpath-specific controls
        _FootpathTilingMul   ("Footpath Tiling Mul", Float) = 1
        _FootpathWorldOffset ("Footpath World Offset (m)", Vector) = (0,0,0,0)

        // Wetness controls (for GutterRun)
        _WetMinHeight        ("Wet Min Height (world Y)", Float) = 0.0
        _WetMaxHeight        ("Wet Max Height (world Y)", Float) = 0.1
        _WetPower            ("Wet Falloff Power",        Float) = 1.5
        _WetStrength         ("Wet Strength",             Range(0,1)) = 0.7
        _WetDarkenFactor     ("Wet Darken Factor",        Range(0,1)) = 0.6
        _WetSmoothnessBoost  ("Wet Smoothness Boost",     Range(0,1)) = 0.3

        // PBR sliders
        _Smoothness         ("Smoothness", Range(0,1)) = 0.5
        _Metallic           ("Metallic",   Range(0,1)) = 0.0

        // Shared PBR maps
        _NormalMap          ("Normal Map", 2D) = "bump" {}
        _MetallicGlossMap   ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _AOMap              ("AO (R)", 2D) = "white" {}
        _HeightMap          ("Height (R, unused)", 2D) = "black" {} // reserved for later
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert

        #include "UnityCG.cginc"

        struct Input
        {
            float3 worldPos;
            float3 triWorldNormal;  // our own world-space normal for tri-planar
            fixed4 color : COLOR;   // vertex color (R encodes RoadSurfaceType)
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            float3 worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 worldNormal = UnityObjectToWorldNormal(v.normal);

            o.worldPos       = worldPos;
            o.triWorldNormal = worldNormal;
            o.color          = v.color;
        }

        // Albedo textures
        sampler2D _MainTexRoad;
        sampler2D _MainTexFootpath;
        sampler2D _MainTexCurbFace;
        sampler2D _MainTexGutterDrop;
        sampler2D _MainTexGutterRun;

        // Shared PBR maps
        sampler2D _NormalMap;
        sampler2D _MetallicGlossMap;
        sampler2D _AOMap;

        float  _Tiling;
        fixed4 _Color;
        half   _Smoothness;
        half   _Metallic;

        half _RoadBrightness;
        half _FootpathBrightness;
        half _CurbFaceBrightness;
        half _GutterDropBrightness;
        half _GutterRunBrightness;

        float  _FootpathTilingMul;
        float4 _FootpathWorldOffset;

        // Wetness params
        float _WetMinHeight;
        float _WetMaxHeight;
        float _WetPower;
        float _WetStrength;
        float _WetDarkenFactor;
        float _WetSmoothnessBoost;

        float3 TriplanarWeights(float3 worldNormal)
        {
            float3 an = abs(worldNormal);
            float sum = an.x + an.y + an.z + 1e-5;
            return an / sum;
        }

        float4 TriplanarSampleRGBA(sampler2D tex, float3 wp, float3 weights)
        {
            float2 uvX = float2(wp.z, wp.y); // YZ
            float2 uvY = float2(wp.x, wp.z); // XZ
            float2 uvZ = float2(wp.x, wp.y); // XY

            float4 cX = tex2D(tex, uvX);
            float4 cY = tex2D(tex, uvY);
            float4 cZ = tex2D(tex, uvZ);

            return cX * weights.x + cY * weights.y + cZ * weights.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wpBase = IN.worldPos;           // raw world position
            float3 n      = normalize(IN.triWorldNormal);
            float3 w      = TriplanarWeights(n);

            // IDs: 1=Road, 2=Footpath, 3=CurbFace, 4=GutterDrop, 5=GutterRun
            float idF = IN.color.r * 255.0;
            int   id  = (int)(idF + 0.5);

            // Base tiled world coords
            float3 wpGlobal = wpBase * _Tiling;

            // Footpath-specific world coords (offset + extra tiling)
            float  footTiling = _Tiling * _FootpathTilingMul;
            float3 wpFoot     = (wpBase + _FootpathWorldOffset.xyz) * footTiling;

            float3 albedoRGB;
            half   brightness;

            if (id == 1) // Road
            {
                albedoRGB  = TriplanarSampleRGBA(_MainTexRoad, wpGlobal, w).rgb;
                brightness = _RoadBrightness;
            }
            else if (id == 2) // Footpath
            {
                albedoRGB  = TriplanarSampleRGBA(_MainTexFootpath, wpFoot, w).rgb;
                brightness = _FootpathBrightness;
            }
            else if (id == 3) // CurbFace
            {
                albedoRGB  = TriplanarSampleRGBA(_MainTexCurbFace, wpGlobal, w).rgb;
                brightness = _CurbFaceBrightness;
            }
            else if (id == 4) // GutterDrop
            {
                albedoRGB  = TriplanarSampleRGBA(_MainTexGutterDrop, wpGlobal, w).rgb;
                brightness = _GutterDropBrightness;
            }
            else if (id == 5) // GutterRun
            {
                albedoRGB  = TriplanarSampleRGBA(_MainTexGutterRun, wpGlobal, w).rgb;
                brightness = _GutterRunBrightness;
            }
            else
            {
                // fallback: treat as road
                albedoRGB  = TriplanarSampleRGBA(_MainTexRoad, wpGlobal, w).rgb;
                brightness = _RoadBrightness;
            }

            float3 albedo = albedoRGB * brightness * _Color.rgb;

            // AO (shared)
            float ao = TriplanarSampleRGBA(_AOMap, wpGlobal, w).r;
            ao = max(ao, 0.001);
            albedo *= ao;

            // Metallic/Smoothness (shared)
            float4 mg          = TriplanarSampleRGBA(_MetallicGlossMap, wpGlobal, w);
            float  metallicMap = mg.r;
            float  smoothMap   = mg.a;

            float metallic   = saturate(metallicMap * _Metallic);
            float smoothness = saturate(smoothMap   * _Smoothness);

            // --- Wetness only for GutterRun (id == 5) ------------------------
            float wetMask = 0.0;

            if (id == 5)
            {
                // Map world Y into [0,1] between WetMinHeight and WetMaxHeight
                float h0 = _WetMinHeight;
                float h1 = _WetMaxHeight;
                float invRange = 1.0 / max(h1 - h0, 1e-4);

                float t = saturate( (IN.worldPos.y - h0) * invRange );

                // We want strongest wetness at low Y, fading out toward high Y
                // t = 0 => bottom, t = 1 => top
                float baseMask = 1.0 - t;

                // Shape with power (higher power = more concentrated near bottom)
                baseMask = pow(baseMask, max(_WetPower, 0.0001));

                // Scale by strength
                wetMask = saturate(baseMask * _WetStrength);

                // Apply darkening
                float darkFactor = lerp(1.0, _WetDarkenFactor, wetMask);
                albedo *= darkFactor;

                // Apply smoothness boost
                float targetSmooth = saturate(smoothness + _WetSmoothnessBoost);
                smoothness = lerp(smoothness, targetSmooth, wetMask);
            }

            // Normal (shared)
            float3 wpN = wpGlobal;

            float2 uvX = float2(wpN.z, wpN.y);
            float2 uvY = float2(wpN.x, wpN.z);
            float2 uvZ = float2(wpN.x, wpN.y);

            float3 nX = UnpackNormal(tex2D(_NormalMap, uvX));
            float3 nY = UnpackNormal(tex2D(_NormalMap, uvY));
            float3 nZ = UnpackNormal(tex2D(_NormalMap, uvZ));

            float3 nTri = normalize(nX * w.x + nY * w.y + nZ * w.z);

            o.Normal     = nTri;
            o.Albedo     = albedo;
            o.Alpha      = 1.0;
            o.Metallic   = metallic;
            o.Smoothness = smoothness;
        }

        ENDCG
    }

    FallBack "Standard"
}
