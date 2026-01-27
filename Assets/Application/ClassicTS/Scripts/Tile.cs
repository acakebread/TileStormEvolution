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

		private readonly int flags;
		private static readonly int navMask = North | South | East | West;

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

	//// Add inside TileData (or as extension method)
	//public static class DirectionFlags
	//{
	//	// Clockwise 90° rotation of direction bits: N→E, E→S, S→W, W→N
	//	public static int _Rotate90CW(this int flags)
	//	{
	//		//   N E S W        →     original bit positions
	//		//   0 2 1 3              after 90° CW
	//		return ((flags & TileData.North) << 2) |  // N → E (bit 0 → bit 2)
	//			   ((flags & TileData.East) >> 1) |  // E → S (bit 2 → bit 1)
	//			   ((flags & TileData.South) << 2) |  // S → W (bit 1 → bit 3? wait)
	//			   ((flags & TileData.West) >> 3);   // W → N (bit 3 → bit 0)

	//		// Better/more readable version using bit permutation:
	//		// N(1) → E(4), E(4) → S(2), S(2) → W(8), W(8) → N(1)
	//		int rotated = 0;
	//		if ((flags & TileData.North) != 0) rotated |= TileData.East;
	//		if ((flags & TileData.East) != 0) rotated |= TileData.South;
	//		if ((flags & TileData.South) != 0) rotated |= TileData.West;
	//		if ((flags & TileData.West) != 0) rotated |= TileData.North;
	//		return rotated;
	//	}

	//	public static int Rotate90CW(this int flags)
	//	{
	//		int result = 0;

	//		if ((flags & TileData.North) != 0) result |= TileData.East;   // 1 → 4
	//		if ((flags & TileData.East) != 0) result |= TileData.South;  // 4 → 2
	//		if ((flags & TileData.South) != 0) result |= TileData.West;   // 2 → 8
	//		if ((flags & TileData.West) != 0) result |= TileData.North;  // 8 → 1

	//		return result;
	//	}

	//	public static int Rotate(this int flags, int angleDeg)
	//	{
	//		// angleDeg expected to be 0,90,180,270 (or -90, etc.)
	//		int turns = ((angleDeg % 360 + 360) % 360) / 90; // 0,1,2,3
	//		int result = flags;
	//		for (int i = 0; i < turns; i++)
	//			result = result.Rotate90CW();
	//		return result;
	//	}
	//}
}
