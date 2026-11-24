#ifndef NAINITAL_TERRAIN_ROADS_INCLUDED
#define NAINITAL_TERRAIN_ROADS_INCLUDED

// The roads and pavings drawn into the terrain (user, 2026-11-16: "saari jitni bhi roads hain sbko terrain me bake kro ...
// koi road separate mesh na ho, merging and mixing bhi theek hogi, texturing me geela ya gadda bhi"), and the dirt and
// the wet patches over all of it. Everything here is a global set by TerrainRoads.cs.
//   _NtRoadMask   RGBA weights over the whole map: asphalt, cement concrete, grey pavers, red pavers
//   _NtRoadRect   x0, z0, 1/width, 1/depth of the mask in world metres
//   _NtTiles      metres per repeat: asphalt, concrete, pavers, dirt noise
//   _NtDirt       tileable noise: r large blotches, g fine grime, b damp patches

TEXTURE2D(_NtRoadMask);   SAMPLER(sampler_NtRoadMask);
TEXTURE2D(_NtAsphalt);    SAMPLER(sampler_NtAsphalt);
TEXTURE2D(_NtAsphaltN);
TEXTURE2D(_NtConcrete);
TEXTURE2D(_NtPaver);
TEXTURE2D(_NtPaverN);
TEXTURE2D(_NtDirt);
float4 _NtRoadRect;
float4 _NtTiles;
float4 _NtRedTint;
float4 _NtGreyTint;
float _NtWetness;
float _NtRoadsOn;
float _NtRoadSdf;   // > 0: the mask is signed distances, +- this many metres over 0..1

// a stable 0..1 hash of a world cell (road wear: patches, potholes, manhole covers)
float NtHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

