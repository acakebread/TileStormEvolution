using UnityEngine;

namespace ClassicTilestorm
{
    internal class TileSelectableHandler : IEditorAttachmentHandler
    {
		public static readonly TileSelectableHandler Instance = new();

		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			var tile = (Tile)selection;
			if (null != tile.gameObject) tile.gameObject.SetActive(false);
		}

		public void OnDeselect(ISelectable selection)
		{
			var tile = (Tile)selection;
			if (null != tile.gameObject) tile.gameObject.SetActive(true);
		}

		public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
		{
			return false;
		}
	}
}