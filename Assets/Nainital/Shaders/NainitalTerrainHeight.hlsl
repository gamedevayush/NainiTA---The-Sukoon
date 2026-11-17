#ifndef NAINITAL_TERRAIN_HEIGHT_INCLUDED
#define NAINITAL_TERRAIN_HEIGHT_INCLUDED

// The terrain's height read at full precision on phones (user, iPhone bugs 2026-11-16: "green terrain showing up on road",
// "road is not smooth", "DSA road ... smoothed not up down", "player stucked in terrain near capitol stairs").
// URP's TerrainInstancing (TerrainLitInput.hlsl) decodes each vertex's height with UnpackHeightmap(real4), and on phones
// (SHADER_API_MOBILE: Metal, Vulkan, GLES) 'real' is half. Our terrain is 1437 m high and the town lies about half way up
// it, where a half holds a 0..1 value only to 1/2048: every height snapped to 0.7 m terraces. On the iPhone the ground
// came through the Mall walk in steps, roads painted into the ground went up and down in steps, and the player was caught
// on a terrace edge by the Capitol's wall. On PC 'real' is float, so the editor never showed it.
// Included before TerrainLitInput.hlsl in every pass of Nainital/Terrain: the same decoding, in float.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float NtUnpackHeightmapFloat(float4 height)
{
#if defined(SHADER_API_VULKAN) || defined(SHADER_API_GLES3) || defined(SHADER_API_WEBGPU)
    return (height.r + height.g * 256.0) / 257.0;   // two 8-bit halves of a 16-bit height
#else
    return height.r;
#endif
}

#define UnpackHeightmap NtUnpackHeightmapFloat

#endif