void NtGround(float3 positionWS, inout half3 albedo, inout half smoothness, inout half3 normalTS, inout half occlusion)
{
    if (_NtRoadsOn < 0.5)
        return;
    float2 xz = positionWS.xz;
    float2 uvD = xz / _NtTiles.w;
    half4 dirt = SAMPLE_TEXTURE2D(_NtDirt, sampler_NtAsphalt, uvD);
    half4 fine = SAMPLE_TEXTURE2D(_NtDirt, sampler_NtAsphalt, uvD * 4.37 + 0.31);

    float2 muv = (xz - _NtRoadRect.xy) * _NtRoadRect.zw;
    half4 w;
    half dust = 0.0h;   // a band of dust on the ground just outside an edge
    if (_NtRoadSdf > 0.0)
    {
        // the mask holds each surface's signed distance to its edge (m, + inside; WorldBuilder.RoadMask.cs): the edge is cut
        // at 0, sharp and parallel to the road at any mask size (iPhone bugs 2026-11-16: "dotted and random", "make it
        // perfect parallel to road"). Only a hair of wear (+-3 cm) along it, never dots.
        float4 d = (SAMPLE_TEXTURE2D(_NtRoadMask, sampler_NtRoadMask, saturate(muv)) - 0.5) * (2.0 * _NtRoadSdf);
        d += (fine.g - 0.5) * 0.02;
        float4 soft = max(fwidth(d), 0.04);
        float4 c = saturate(d / soft + 0.5);
        // where two surfaces overlap the main one lies on top, never a blend (user, 2026-11-17: the red path's end showed as
        // a pink blob on the grey walk): cement (the lake roads, the main ones) over asphalt (the side roads joining them)
        // over grey pavers over red pavers
        w.g = (half)c.y;
        w.r = (half)(c.x * (1.0 - c.y));
        w.b = (half)(c.z * (1.0 - c.x) * (1.0 - c.y));
        w.a = (half)(c.w * (1.0 - c.x) * (1.0 - c.y) * (1.0 - c.z));
        float dmax = max(max(d.x, d.y), max(d.z, d.w));
        dust = (half)(saturate(1.0 + dmax / 0.45) * step(dmax, 0.0));
    }
    else
    {
        w = SAMPLE_TEXTURE2D(_NtRoadMask, sampler_NtRoadMask, saturate(muv));
        // (an older plain coverage mask) frayed, dusty edges: the soft rim is eaten into by the fine noise
        w = saturate((w - fine.g * 0.45) * 2.2);
    }
    half total = saturate(w.r + w.g + w.b + w.a);
    albedo = lerp(albedo, albedo * half3(1.05h, 0.98h, 0.88h) * 0.95h, dust * 0.5h);

    float2 uvA = xz / _NtTiles.x, uvC = xz / _NtTiles.y, uvP = xz / _NtTiles.z;
    float2 dAx = ddx(uvA), dAy = ddy(uvA), dCx = ddx(uvC), dCy = ddy(uvC), dPx = ddx(uvP), dPy = ddy(uvP);
    [branch] if (total > 0.002)
    {
        half inv = 1.0h / max(total, 1e-3h);
        half3 cA = SAMPLE_TEXTURE2D_GRAD(_NtAsphalt, sampler_NtAsphalt, uvA, dAx, dAy).rgb;
        half3 cC = SAMPLE_TEXTURE2D_GRAD(_NtConcrete, sampler_NtAsphalt, uvC, dCx, dCy).rgb;
        half3 cP = SAMPLE_TEXTURE2D_GRAD(_NtPaver, sampler_NtAsphalt, uvP, dPx, dPy).rgb;
        // grey pavers: the paving photo is warm, the walk above the Capitol is grey (user, 2026-11-16), so it is mostly
        // desaturated; red pavers: the same photo tinted terracotta
        half3 cG = lerp(cP, dot(cP, half3(0.3h, 0.59h, 0.11h)).xxx, 0.85h);
        half3 road = (cA * w.r + cC * w.g + cG * _NtGreyTint.rgb * w.b + cP * _NtRedTint.rgb * w.a) * inv;
        half3 nA = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NtAsphaltN, sampler_NtAsphalt, uvA, dAx, dAy));
        half3 nP = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NtPaverN, sampler_NtAsphalt, uvP, dPx, dPy));
        // the second channel is the main roads' black tarmac (19 Nov): the asphalt's grain too
        half3 nRoad = normalize(nA * (w.r + w.g) + nP * (w.b + w.a) + half3(0, 0, 1e-3));
        half sRoad = (0.22h * w.r + 0.2h * w.g + 0.28h * (w.b + w.a)) * inv;
        albedo = lerp(albedo, road, total);
        smoothness = lerp(smoothness, sRoad, total);
        normalTS = normalize(lerp(normalTS, nRoad, total));
        occlusion = lerp(occlusion, 1.0h, total);
        // dust along the rims, where the tarmac or the tiles meet the earth
        half rim = saturate(total * (1.0h - total) * 4.0h);
        albedo = lerp(albedo, albedo * half3(1.04h, 0.97h, 0.86h) * 0.92h, rim * 0.6h);

        // wear on the tarmac (owner, 25 Nov 2026: "game ko or realistic but not heavy"): patches of newer, blacker asphalt,
        // potholes and round iron manhole covers, scattered by a hash of world cells. Arithmetic only (no new textures),
        // on road pixels only; holes hold water in the rain.
        half tar = saturate((w.r + w.g) * inv) * total;
        [branch] if (tar > 0.01h)
        {
            // repair patches: one in ~16% of 7 m cells, a rotated rectangle 1.5-3.5 m by 1-2.4 m
            float2 pc = floor(xz / 7.0);
            float hp = NtHash(pc);
            if (hp < 0.16)
            {
                float a = NtHash(pc + 3.1) * 3.14159;
                float2 o = (float2(NtHash(pc + 1.7), NtHash(pc + 2.3)) - 0.5) * 3.0;
                float2 q = xz - (pc + 0.5) * 7.0 - o;
                float2 r = float2(q.x * cos(a) + q.y * sin(a), -q.x * sin(a) + q.y * cos(a));
                float2 hs = float2(0.75 + NtHash(pc + 4.9) * 1.0, 0.5 + NtHash(pc + 6.2) * 0.7);
                float2 e = abs(r) - hs;
                float edge = max(e.x, e.y) + (fine.g - 0.5) * 0.08;
                half patch = (half)saturate(-edge / 0.04) * tar;
                albedo = lerp(albedo, albedo * 0.7h + 0.03h, patch * 0.85h);   // a different shade: greyer on black tarmac
                smoothness = lerp(smoothness, smoothness + 0.08h, patch);
                albedo *= 1.0h - (half)saturate(1.0 - abs(edge) / 0.05) * 0.25h * tar;   // the cut line round it
            }
            // potholes: one in ~7% of 9 m cells, 0.3-0.7 m across, ragged
            float2 kc = floor(xz / 9.0);
            if (NtHash(kc + 7.7) < 0.07)
            {
                float2 c0 = (kc + 0.2 + float2(NtHash(kc + 8.1), NtHash(kc + 9.3)) * 0.6) * 9.0;
                float rad = 0.3 + NtHash(kc + 5.5) * 0.4;
                float dd = length(xz - c0) / rad + (fine.g - 0.5) * 0.5 + (dirt.g - 0.5) * 0.3;
                half hole = (half)saturate((1.0 - dd) / 0.15) * tar;
                half lip = (half)saturate(1.0 - abs(dd - 1.0) / 0.2) * tar;
                albedo = lerp(albedo, albedo * half3(0.55h, 0.52h, 0.48h), hole);
                albedo = lerp(albedo, albedo * half3(1.08h, 1.02h, 0.92h), lip * 0.5h);   // broken, dusty edge
                float2 g = xz - c0;
                normalTS = normalize(lerp(normalTS, normalize(half3((half2)(-g / max(length(g), 1e-3)) * 0.8h, 1.0h)), lip * 0.7h));
                smoothness = lerp(smoothness, 0.92h, hole * saturate(_NtWetness * 2.5h));   // water standing in it
                albedo *= 1.0h - hole * saturate(_NtWetness * 2.5h) * 0.3h;
            }
            // manhole covers: one in ~10% of 23 m cells, 0.6 m across, a ring and a cross-hatched iron lid
            float2 mc = floor(xz / 23.0);
            if (NtHash(mc + 11.3) < 0.1)
            {
                float2 c1 = (mc + 0.3 + float2(NtHash(mc + 12.9), NtHash(mc + 13.7)) * 0.4) * 23.0;
                float2 g = xz - c1;
                float dm = length(g);
                half lid = (half)saturate((0.3 - dm) / 0.012) * tar;
                half ring = (half)saturate(1.0 - abs(dm - 0.31) / 0.02) * tar;
                half hatch = (half)step(0.5, frac((g.x + g.y) * 9.0)) * (half)step(0.5, frac((g.x - g.y) * 9.0));
                half3 iron = half3(0.16h, 0.155h, 0.15h) * (0.85h + 0.3h * hatch) * (0.8h + 0.4h * fine.g);
                albedo = lerp(albedo, iron, lid);
                albedo = lerp(albedo, albedo * 0.35h, ring);
                smoothness = lerp(smoothness, 0.45h, lid);
                normalTS = normalize(lerp(normalTS, half3(0, 0, 1), lid * 0.8h));
            }
        }
    }

    // dirt over everything: blotches of grime, a little more on the roads (oil, soot, mud carried on tyres)
    half grime = smoothstep(0.5h, 0.9h, dirt.r) * 0.24h + (fine.g - 0.5h) * 0.09h;
    albedo *= 1.0h - grime * (0.55h + 0.45h * total);

    // damp patches and puddles on the roads (drains, shops washing their fronts), and everything wet in the rain
    // on a dry day only darker, slightly sheened patches (16 Nov: at 0.93 they read as mirrors); puddles only in the rain
    half damp = smoothstep(0.64h, 0.74h, dirt.b) * total;
    half wet = max(damp * 0.45h, _NtWetness * total * (0.55h + 0.45h * smoothstep(0.35h, 0.7h, dirt.b)));
    wet = max(wet, _NtWetness * 0.35h * (1.0h - total));
    half puddle = smoothstep(0.7h, 0.8h, dirt.b) * total * saturate(_NtWetness * 2.0h);
    albedo *= 1.0h - wet * 0.42h - puddle * 0.15h;
    smoothness = lerp(smoothness, 0.9h, saturate(wet * (0.55h + 0.45h * _NtWetness) + puddle));
    normalTS = normalize(lerp(normalTS, half3(0, 0, 1), saturate(wet * 0.7h + puddle)));
}

#endif
