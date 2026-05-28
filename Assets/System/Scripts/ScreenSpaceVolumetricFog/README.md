# Screen Space Volumetric Fog

This folder contains the test harness for a lightweight, URP-friendly fake
volumetric fog prototype. The reusable system lives under `Assets/System`.

## What it is

This effect is not a true volumetric renderer. It is a screen-space compositor
that:

1. Reconstructs a view ray for each pixel.
2. Projects that ray into a spherical direction lookup.
3. Samples procedural fog noise in several layered depth intervals.
4. Clips those intervals against the scene depth buffer.
5. Accumulates the visible portion into a fog opacity value.
6. Blends the fog over the resolved scene color.

The result is a deliberately fake but very cheap approximation of volumetric fog.

## Why it exists

Traditional volumetric fog approaches often rely on raymarching, 3D volumes,
shadow integration, or HDRP features. Those are usually too expensive or too
heavy for this project's intended URP/WebGL/mobile use case.

This prototype focuses on:

- low runtime cost
- easy drop-in integration
- stable screen-space rendering
- scene-depth awareness
- camera-relative motion

## Main ideas

- The fog is built from layered depth spans rather than a continuous volume.
- The fog noise is projected through a spherical direction map so it behaves
  more like a world-space field than a flat overlay.
- Camera rotation is compensated per layer so the fog does not just stick to
  the screen.
- Camera translation feeds a synthetic depth phase so the field appears to move
  through space.
- An animation seed offset can be enabled to give the fog a slow living drift.

## Current controls

- `Fog Color`
- `Fog Far Plane`
- `Fog Multiplier`
- `Fog Animation Speed`
- `Depth Layer Count`
- `Debug Fog`

## Terminology

The code still uses some prototype-style names internally, but conceptually:

- `pseudo depth` means `synthetic depth phase`
- `span buffer` means `fog interval accumulator`
- `fog multiplier` means `opacity multiplier`
- `fog seed offset` means `noise phase offset`

## Current status

The system is intentionally still a prototype. It is good enough to demonstrate
the core technique and to hand off for further development, but it still has
some known artifacts such as directional shearing in the animated noise field.

## Related files

- [ScreenSpaceVolumetricFogTest.cs](./ScreenSpaceVolumetricFogTest.cs)
- [ScreenSpaceVolumetricFog.unity](./ScreenSpaceVolumetricFog.unity)
