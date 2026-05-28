# Screen Space Volumetric Fog Technical Notes

These notes describe the current implementation in enough detail for another
developer to recreate the system from scratch.

## Summary

The effect is a screen-space fog compositor for URP. It approximates volumetric
fog by accumulating procedural depth intervals along each camera ray, clipping
those intervals against scene depth, and blending the result over the resolved
scene color.

It is not a physically based volumetric renderer. It is a deliberately cheap
screen-space approximation with scene-depth awareness and camera-relative motion.

## Core Terms

- `synthetic depth phase`: a controller-side scalar that advances as the camera
  moves forward or backward through the fog range.
- `logical layer`: one repeating procedural fog band.
- `visible layer`: a logical layer instance currently overlapping normalized
  fog depth `0..1`.
- `fog interval`: the span between a start depth sample and an end depth sample.
- `fog amount`: accumulated visible fog interval length after scene-depth
  clipping and optional height attenuation.
- `per-layer view compensation`: a quaternion per visible layer that keeps the
  directional noise field stable under camera rotation and synthetic parallax.

## Render Pipeline

The effect runs as a full-screen draw call:

```csharp
commandBuffer.DrawProcedural(
    Matrix4x4.identity,
    fogMaterial,
    0,
    MeshTopology.Triangles,
    3,
    1);
```

The shader pass has:

```text
ZWrite Off
ZTest Always
Cull Off
Blend Off
```

The shader samples the resolved scene color from `_BlitTexture`, computes the
fogged color, and writes the final color directly.

## Scene Depth Normalization

The scene depth buffer is sampled with `SampleSceneDepth(screenUV)` and converted
to linear eye depth:

```hlsl
float rawDepth = SampleSceneDepth(screenUV);
float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
```

The fog system uses its own far plane, `_FogFarPlane`, rather than the camera far
clip. Depth is normalized into fog space with the camera near clip accounted for:

```hlsl
float fogRange = max(_FogFarPlane - _ProjectionParams.y, 1e-6);
float sceneDepth01 = saturate((eyeDepth - _ProjectionParams.y) / fogRange);
```

All fog interval endpoints are compared in this normalized fog depth space.

## Camera Ray Reconstruction

Two world-space rays are reconstructed:

1. The layer ray, used for fog/noise lookup.
2. The true camera ray, used for world-space ground falloff.

The base reconstruction is:

```hlsl
float2 ndc = screenUV * 2.0 - 1.0;
ndc.y = -ndc.y;
float4 viewClip = float4(ndc, 1.0, 1.0);
float4 viewSpace = mul(UNITY_MATRIX_I_P, viewClip);
float3 viewDir = normalize(viewSpace.xyz / max(viewSpace.w, 1e-6));
float3 worldDir = normalize(mul(UNITY_MATRIX_I_V, float4(viewDir, 0.0)).xyz);
```

For layer sampling, `ndc` is first scaled by the layer FOV scale, then the world
direction is rotated by the layer's compensation quaternion.

## Layer FOV Scale

Near fog layers should sample a smaller angular region than far layers, creating
a cheap approximation of depth scale.

```hlsl
float nearToFogFar = saturate(_ProjectionParams.y / max(_FogFarPlane, 1e-6));
float fovScale = lerp(nearToFogFar, 1.0, saturate(layerDepth01));
```

This scale is applied before inverse projection:

```hlsl
ndc *= fovScale;
```

`layerDepth01` is usually the midpoint of the visible band.

## Procedural Fog Field

The fog field is generated procedurally with value noise and FBM. No external
texture is required in the current version.

The low-level value noise hashes integer cell coordinates:

```hlsl
float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}
```

`ValueNoise3D` interpolates eight cell corners using smoothstep-style cubic
weights:

```hlsl
float3 u = f * f * (3.0 - 2.0 * f);
```

`Fbm` sums four octaves:

```hlsl
sum += amplitude * ValueNoise3D(p * frequency);
frequency *= 2.0;
amplitude *= 0.5;
```

Each fog endpoint samples a procedural depth field:

