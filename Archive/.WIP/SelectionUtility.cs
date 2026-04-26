using System;
using System.Linq;

namespace ClassicTilestorm
{
    public static class SelectionUtility
    {
		private static EditorController editorController;
		private static IMapEdit iMap => editorController.iMap;

		public static bool IsMultiSelect => selection?.Length > 1;
		public static bool HasSelection => selection?.Length > 0;

		private static ISelectable[] _selection = null;
		private static ISelectable[] selection
		{
			get => _selection;

			set
			{
				var oldItems = _selection ?? Array.Empty<ISelectable>();
				var newItems = value ?? Array.Empty<ISelectable>();

				// 1. Deselect items that are no longer wanted
				foreach (var item in oldItems.Except(newItems))
				{
					if (item is Cell cell)
					{
						if (cell.position != cell.origin)
						{
							iMap.RemoveTileAt(cell.origin);
							iMap.UpdateTileAt(cell.position, cell.variant);
						}
					}
					item.Deselect(editorController);
				}

				// 2. Select newly added items
				foreach (var item in newItems.Except(oldItems))
					item.Select(editorController);

				// Preserve your original null-when-empty convention
				_selection = newItems.Length == 0 ? null : newItems;

				// 3. Update items that were already selected and still are
				foreach (var item in oldItems.Intersect(newItems))
					item.Update(editorController);
			}
		}
	}
}