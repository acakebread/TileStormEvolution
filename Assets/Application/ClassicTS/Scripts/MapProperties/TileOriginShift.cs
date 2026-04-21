using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class TileOriginShift
	{
		private static readonly Vector3 HALF_TILE = new (0.5f, 0f, 0.5f);

#if UNITY_EDITOR
		// Shift applied in Editor (including Play Mode) for nice grid alignment
		private static readonly Vector3 OFFSET = HALF_TILE;
#else
        // No shift in built players
        private static readonly Vector3 OFFSET = Vector3.zero;
#endif

		// ─────────────────────────────────────────────
		// Internal core mapping functions
		// ─────────────────────────────────────────────
		private static Vector3 RenderOriginShift => OFFSET - HALF_TILE;

		// ─────────────────────────────────────────────
		// Public API
		// ─────────────────────────────────────────────

		/// <summary>
		/// Converts a world position to the rendered tile position.
		/// </summary>
		public static Vector3 WorldToRender(Vector3 worldPos) => worldPos + OFFSET;

		/// <summary>
		/// Converts a rendered tile position back to world position.
		/// </summary>
		public static Vector3 RenderToWorld(Vector3 renderPos) => renderPos - OFFSET;

		/// <summary>
		/// Adjusts a raw raycast intersection point to the map's tile coordinate system.
		/// (This was the old `(result - ORIGIN)` logic)
		/// </summary>
		public static Vector3 AdjustRaycastResult(Vector3 rawPoint) => (rawPoint - RenderOriginShift).Rounded(2);

		/// <summary>
		/// Applies the tile origin shift to any visual element (grid lines, markers, etc.).
		/// Use this instead of manually adding ORIGIN.
		/// Example: TileOriginShift.AdjustVisualOffset(Vector3.up * altitude)
		/// </summary>
		public static Vector3 AdjustVisualOffset(Vector3 visualOffset) => RenderOriginShift + visualOffset;
	}
}