```hlsl
float3 seedDrift = _FogSeedOffset * float3(0.37, -0.61, 0.19);
float3 p = worldViewDir * layerFrequency + layerOffset + seedDrift;
float clouds = Fbm(p);
float detail = Fbm(p * 2.75 + 17.0);
float depth = saturate(clouds * 0.75 + detail * 0.25);
return saturate((depth - 0.32) * 2.78);
```

The remap `(depth - 0.32) * 2.78` pushes the FBM output toward the full `0..1`
range, which is important because the fog interval algorithm expects endpoint
depths that can meaningfully span the available range.

## Fog Interval Construction

For each visible layer, the shader computes a normalized depth band:

```hlsl
float layerScale = rcp((float)depthLayerCount);
int firstLayerIndex = -(int)floor(_PseudoDepth) - 1;
int layerIndex = firstLayerIndex + localLayerIndex;

float rawBandStart = (_PseudoDepth + (float)layerIndex) * layerScale;
float rawBandEnd = rawBandStart + layerScale;
float visibleBandStart = max(rawBandStart, 0.0);
float visibleBandEnd = min(rawBandEnd, 1.0);
```

Only layers with positive visible width are evaluated:

```hlsl
float visibleBandWidth = max(visibleBandEnd - visibleBandStart, 0.0);
if (visibleBandWidth > 1e-5)
{
    ...
}
```

Each layer creates one fog interval from two procedural reads:

```hlsl
float layer0Depth = rawBandStart + StartRead * layerScale;
float layer1Depth = rawBandStart + EndRead * layerScale;
```

The current implementation uses different read directions and offsets for the
start and end endpoints:

```hlsl
float layer = (float)(layerIndex * 2 + (isEndLayer ? 1 : 0));
float3 layerDirection = isEndLayer ? -worldViewDir : worldViewDir;
float3 layerOffset = float3(layer * 6.13, layer * -9.47, layer * 2.71);
```

If `layer1Depth` is not greater than `layer0Depth`, the span contributes zero
after clipping:

```hlsl
float bandFogAmount = max(clippedEndDepth - clippedStartDepth, 0.0);
```

This is the same as saying inverted intervals are naturally discarded.

## Scene-Depth Clipping

Each fog interval is clipped to normalized fog space and scene depth:

```hlsl
float clippedStartDepth = max(layer0Depth, 0.0);
float clippedEndDepth = min(min(layer1Depth, sceneDepth01), 1.0);
float bandFogAmount = max(clippedEndDepth - clippedStartDepth, 0.0);
```

This means:

- if the scene depth is before the fog start, the contribution is zero
- if the scene depth is inside the interval, only the near part contributes
- if the scene depth is beyond the interval, the full interval contributes
- if the interval is inverted, it contributes zero

All active layer contributions are summed:

```hlsl
fogAmount += bandFogAmount;
```

## Final Composition

The fog color alpha is used as an intensity multiplier:

```hlsl
float fogAlpha = saturate(fogAmount * (_FogColor.a * 4.0));
```

The RGB channels are used as the tint:

```hlsl
float3 fogColor = lerp(sceneColor, _FogColor.rgb, fogAlpha);
```

The pass writes opaque output:

```hlsl
return half4(finalColor, 1.0);
```

The color alpha is never used as the output pixel alpha.

## Debug View

Debug view uses the nearest contributing fog start depth as a grayscale shade:

```hlsl
float fogDebugShade = 1.0 - nearestFogStartDepth;
float3 debugFogColor = float3(fogDebugShade, fogDebugShade, fogDebugShade);
```

The debug result is blended by the attenuated fog amount:

```hlsl
float fogMask = saturate(fogAmount * 4.0);
float3 debugColor = lerp(sceneColor, debugFogColor, fogMask);
```

This makes debug mode useful for checking span placement and clipping.

## Synthetic Depth Phase

The controller tracks camera movement along the camera-facing depth plane.

On the first frame for a camera, tracking is initialized. On later frames:

```csharp
Vector3 depthPlaneNormal = Vector3.Cross(cameraTransform.right, cameraTransform.up);
depthPlaneNormal.Normalize();
Plane currentDepthPlane = new Plane(depthPlaneNormal, currentCameraPosition);
float cameraSpaceZDelta = currentDepthPlane.GetDistanceToPoint(lastCameraWorldPosition);
accumulatedCameraDepth += cameraSpaceZDelta;
```

