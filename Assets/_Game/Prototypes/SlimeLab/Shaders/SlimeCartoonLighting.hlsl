#ifndef SLIME_LAB_CARTOON_LIGHTING_INCLUDED
#define SLIME_LAB_CARTOON_LIGHTING_INCLUDED

// Palette-driven shadows: the scene's dark ambient never turns the candy colors gray.
half3 SlimeCartoonDiffuse(half3 color, half3 shadowTint, half ndl, half attenuation)
{
    half illumination = saturate(ndl * 0.5h + 0.5h) * lerp(0.45h, 1.0h, attenuation);
    half band = 0.40h + 0.32h * smoothstep(0.28h, 0.32h, illumination)
        + 0.28h * smoothstep(0.66h, 0.70h, illumination);
    return lerp(color * shadowTint, color, band);
}

half SlimeCartoonSpot(float2 p, float2 center, float2 radius)
{
    float2 delta = (p - center) / radius;
    float distanceSquared = dot(delta, delta);
    float aa = max(fwidth(distanceSquared), 0.025);
    return 1.0h - smoothstep(1.0 - aa, 1.0 + aa, distanceSquared);
}

#endif
