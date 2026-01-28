using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public readonly struct Tile
	{
		// ── New constructor: takes Variant instead of just HashId ──────────
		private readonly int flags;
		public readonly GameObject gameObject;

		public Tile(Variant variant, Transform parent, Vector3 worldPosition)
		{
			var def = ResourceManager.ResolveDefinition(variant.hash, out bool hadError);
			if (hadError)
				Debug.LogWarning($"Failed to resolve tile definition at tile ({worldPosition.x},{worldPosition.z}) (hash: {variant.hash}) — using default");

			flags = ((IFlagAccess)def).Flags;

			Vector3 finalPosition = worldPosition + new Vector3(0f, variant.delta, 0f);
			Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

			gameObject = def != null && !def.IsDefault()
				? InstantiateTile(def, parent, finalPosition, finalRotation)
				: null;

			// Apply rotation to navigation bits (done in-place on flags)
			int rotatedNav = Navigation.Rotate(Nav, Mathf.RoundToInt(variant.angle));
			flags = (flags & ~(int)DirectionFlags.Directions) | rotatedNav;

			static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position, Quaternion rotation)
			{
				if (string.IsNullOrEmpty(definition?.model))
				{
					if (definition != null && definition.bDock)
						return ApplicationSettings.ShowHiddenTiles
							? GeometryFactory.CreateDebugTile(parent, position, rotation)
							: null;

					Debug.LogWarning($"Invalid Definition or model for {definition?.name ?? "null"}");
					return GeometryFactory.CreateFallbackTile(parent, position, rotation);
				}

				return DefinitionFactory.Instantiate(definition, position, rotation, parent);  // ← already takes rotation
			}
		}

		// ── Backward compatibility: keep old constructor ───────────────────
		public Tile(HashId hashId, Transform parent, Vector3 worldPosition)
			: this(new Variant(hashId), parent, worldPosition)  // ← delegate to new one
		{
		}

		// Forwarded properties — now directly on our own flags
		public readonly bool IsStart => (flags & (int)DefinitionFlags.Start) != 0;
		public readonly bool IsEnd => (flags & (int)DefinitionFlags.End) != 0;
		public readonly bool IsConsole => (flags & (int)DefinitionFlags.Console) != 0;
		public readonly bool IsDrag => (flags & (int)DefinitionFlags.Drag) != 0;
		public readonly bool IsDock => (flags & (int)DefinitionFlags.Dock) != 0;
		public readonly bool IsRoll => (flags & (int)DefinitionFlags.Roll) != 0;
		public readonly int Nav => flags & (int)DirectionFlags.Directions;

		public Bounds GetGeometryBounds()
		{
			// No geometry → no bounds
			if (gameObject == null)
				return default;

			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);

			bool hasBounds = false;
			Bounds combined = default;

			foreach (var r in renderers)
			{
				// Ignore disabled renderers or inactive objects
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

		public void Destroy()
		{
			if (gameObject != null)
				Object.DestroyImmediate(gameObject);
		}
	}
}