This is intentionally a delta-based fake. It does not try to reconstruct a
stable global 3D position. A camera moving forward while turning slightly will
continue to produce forward depth deltas, even if it eventually loops back to
its original world position.

The accumulated world distance is converted to synthetic depth phase:

```csharp
float motionScale = Mathf.Max(depthLayerCount, 1) / fogFarPlane;
return accumulatedCameraDepth * motionScale;
```

One traversal of `fogFarPlane` advances the layer phase by roughly the number of
active depth layers.

## Layer Cycling

The shader evaluates `depthLayerCount + 2` visible layers.

The extra two layers act as bookends so that when the synthetic depth phase
crosses an integer threshold, one layer can leave the front of the normalized
range while another enters the back without a visible pop.

Layer selection is based on:

```hlsl
int firstLayerIndex = -(int)floor(_PseudoDepth) - 1;
```

The controller uses the same first-layer calculation to remap per-layer
quaternions when the layer window shifts:

```csharp
private static int GetFirstLayerIndex(float resolvedPseudoDepth)
{
    return -(int)Mathf.Floor(resolvedPseudoDepth) - 1;
}
```

When the first layer changes, existing quaternion states are copied into their
new local slots. New front or back entries inherit the nearest existing edge
state. This prevents orientation pops when layers recycle.

## Per-Layer View Compensation

The fog field should not behave like a flat screen overlay, but it also should
not over-parallax when the camera rotates. To balance this, each visible layer
maintains its own quaternion.

At initialization, each layer quaternion is set to the current camera rotation.

Each frame:

1. Layer slots are remapped if the synthetic depth window moved.
2. Camera translation is converted to a fake strafe rotation.
3. Camera rotation delta is applied proportionally by layer depth.
4. The layer quaternion is updated.
5. The shader receives a compensation quaternion relative to the current camera
   rotation.

The rotation scale for a layer is based on its visible normalized depth:

```csharp
float rawBandStart = (resolvedPseudoDepth + layerIndex) * layerScale;
float rawBandEnd = rawBandStart + layerScale;
float visibleBandStart = Mathf.Max(rawBandStart, 0.0f);
float visibleBandEnd = Mathf.Min(rawBandEnd, 1.0f);
return Mathf.Clamp01((visibleBandStart + visibleBandEnd) * 0.5f);
```

Near layers receive less of the camera rotation delta. Far layers receive more.

The shader applies the compensation with quaternion rotation:

```hlsl
return normalize(RotateByQuaternion(worldViewDir, _LayerRotationCompensations[localLayerIndex]));
```

## Camera Translation And Strafe Parallax

Camera translation is converted into a small angular rotation:

```csharp
float yawDegrees = Mathf.Atan2(cameraSpaceDelta.x, fogRange) * Mathf.Rad2Deg;
float pitchDegrees = -Mathf.Atan2(cameraSpaceDelta.y, fogRange) * Mathf.Rad2Deg;
Quaternion yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);
Quaternion pitch = Quaternion.AngleAxis(pitchDegrees, Vector3.right);
```

This is applied to the whole layer stack, making lateral camera movement feel
like parallax through a directional fog field.

## Convection

Convection is implemented without shader changes. The controller injects a
synthetic vertical translation into `cameraSpaceDelta` before the strafe
rotation is calculated:

```csharp
cameraSpaceDelta.y -= fogFarPlane * convection * ConvectionSpeedScale * Time.deltaTime;
```

`ConvectionSpeedScale` is currently `0.1`, so a convection value of `1` produces
synthetic vertical motion of about `10%` of the fog range per second.

This creates a rising steam or swamp mist effect by reusing the existing
translation/parallax system.

## Noise Animation

`Fog Animation Speed` updates a scalar seed offset:

```csharp
fogSeedOffset = Mathf.Repeat(
    fogSeedOffset + Time.deltaTime * fogAnimationSpeed * FogAnimationSpeedScale,
    1024.0f);
```

