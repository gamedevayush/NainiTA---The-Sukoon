#ifndef NAINITAL_ROOMS_INCLUDED
#define NAINITAL_ROOMS_INCLUDED

// Nainital fake interiors (user, 2026-11-17: "fake interior shader saari building or shops pe lagao, only ground floor";
// then "photo lgaaye", "crowd bhi photos me", phone first). The room behind ground-floor glass is a PHOTO (Google Flow,
// Tools/interiors/prompts.json, Indian shoppers and diners baked in) on a flat plane a few metres behind the glass: the
// eye ray through each glass point is carried to that plane (plane parallax), so the room slides behind the frame as
// you walk past. One texture read and ~25 ALU per glass pixel; nothing per vertex.
// Photos: Data/Interiors/RoomPhotos.png, NT_ROOM_COLS x NT_ROOM_ROWS tiles, room r at column r % cols, row r / cols
// counted from the bottom (Tools/interiors/pack_rooms.py). Numbers shared with Atlas.cs (Room*).
// The look (photo size, height, side shift, depths, light) is set by the owner in Nainital -> Fake Interior Look
// (18 Nov: "tool jisse main set kr sku, pos and zoom wagera sab, for all"): Resources/InteriorLook.asset, sent here as
// globals by InteriorLook.Apply. Until it has run (_NtLookSet = 0) the defaults below are used.
#define NT_ROOM_COLS        8.0
#define NT_ROOM_ROWS        4.0
#define NT_ROOM_INSET       (2.0 / 512.0)   // keep bilinear inside one photo

TEXTURE2D(_RoomTex);
SAMPLER(sampler_RoomTex);
float _NainitalInteriorOff;    // global, 1: rooms off (Settings, low quality; InteriorCost's on/off timing)

// the owner's look (InteriorLook.cs); defaults in brackets
float _NtLookSet;
float _NtMallSize, _NtMallMid, _NtMallSide, _NtMallGlassDepth, _NtMallOpenDepth;   // (4.8, 1.1, 0, 3.0, 3.4)
float _NtMallStretchX, _NtMallStretchY, _NtBayStretchX, _NtBayStretchY;             // photo stretch (1, 1, 1, 1)
float _NtMallOpenW, _NtMallSill, _NtMallTop;                                         // Mall glass opening (1, 0, 10)
float4 _NtMallPanel;                                                                 // the panel outside it
float _NtBaySize, _NtBayMid, _NtBaySide, _NtBayWindowDepth, _NtBayShopDepth;       // (4.8, 1.1, 0, 2.6, 3.2)
float _NtRoomDay, _NtRoomAmbient, _NtRoomNight, _NtShopDayLight, _NtMaxSlide;     // (0.22, 0.03, 2.4, 0.13, 4.5)

// One group's framing: how big the photo is on its back plane (metres, square photos), where its middle sits above the
// floor, how far it is shifted to the right, and the two back-plane depths (glass / open for Mall Road, window / shop
// for the other ground floors).
struct NtFrame { float size, mid, side, depthA, depthB; float2 stretch; };

NtFrame NtMallFrame()
{
    NtFrame f;
    bool set = _NtLookSet > 0.5;
    f.size = set ? max(_NtMallSize, 0.5) : 4.8;
    f.stretch = set ? max(float2(_NtMallStretchX, _NtMallStretchY), 0.1) : float2(1.0, 1.0);
    f.mid = set ? _NtMallMid : 1.1;
    f.side = set ? _NtMallSide : 0.0;
    f.depthA = set ? max(_NtMallGlassDepth, 0.05) : 3.0;
    f.depthB = set ? max(_NtMallOpenDepth, 0.05) : 3.4;
    return f;
}

NtFrame NtBayFrame()
{
    NtFrame f;
    bool set = _NtLookSet > 0.5;
    f.size = set ? max(_NtBaySize, 0.5) : 4.8;
    f.stretch = set ? max(float2(_NtBayStretchX, _NtBayStretchY), 0.1) : float2(1.0, 1.0);
    f.mid = set ? _NtBayMid : 1.1;
    f.side = set ? _NtBaySide : 0.0;
    f.depthA = set ? max(_NtBayWindowDepth, 0.05) : 2.6;
    f.depthB = set ? max(_NtBayShopDepth, 0.05) : 3.2;
    return f;
}

// Where the eye ray through the glass point m (metres: x from the opening's centre to the right as seen from outside,
// y up from the floor) meets a plane `depth` metres behind the glass. ray: from the eye to the glass point; T: to the
// right along the opening; N: out of it.
float2 NtBackPlane(float2 m, float3 ray, float3 T, float3 N, float depth)
{
    float maxSlide = _NtLookSet > 0.5 ? max(_NtMaxSlide, 0.1) : 4.5;
    float into = max(-dot(ray, N), 1e-4 * length(ray));
    float2 slide = float2(dot(ray, T), ray.y) * (depth / into);
    float len = length(slide);
    if (len > maxSlide) slide *= maxSlide / len;
    return m + slide;
}

// The photo's uv inside the room atlas for a back-plane point (metres), and how much of it is inside the photo (1) or
// past its edge (fades to dark: the room's sides in shadow).
float2 NtPhotoUV(float room, float2 plane, NtFrame f, out half inside)
{
    float2 p = 0.5 + (plane - float2(f.side, f.mid)) / (f.size * f.stretch);
    float2 over = max(abs(p - 0.5) - 0.5, 0.0);
    inside = saturate(1.0h - (half)max(over.x, over.y) * 5.0h);
    p = lerp(NT_ROOM_INSET, 1.0 - NT_ROOM_INSET, saturate(p));
    float row = floor((room + 0.5) / NT_ROOM_COLS);   // robust to a room index a hair under its whole number
    float2 tile = float2(floor(room + 0.5) - row * NT_ROOM_COLS, row);
    return (tile + p) / float2(NT_ROOM_COLS, NT_ROOM_ROWS);
}

// metres per pixel on the glass -> the photo's mip footprint
float2 NtPhotoGrad(float2 metresPerPixel, NtFrame f)
{
    return metresPerPixel / (f.size * f.stretch * float2(NT_ROOM_COLS, NT_ROOM_ROWS));
}

// Room light: the sun's strength through the glass by day, the front's own glow at night (its colour and strength).
half3 NtRoomLight(half3 photo, half3 nightGlow)
{
    bool set = _NtLookSet > 0.5;
    half day = set ? _NtRoomDay : 0.22h, amb = set ? _NtRoomAmbient : 0.03h, night = set ? _NtRoomNight : 2.4h;
    half sun = dot(_MainLightColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
    return photo * (sun * day + amb) + photo * nightGlow * night;
}

// A shop's own lights by day (Mall Road keeps its tube lights on), added under the night glow.
half NtShopDayLight() { return _NtLookSet > 0.5 ? (half)_NtShopDayLight : 0.13h; }

#endif
