using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public readonly struct Tile
	{
		private readonly DefinitionData data;
		public readonly GameObject gameObject;

		// ── New constructor: takes Variant instead of just HashId ──────────
		public Tile(Variant variant, Transform parent, Vector3 worldPosition)
		{
			var def = ResourceManager.ResolveDefinition(variant.hash, out bool hadError);
			if (hadError)
				Debug.LogWarning($"Failed to resolve tile definition at tile ({worldPosition.x},{worldPosition.z}) (hash: {variant.hash}) — using default");

			data = new DefinitionData(def);

			// Position with delta offset
			Vector3 finalPosition = worldPosition + new Vector3(0f, variant.delta, 0f);

			// Rotation from angle (around Y-axis for top-down map)
			Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

			gameObject = def != null && !def.IsDefault()
				? InstantiateTile(def, parent, finalPosition, finalRotation)
				: null;

			data.Nav = Navigation.Rotate(data.Nav, Mathf.RoundToInt(variant.angle));

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

		// Forwarded properties
		public bool IsStart => data.IsStart;
		public bool IsEnd => data.IsEnd;
		public bool IsConsole => data.IsConsole;
		public bool IsDrag => data.IsDrag;
		public bool IsDock => data.IsDock;
		public bool IsRoll => data.IsRoll;
		public readonly int Nav => data.Nav;

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