The shader applies this to the procedural noise position:

```hlsl
float3 seedDrift = _FogSeedOffset * float3(0.37, -0.61, 0.19);
```

This causes the procedural field to evolve over time. It is useful at low
values, but high values can show directional shearing.

## Ground Plane Falloff

The optional ground falloff attenuates fog based on reconstructed world height
above the `y=0` plane.

The falloff path is disabled when `_GroundPlaneFalloff <= 1e-5`.

When enabled, the shader reconstructs a world position for several depths across
the clipped fog interval. The reconstruction uses the true camera ray rather
than the layer-compensated fog lookup ray:

```hlsl
float3 cameraForwardWS = normalize(mul(UNITY_MATRIX_I_V, float4(0, 0, 1, 0)).xyz);
float eyeDepth = _ProjectionParams.y + saturate(fogDepth01) * fogRange;
float rayDistance = eyeDepth / max(abs(dot(worldViewDir, cameraForwardWS)), 1e-4);
float3 fogWorldPosition = _WorldSpaceCameraPos + worldViewDir * rayDistance;
```

The effective falloff height is varied with static world-space XZ noise:

```hlsl
float heightNoise = ValueNoise3D(float3(fogWorldPosition.xz * 0.075, 19.37));
float variedFalloff = _GroundPlaneFalloff * lerp(0.65, 1.35, heightNoise);
```

The attenuation is currently linear:

```hlsl
float height01 = saturate(max(fogWorldPosition.y, 0.0) / max(variedFalloff, 1e-5));
return saturate(1.0 - height01);
```

Five depth samples are averaged across each visible interval:

```hlsl
for (int i = 0; i < 5; i++)
{
    float sampleDepth01 = startDepth01 + depthRange * ((float)i * 0.25);
    attenuation += GroundPlaneAttenuation(worldViewDir, sampleDepth01);
}
return attenuation * 0.2;
```

This reduces, but does not entirely remove, cutoff-like artifacts. For subtle
ground mist values it is useful enough for demonstration.

## Required Shader Inputs

The material receives:

```text
_FogColor
_PseudoDepth
_DepthLayerCount
_FogFarPlane
_GroundPlaneFalloff
_FogSeedOffset
_DebugFog
_LayerRotationCompensations[]
```

URP/common pipeline inputs used by the shader include:

```text
_BlitTexture
_ZBufferParams
_ProjectionParams
UNITY_MATRIX_I_P
UNITY_MATRIX_I_V
_WorldSpaceCameraPos
```

## Rebuild Checklist

To recreate this effect:

1. Create a full-screen URP pass that can sample scene color and scene depth.
2. Reconstruct camera world rays from screen UV.
3. Linearize scene depth and normalize it into a configurable fog range.
4. Implement procedural value noise and FBM.
5. For each active layer plus two bookends, compute a normalized depth band.
6. Use two out-of-phase procedural reads to create a start and end depth.
7. Clip each interval against `0`, `1`, and normalized scene depth.
8. Add only positive interval lengths to `fogAmount`.
9. Multiply `fogAmount` by `FogColor.a * 4`.
10. Blend scene RGB toward `FogColor.rgb` by the resulting alpha.
11. Track camera depth deltas to advance synthetic depth phase.
12. Maintain per-layer quaternions and remap them when the layer window shifts.
13. Convert camera translation into a small angular parallax delta.
14. Optionally add seed animation, convection, and ground-plane falloff.

## Known Artifacts And Future Work

- Ground falloff can still read as a cutoff at strong values.
- Animated noise can show directional shearing.
- The motion model is synthetic and camera-relative, not a true world-space
  volume.
- There is no lighting model, shadow buffer, volumetric shadowing, or scattering.
- The fog field is procedural and directional rather than a true 3D density
  texture.

Possible next improvements:

- Add a proper world-space density volume or tiled 3D noise input.
- Add a low-cost shadow/darkening term.
- Expose falloff noise scale and variance if the current constants are not
  flexible enough.
- Add light-direction modulation for stronger art direction.
- Add temporal smoothing if layer transitions become visible in production.
