using UnityEngine;

namespace ClassicTilestorm
{
	/// <summary>
	/// Runtime-safe extensions for MapAttachment (especially transformable ones like Emitter and View).
	/// Provides clean world-space Position and Rotation access.
	/// Automatically tracks the active IMapManager via MapManager lifecycle.
	/// </summary>
	public static class MapAttachmentExtensions
	{
		private static IMapManager _currentMapManager;

		public static IMapManager CurrentMapManager => _currentMapManager;

		// Called automatically by MapManager
		internal static void SetActiveMapManager(IMapManager mapManager) => _currentMapManager = mapManager;

		// Called automatically by MapManager
		internal static void ClearActiveMapManager() => _currentMapManager = null;

		// Internal duplicate of MapManager's TileWorldPosition logic
		private static Vector3 GetTileWorldPosition(int tileIndex)
		{
			if (_currentMapManager == null || tileIndex < 0)
				return Vector3.zero;

			int width = _currentMapManager.Width;
			int x = tileIndex % width;
			int z = tileIndex / width;

			// Match MapManager exactly — uses tile_origin (0.5,0,0.5 in editor, zero in build)
			return new Vector3(x, 0f, z) + MapManager.tile_origin;
		}

		// ===================================================================
		// World Position
		// ===================================================================
		public static Vector3 GetWorldPosition(this MapAttachment attachment)
		{
			if (attachment is not ITransformableAttachment t)
				return Vector3.zero;
			return GetTileWorldPosition(attachment.tile) + t.Position;
		}

		public static void SetWorldPosition(this MapAttachment attachment, Vector3 worldPosition)
		{
			if (attachment is not ITransformableAttachment t)
				return;

			Vector3 localPos = worldPosition - GetTileWorldPosition(attachment.tile);
			if (t is Emitter e)
				e.Position = localPos;
			else if (t is View v)
				v.Position = localPos;
			// Add future transformable types here
		}

		// ===================================================================
		// World Rotation
		// ===================================================================
		public static Quaternion GetWorldRotation(this MapAttachment attachment)
		{
			return attachment is ITransformableAttachment t ? t.Rotation : Quaternion.identity;
		}

		public static void SetWorldRotation(this MapAttachment attachment, Quaternion worldRotation)
		{
			if (attachment is not ITransformableAttachment t)
				return;

			if (t is Emitter e)
				e.Rotation = worldRotation;
			else if (t is View v)
				v.Rotation = worldRotation;
			// Add future types here
		}

		// Syntax sugar — prevents accidental use on the static class
		public static Vector3 WorldPosition
		{
			get => throw new System.NotSupportedException("Use on instance only");
			set => throw new System.NotSupportedException("Use on instance only");
		}

		public static Quaternion WorldRotation
		{
			get => throw new System.NotSupportedException("Use on instance only");
			set => throw new System.NotSupportedException("Use on instance only");
		}
	}
}