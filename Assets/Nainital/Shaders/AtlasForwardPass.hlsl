#ifndef NAINITAL_ATLAS_FORWARD_PASS_INCLUDED
#define NAINITAL_ATLAS_FORWARD_PASS_INCLUDED

// Nainital: the grime needs the world position in the fragment
#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

// Nainital weathering (globals, set by SurfaceLook so the SRP batcher still batches this shader)
TEXTURE2D(_NainitalGrimeTex);
SAMPLER(sampler_NainitalGrimeTex);
float _NainitalGrimeScale;      // grime texture repeats per metre
float _NainitalGrimeStrength;   // 0: none
float _NainitalMuted;           // 0: palette colours as they are, 1: grey

void NainitalWeather(float3 positionWS, float3 normalWS, inout SurfaceData s)
{
    if (_NainitalGrimeStrength <= 0.0) return;
    float3 n = abs(normalWS);
    n = n / max(n.x + n.y + n.z, 1e-4);
    float3 p = positionWS * _NainitalGrimeScale;
    // walls: R channel, stretched down the face so stains run; tops: G channel
    half gx = SAMPLE_TEXTURE2D(_NainitalGrimeTex, sampler_NainitalGrimeTex, float2(p.z, p.y * 0.4)).r;
    half gz = SAMPLE_TEXTURE2D(_NainitalGrimeTex, sampler_NainitalGrimeTex, float2(p.x, p.y * 0.4)).r;
    half gy = SAMPLE_TEXTURE2D(_NainitalGrimeTex, sampler_NainitalGrimeTex, p.xz).g;
    half g = gx * n.x + gy * n.y + gz * n.z;              // ~0.8 is clean
    half lum = dot(s.albedo, half3(0.3, 0.59, 0.11));
    s.albedo = lerp(s.albedo, lum.xxx, _NainitalMuted);
    s.albedo *= lerp(1.0h, g * 1.25h, _NainitalGrimeStrength);
    s.smoothness *= lerp(1.0h, 0.6h, _NainitalGrimeStrength);
}

// Nainital fake interiors on ordinary ground-floor bays (the shared photo rooms: NainitalRooms.hlsl). BuildingKit moves
// the bay's atlas UV by whole atlas widths (Atlas.InteriorBay: u - 1 - (slot & 1), v - (slot >> 1)); the atlas textures
// repeat, so every pass samples the same texels, and here the fragment reads the flag (u < 0) and the room slot back from
// the raw UV. Where the room shows comes from the atlas alpha (1 - glass, FacadeArt). Nothing extra per vertex.
#include "NainitalRooms.hlsl"
#define NT_ATLAS_CELLS      16.0
#define NT_CELL_INSET       (2.5 / 64.0)    // Atlas.InsetPx / Atlas.CellPx
#define NT_BAY_W            3.2             // a facade bay, metres (BuildingKit: bays of ~3.2 m, floors of 3 m)
#define NT_BAY_H            3.0
// back-plane depths, photo size and height: the owner's InteriorLook ("Other ground floors"), NtBayFrame()
#define NT_GLASS_ALBEDO     0.03
// photo rooms (Atlas.Room*): homes 0..3; the shops an ordinary bazaar bay picks from
static const float NtShopPool[12] = { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 28 };

// rawUV: the mesh UV (flagged when u < 0). sgn: +1 when the bay's u runs along cross(up, N), -1 when mirrored.
// bayDx, bayDy: the bay coordinates' screen derivatives (taken outside the branch).
void NainitalInterior(float2 rawUV, float3 positionWS, float3 normalWS, float sgn, float2 bayDx, float2 bayDy, inout SurfaceData s)
{
    half glass = saturate(1.0h - s.alpha);
    s.alpha = 1.0h;
    if (glass < 0.004h) return;
    float k = floor(-rawUV.x);
    float m = rawUV.y < 0.0 ? 1.0 : 0.0;
    float2 cellF = frac(rawUV) * NT_ATLAS_CELLS;
    float2 cell = floor(cellF);
    float2 bay = saturate((cellF - cell - NT_CELL_INSET) / (1.0 - 2.0 * NT_CELL_INSET));
    // facade style of the cell (Atlas.Facade / StrongCell); 4 and 5 are the shop fronts
    float style = cell.x < 9.5 ? cell.x : fmod(cell.y * 6.0 + cell.x - 10.0, 10.0);
    bool shop = abs(style - 4.5) < 0.9;
    float slot = k + 2.0 * m;
    float room = shop ? NtShopPool[(int)fmod(slot + 3.0 * cell.y + 5.0 * cell.x, 12.0)] : fmod(slot + cell.y, 4.0);

    float3 N = normalize(normalWS);
    float3 T = sgn * normalize(cross(float3(0.0, 1.0, 0.0), N));
    NtFrame f = NtBayFrame();
    float2 glassM = float2((bay.x - 0.5) * NT_BAY_W, bay.y * NT_BAY_H);
    float2 plane = NtBackPlane(glassM, positionWS - GetCameraPositionWS(), T, N, shop ? f.depthB : f.depthA);
    half inside;
    float2 puv = NtPhotoUV(room, plane, f, inside);
    float2 sc = float2(NT_BAY_W, NT_BAY_H);
    half3 photo = SAMPLE_TEXTURE2D_GRAD(_RoomTex, sampler_RoomTex, puv, NtPhotoGrad(bayDx * sc, f), NtPhotoGrad(bayDy * sc, f)).rgb * inside;

    s.emission = lerp(s.emission, NtRoomLight(photo, s.emission), glass);
    // a window is glass (dark, glossy: the sky and street reflect in it); a shop front is open
    half3 behind = shop ? half3(0.0h, 0.0h, 0.0h) : half3(NT_GLASS_ALBEDO, NT_GLASS_ALBEDO, NT_GLASS_ALBEDO);
    s.albedo = lerp(s.albedo, behind, glass);
    s.smoothness = lerp(s.smoothness, shop ? 0.0h : 0.92h, glass);
    s.occlusion = lerp(s.occlusion, 1.0h, glass);
    s.normalTS = normalize(lerp(s.normalTS, half3(0.0h, 0.0h, 1.0h), glass));
}

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);   // a flagged ground-floor bay keeps its shifted UV: the textures repeat

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

// Used in Standard (Physically Based) shader
void LitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    NainitalWeather(input.positionWS, input.normalWS, surfaceData);
    {
        // derivatives outside the branch: the bay's mip footprint, and which way its u runs (sign of dP/du along
        // cross(up, N): the facade art is mirrored on some walls)
        float2 bayDx = ddx(input.uv) * (NT_ATLAS_CELLS / (1.0 - 2.0 * NT_CELL_INSET));
        float2 bayDy = ddy(input.uv) * (NT_ATLAS_CELLS / (1.0 - 2.0 * NT_CELL_INSET));
        float3 along = cross(float3(0.0, 1.0, 0.0), input.normalWS);
        float sgn = dot(ddx(input.positionWS), along) * ddx(input.uv.x) + dot(ddy(input.positionWS), along) * ddy(input.uv.x) >= 0.0 ? 1.0 : -1.0;
        if (input.uv.x < 0.0 && _NainitalInteriorOff < 0.5)   // one path per bay: the flag is the same over a whole quad
            NainitalInterior(input.uv, input.positionWS, input.normalWS, sgn, bayDx, bayDy, surfaceData);
    }

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
