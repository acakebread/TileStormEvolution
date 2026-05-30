# Screen Space Volumetric Fog

This folder contains a lightweight URP-compatible fake volumetric fog system.
It is designed for projects where true volumetric rendering is too expensive or
too platform-specific, especially WebGL and mobile-oriented URP builds.

The effect is implemented as a full-screen render pass. It does not raymarch a
real 3D volume. Instead, it builds a small set of procedural fog intervals in
normalized depth space, clips those intervals against the camera depth buffer,
and blends the visible result over the resolved scene color.

## Files

- `ScreenSpaceVolumetricFogSystem.cs`
- `ScreenSpaceVolumetricFog.shader`
- `README.md`
- `ScreenSpaceVolumetricFogTechnicalNotes.md`

The system test scene currently uses:

- `Assets/System/Tests/screenSpaceVolumetricFog/ScreenSpaceVolumetricFog.unity`
- `Assets/System/Tests/screenSpaceVolumetricFog/ScreenSpaceVolumetricFogTest.cs`

## What The Effect Does

For each screen pixel, the shader:

1. Reads the scene color.
2. Reads and linearizes the scene depth.
3. Reconstructs the camera ray for the pixel.
4. Builds several synthetic fog depth intervals along that ray.
5. Clips each fog interval against the scene depth.
6. Accumulates the visible interval lengths into a final fog amount.
7. Blends the fog color over the scene color.

The important trick is that the fog is represented by sparse depth intervals
rather than a dense volume. This keeps the pass cheap while still allowing
objects to appear in front of, inside, or behind the fog.

## Controls

### Render Pass Event

Controls where the full-screen pass is injected. The test scene currently uses
`AfterRenderingPostProcessing`.

### Fog Color

RGB is the final fog tint.

Alpha is used as the fog intensity control. It maps to the old multiplier range:

```text
opacity multiplier = FogColor.a * 4
```

Examples:

- alpha `0.25` gives multiplier `1`
- alpha `0.5` gives multiplier `2`
- alpha `1.0` gives multiplier `4`

The shader writes an opaque final pixel. The color alpha is not used as output
alpha.

### Fog Far Plane

Defines the far distance of the fog volume in world units. Scene depth is
normalized into the range between the fog near plane and this value.

The camera far clip can be farther than this. The fog math should be considered
to operate in its own fog range, not directly in camera clip range.

### Fog Near Plane

Defines the near distance used for fog-depth normalization when `Override Fog
Clipping` is enabled.

### Override Fog Clipping

When disabled, the shader uses the camera near clip for fog-depth normalization.
When enabled, the inspector `Fog Near Plane` and `Fog Far Plane` values are used
instead.

### Fog Animation Speed

Animates the procedural noise seed. At zero the noise field is static. Higher
values make the fog evolve over time.

The current animation is deliberately subtle. It is useful for making still fog
feel alive, but it can show directional shearing if pushed too far.

### Convection

Adds a synthetic upward flow by feeding a fake vertical translation into the
existing camera-translation/parallax system.

- `0` disables the feature.
- `-1` applies the current maximum downward convection speed.
- `1` applies the current maximum upward convection speed.

This is controller-side only. It does not add shader uniforms or shader work.

### Depth Layer Count

Controls the number of active fog bands. The shader supports up to `8` logical
layers, plus two bookend layers used for seamless cycling.

Higher values can reduce obvious banding and create a richer field, but they
increase shader work.

### Debug Fog

Displays a grayscale debug view of the fog contribution instead of the final
colored blend. This is useful when checking:

- depth sorting
- layer cycling
- scene-depth clipping
- motion/parallax behavior

## Integration

Add `ScreenSpaceVolumetricFogSystem` or a subclass to an active scene object.
The script implements `IDirectCommandProvider`, so it is picked up by the
project's direct command buffer render feature.

The system creates a runtime material from:

```text
Hidden/ScreenSpaceVolumetricFog
```

The pass requires:

- a URP camera
- a valid scene depth texture
- access to the current camera color buffer through the existing blit texture
  path

## Performance Shape

The system is cheap compared to true volumetric fog because it avoids:

- raymarching through a volume texture
- froxel buffers
- voxel grids
- volumetric lighting integration
- multiple scattering

The main cost comes from:

- one full-screen pass
- procedural value noise and FBM
- up to `DepthLayerCount + 2` visible layer iterations

## Known Limitations

- The effect is a visual fake, not physically correct volumetric rendering.
- Fog animation can show directional shearing at higher speeds.
- The current motion model is camera-relative and synthetic.
- It does not include volumetric shadows, light shafts, true density fields, or
  scattering.

See `ScreenSpaceVolumetricFogTechnicalNotes.md` for the full implementation
description.
