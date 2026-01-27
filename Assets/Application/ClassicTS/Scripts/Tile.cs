using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public struct TileData
	{
		public const int North = 1 << 0;   // 0b00000000001
		public const int South = 1 << 1;   // 0b00000000010
		public const int East = 1 << 2;    // 0b00000000100
		public const int West = 1 << 3;    // 0b00000001000
		public const int Drag = 1 << 4;    // 0b00000010000
		public const int Roll = 1 << 5;    // 0b00000100000
		public const int Dock = 1 << 6;    // 0b00001000000
		public const int Start = 1 << 7;   // 0b00010000000
		public const int End = 1 << 8;     // 0b00100000000
		public const int Door = 1 << 9;    // 0b01000000000
		public const int Console = 1 << 10;// 0b10000000000
		public static readonly int navMask = North | South | East | West;

		private readonly int flags;

		public TileData(Definition def)
		{
			flags = def == null ? 0 : CombineFlags(def);

			static int CombineFlags(Definition d)
			{
				int f = 0;
				if (d.bNorth) f |= North;
				if (d.bSouth) f |= South;
				if (d.bEast) f |= East;
				if (d.bWest) f |= West;
				if (d.bDrag) f |= Drag;
				if (d.bRoll) f |= Roll;
				if (d.bDock) f |= Dock;
				if (d.bStart) f |= Start;
				if (d.bEnd) f |= End;
				if (d.bDoor) f |= Door;
				if (d.bConsole) f |= Console;
				return f;
			}
		}

		public readonly bool IsStart => (flags & Start) != 0;
		public readonly bool IsEnd => (flags & End) != 0;
		public readonly bool IsConsole => (flags & Console) != 0;
		public readonly bool IsDrag => (flags & Drag) != 0;
		public readonly bool IsDock => (flags & Dock) != 0;
		public readonly bool IsRoll => (flags & Roll) != 0;
		public readonly int Nav => flags & navMask;
	}

	public readonly struct Tile
	{
		private readonly TileData _data;
		public readonly GameObject gameObject;
		private readonly int _rotatedNav;// ← new cached field

		// ── New constructor: takes Variant instead of just HashId ──────────
		public Tile(Variant variant, Transform parent, Vector3 worldPosition)
		{
			var def = ResourceManager.ResolveDefinition(variant.hash, out bool hadError);
			if (hadError)
				Debug.LogWarning($"Failed to resolve tile definition at tile ({worldPosition.x},{worldPosition.z}) (hash: {variant.hash}) — using default");

			_data = new TileData(def);

			// Position with delta offset
			Vector3 finalPosition = worldPosition + new Vector3(0f, variant.delta, 0f);

			// Rotation from angle (around Y-axis for top-down map)
			Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

			gameObject = def != null && !def.IsDefault()
				? InstantiateTile(def, parent, finalPosition, finalRotation)
				: null;

			_rotatedNav = Navigation.Rotate(_data.Nav, Mathf.RoundToInt(variant.angle));

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
		public bool IsStart => _data.IsStart;
		public bool IsEnd => _data.IsEnd;
		public bool IsConsole => _data.IsConsole;
		public bool IsDrag => _data.IsDrag;
		public bool IsDock => _data.IsDock;
		public bool IsRoll => _data.IsRoll;
		//public int Nav => _data.Nav;
		public readonly int Nav => _rotatedNav;

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
