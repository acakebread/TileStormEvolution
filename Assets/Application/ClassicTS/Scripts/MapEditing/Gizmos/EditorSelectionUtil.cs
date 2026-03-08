using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorSelectionUtil
	{
		private static (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		public static bool HighlightTile(IMapEdit map, Vector3 worldPos)
		{
			var variant = map.GetVariantAt(worldPos);
			if (variant.IsDefaultEquivalent)
				return false;

			return HighlightGameObject(map.GetTile(worldPos).gameObject);
		}

		public static void UnhighlightTile(IMapEdit map, Vector3 worldPos)
		{
			UnhighlightGameObject(map.GetTile(worldPos).gameObject);
			originalRenderersState = null;
		}

		public static bool HighlightGameObject(GameObject gameObject)
		{
			if (gameObject == null) return false;

			Color SELECT_TINT = new(1.4f, 1.25f, 0.85f, 1f);
			const float SELECT_TINT_BRIGHTNESS = 1.35f;

			originalRenderersState = gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);

			return true;
		}

		public static void UnhighlightGameObject(GameObject gameObject)
		{
			if (null != gameObject)
				gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
		}
	}
}