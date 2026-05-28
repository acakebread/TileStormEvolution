# Screen Space Volumetric Fog Technical Notes

## Summary

This effect is a low-cost fog compositor implemented as a full-screen URP pass.
It approximates volumetric fog using layered procedural depth spans, scene-depth
clipping, and camera-relative directional projection. The reusable system code
lives in `Assets/System/ScreenSpaceVolumetricFog/`.

It is intended as a practical fake rather than a physically correct solution.
The design goal is to get a convincing sense of depth, motion, and density at
minimal cost.

## High-level concept

For each pixel, the shader reconstructs a world-space view direction from the
screen position. That direction is projected into a spherical lookup space and
used to sample a procedural noise field.

Rather than treating the fog as a single flat alpha overlay, the noise field is
interpreted as a set of depth intervals. Each interval contributes a span of
fog along the ray. Those spans are then clipped against the scene depth buffer
so opaque geometry can occlude or sit inside the fog field.

The final fog value is accumulated from the visible spans and composited over
the scene color.

## Pipeline

### 1. Scene inputs

- The resolved scene color is sampled from the color buffer.
- The resolved scene depth is sampled and linearized into normalized fog space.

### 2. View-ray reconstruction

- The current screen UV is converted into a view ray.
- The ray is projected into spherical direction space so the fog field behaves
  like a wrapped angular environment rather than a simple 2D overlay.

### 3. Layered fog evaluation

- The fog field is evaluated in several overlapping depth layers.
- Each layer uses a phase offset derived from the synthetic depth phase.
- Each layer also has a per-layer angular compensation quaternion so camera
  rotation does not cause unwanted parallax.

### 4. Scene-depth clipping

- The fog interval for each layer is clipped against normalized scene depth.
- Only the visible portion of the fog interval contributes to opacity.

### 5. Composition

- The visible fog contribution is accumulated.
- The result is multiplied by the fog opacity multiplier.
- The fog color is blended over the scene color.

## Why this approach is cheap

The system avoids the expensive parts of a true volumetric renderer:

- no raymarch loop through a 3D density volume
- no voxel grid
- no shadowed volumetric light integration
- no multiple scattering solve
- no large froxel buffer

Instead, it uses:

- one full-screen pass
- procedural noise
- depth-buffer clipping
- a small number of layered reads

That makes it far more practical for URP, WebGL, and mobile-oriented targets.

## Camera handling

The effect has two camera-related mechanisms:

### Rotation compensation

Each fog layer maintains a compensated orientation state. Camera rotation is
applied proportionally by layer depth so the nearer layers can respond more
slowly than the farther ones. This avoids the fog feeling glued to the screen.

### Translation phase

Camera movement contributes to a synthetic depth phase. This is not intended to
be mathematically exact world-space fog motion. It is a controlled fake that
creates the impression of the fog field moving through space as the camera
travels.

## Current controls

- `Fog Color`
- `Fog Far Plane`
- `Fog Multiplier`
- `Fog Animation Speed`
- `Depth Layer Count`
- `Debug Fog`

## Debug mode

Debug fog renders a grayscale view of fog presence instead of the final fog
color blend. This is useful for validating:

- where the fog spans are appearing
- whether the depth clipping is behaving correctly
- whether the layered span logic is stable

## Known artifacts

The current prototype is functional, but it still has a few known issues:

- animated noise can show directional shearing
- the spherical projection can expose seam-like behavior if pushed too hard
- the current motion model is intentionally synthetic, not physically accurate

These are acceptable for the current prototype because the primary goal is to
preserve performance and visual plausibility rather than physical correctness.

## Suggested terminology for handoff

To keep the design document consistent, use these terms:

- `synthetic depth phase` instead of `pseudo depth`
- `fog interval accumulator` instead of `span buffer`
- `noise phase offset` instead of `seed delta`
- `opacity multiplier` instead of `fog multiplier`
- `per-layer view compensation` instead of ad hoc rotation fixups

## Files

- [ScreenSpaceVolumetricFogTest.cs](./ScreenSpaceVolumetricFogTest.cs)
- [ScreenSpaceVolumetricFog.unity](./ScreenSpaceVolumetricFog.unity)
