using System;
using UnityEngine;

namespace ClassicTilestorm
{
	public readonly struct Tile : IDisposable
	{
		private readonly Definition definition;
		private readonly int flags;
		public readonly GameObject gameObject;

		public Tile(Variant variant, Transform parent, Vector3 renderPosition)
		{
			var def = ResourceManager.ResolveDefinition(variant.hash, out bool hadError);
			definition = def;
			if (hadError)
				Debug.LogWarning($"Failed to resolve tile definition at tile ({renderPosition.x:F1},{renderPosition.z:F1}) (hash: {variant.hash}) — using default");

			// Carry the full definition bitfield through unchanged and only rotate Nav.
			// The other bits are stable, so Tile can forward them directly from Definition.
			int baseFlags = ((IFlagAccess)def)?.Flags ?? 0;

			// Rotate navigation bits **before** final assignment (safe in readonly struct)
			int rotatedNav = Navigation.Rotate(
				baseFlags & (int)DefinitionFlags.DirMask,
				Mathf.RoundToInt(variant.angle)
			);

			flags = (baseFlags & ~(int)DefinitionFlags.DirMask) | rotatedNav;

			Vector3 finalPosition = renderPosition + variant.delta;
			Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

			gameObject = InstantiateTile(def, parent, finalPosition, finalRotation);

			static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position, Quaternion rotation)
			{
				if (string.IsNullOrEmpty(definition?.model))
				{
					return ApplicationSettings.ShowHiddenTiles
						? GeometryFactory.CreateDebugTile(parent, position, rotation)
						: null;
				}

				var result = DefinitionFactory.Instantiate(definition, position, rotation, parent);
				if (null == result)
				{
					Debug.LogWarning($"Invalid Definition or model for {definition?.name ?? "null"}");
					result = GeometryFactory.CreateFallbackTile(parent, position, rotation);
				}

				return result;
			}
		}

		// Forwarded properties. Only Nav is tile-local because the direction bits can rotate.
		public readonly bool IsStart => false;
		public readonly bool IsEnd => false;
		public readonly bool IsConsole => definition?.Unit ?? false;
		public readonly bool IsBake => definition?.Bake ?? false;
		public readonly bool IsDrag => definition?.Drag ?? false;
		public readonly bool IsRoll => definition?.Roll ?? false;
		public readonly bool IsFold => definition?.Fold ?? false;
		public readonly int Nav => flags & (int)DefinitionFlags.DirMask;

		public Bounds GetGeometryBounds()
		{
			if (gameObject == null) return default;

			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);

			bool hasBounds = false;
			Bounds combined = default;

			foreach (var r in renderers)
			{
				if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
					continue;

				if (!hasBounds)
				{
					combined = r.bounds;
					hasBounds = true;
				}
				else
				{
					combined.Encapsulate(r.bounds);
				}
			}

			return combined;
		}

		public void Dispose()
		{
			if (gameObject == null) return;

			if (Application.isPlaying)
				UnityEngine.Object.Destroy(gameObject);
			else
				UnityEngine.Object.DestroyImmediate(gameObject);
		}
	}
}
