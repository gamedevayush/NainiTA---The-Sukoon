// The wind on foliage and cloth (owner, 23 Nov 2026: "game me kuch feel ki kami hai...like air reaction with trees, kapdo
// ki dukaan me kapde hilna hava se"). Included by Shaders/FoliageWind.shader (URP Lit, copied) after each pass's own file:
// each pass's vertex function is wrapped - the vertex is moved by the wind, then URP's own vertex function runs, so the
// lighting, shadows, Forward+ lights, fog and depth are URP's exactly. Cost: a few sins per vertex, no CPU.
//   _NtWind      global, set by WeatherSystem: 0.3 still air .. 1 a storm, with gusts
//   _WindAmount  metres the top moves at full wind (trees ~0.35, grass ~0.08, cloth ~0.06)
//   _WindHeight  1 / the object's height (object space): the bend grows with height squared (cloth: 0 = no height weight)
//   _WindFlutter metres of quick leaf / fabric flutter at full wind
#ifndef NT_WIND_INCLUDED
#define NT_WIND_INCLUDED

float _NtWind;
float _WindAmount, _WindHeight, _WindFlutter, _WindBob;   // _WindBob: metres a moored boat rises and falls (0 = none)

float3 NtWindWorld(float3 posOS)
{
    float3 wp = TransformObjectToWorld(posOS);
    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float w = max(_NtWind, 0.0);
    float h = _WindHeight > 0 ? saturate(posOS.y * _WindHeight) : 1.0;
    float t = _Time.y;
    float phase = dot(origin.xz, float2(0.071, 0.113)) + dot(wp.xz, float2(0.011, 0.017));
    // a slow sway with a gust riding on it, along the valley's wind (from the west-south-west)
    float sway = sin(t * 0.85 + phase) * 0.6 + sin(t * 1.9 + phase * 1.7) * 0.25 + 0.35;
    float gust = saturate(sin(t * 0.23 + origin.x * 0.004) * 0.5 + 0.5);
    float2 dir = float2(0.83, 0.55);
    float bend = h * h * _WindAmount * w * sway * (0.7 + gust * 0.6);
    float flutter = sin(t * 7.3 + dot(wp, float3(1.7, 1.1, 1.3))) * cos(t * 5.1 + dot(wp, float3(0.9, 2.3, 0.7)));
    flutter *= _WindFlutter * w * h;
    wp.xz += dir * bend + float2(-dir.y, dir.x) * flutter;
    wp.y += flutter * 0.35 - bend * 0.15;
    // moored boats on the lake: a slow swell running across the water, so each boat rises, falls and pitches a little
    // against its neighbours
    float swell = sin(t * 1.15 + dot(wp.xz, float2(0.31, 0.19))) * 0.7 + sin(t * 0.73 + dot(wp.xz, float2(-0.17, 0.27))) * 0.3;
    wp.y += _WindBob * swell * (0.6 + 0.4 * w);
    return wp;
}

float4 NtWindOS(float4 posOS)
{
    return float4(TransformWorldToObject(NtWindWorld(posOS.xyz)), posOS.w);
}

#ifdef NT_WIND_FORWARD
Varyings WindLitPassVertex(Attributes input) { input.positionOS = NtWindOS(input.positionOS); return LitPassVertex(input); }
#endif
#ifdef NT_WIND_SHADOW
Varyings WindShadowPassVertex(Attributes input) { input.positionOS = NtWindOS(input.positionOS); return ShadowPassVertex(input); }
#endif
#ifdef NT_WIND_GBUFFER
Varyings WindLitGBufferPassVertex(Attributes input) { input.positionOS = NtWindOS(input.positionOS); return LitGBufferPassVertex(input); }
#endif
#ifdef NT_WIND_DEPTH
Varyings WindDepthOnlyVertex(Attributes input) { input.position = NtWindOS(input.position); return DepthOnlyVertex(input); }
#endif
#ifdef NT_WIND_DEPTHNORMALS
Varyings WindDepthNormalsVertex(Attributes input) { input.positionOS = NtWindOS(input.positionOS); return DepthNormalsVertex(input); }
#endif

#endif
