using System;
using UnityEngine;

namespace ClassicTilestorm
{
	public readonly struct Tile : IDisposable
	{
		private readonly int flags;
		private readonly bool hasModel;
		public readonly GameObject gameObject;

		public Tile(Variant variant, Transform parent, Vector3 renderPosition)
		{
			var def = ResourceManager.ResolveDefinition(variant.hash, out bool hadError);
			if (hadError)
				Debug.LogWarning($"Failed to resolve tile definition at tile ({renderPosition.x:F1},{renderPosition.z:F1}) (hash: {variant.hash}) — using default");

			// Directly take flags from definition (no recompute needed)
			int baseFlags = ((IFlagAccess)def)?.Flags ?? 0;

			// Rotate navigation bits **before** final assignment (safe in readonly struct)
			int rotatedNav = Navigation.Rotate(
				baseFlags & (int)DefinitionFlags.DirMask,
				Mathf.RoundToInt(variant.angle)
			);

			flags = (baseFlags & ~(int)DefinitionFlags.DirMask) | rotatedNav;
			hasModel = !string.IsNullOrWhiteSpace(def?.model);

			Vector3 finalPosition = renderPosition + variant.delta;
			Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

			gameObject = def != null && !def.IsDefault()
				? InstantiateTile(def, parent, finalPosition, finalRotation)
				: null;

			static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position, Quaternion rotation)
			{
				if (string.IsNullOrEmpty(definition?.model))
				{
					//if (definition != null && definition.Fold)
					if (definition != null && definition.Fixed)
						return ApplicationSettings.ShowHiddenTiles
							? GeometryFactory.CreateDebugTile(parent, position, rotation)
							: null;

					Debug.LogWarning($"Invalid Definition or model for {definition?.name ?? "null"}");
					return GeometryFactory.CreateFallbackTile(parent, position, rotation);
				}

				return DefinitionFactory.Instantiate(definition, position, rotation, parent);
			}
		}

		//// Backward compatibility
		//public Tile(HashId hashId, Transform parent, Vector3 worldPosition) : this(new Variant(hashId), parent, worldPosition) { }

		// Forwarded properties
		public readonly bool IsStart => (flags & (int)DefinitionFlags.Start) != 0;
		public readonly bool IsEnd => (flags & (int)DefinitionFlags.End) != 0;
		public readonly bool IsConsole => (flags & (int)DefinitionFlags.Console) != 0;
		public readonly bool IsDrag => (flags & (int)DefinitionFlags.Fixed) == 0 && (flags & (int)DefinitionFlags.DirMask) != 0;
		public readonly bool IsFold => !hasModel && (flags & (int)DefinitionFlags.Fixed) == 0 && (flags & (int)DefinitionFlags.DirMask) == 0;
		public readonly bool IsRoll => hasModel && (flags & (int)DefinitionFlags.Fixed) == 0 && (flags & (int)DefinitionFlags.DirMask) == 0;
		public readonly int Nav => flags & (int)DefinitionFlags.DirMask;

		public Bounds GetGeometryBounds()
		{
			if (gameObject == null) return default;//return new Bounds(gameObject.transform.position, Vector3.zero);//// return default;

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
