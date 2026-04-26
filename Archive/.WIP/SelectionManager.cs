// File: SelectionManager.cs
using MassiveHadronLtd;
using System;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	internal class SelectionManager
	{
		private readonly EditorController editor;
		private readonly IMapEdit map;

		private ISelectable[] _selection;

		public ISelectable[] Current => _selection ?? Array.Empty<ISelectable>();

		public bool HasSelection => Current.Length > 0;
		public bool IsMultiSelect => Current.Length > 1;

		public event Action OnSelectionChanged;

		public SelectionManager(EditorController editor, IMapEdit map)
		{
			this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
			this.map = map ?? throw new ArgumentNullException(nameof(map));
		}

		public void Clear()
		{
			SetSelection(null);
			map.ResizeMap(map.ContentBounds()); // ← your original behavior
		}

		public void Set(ISelectable[] newSelection)
		{
			SetSelection(newSelection);
		}

		public void ReplaceWith(ISelectable[] items) => SetSelection(items);

		public void Add(ISelectable item)
		{
			if (item == null) return;
			var cur = Current;
			if (cur.Contains(item)) return;
			SetSelection(cur.Append(item).ToArray());
		}

		public void Remove(ISelectable item)
		{
			if (item == null) return;
			var cur = Current;
			if (!cur.Contains(item)) return;
			SetSelection(cur.Where(x => x != item).ToArray());
		}

		public void SetAltitude(float altitude)
		{
			foreach (var cell in Current.OfType<Cell>())
			{
				cell.position.y = altitude;
				cell.Update(editor);
			}
			OnSelectionChanged?.Invoke();
		}

		public void MoveByDelta(Vector3 delta)
		{
			var cells = Current.OfType<Cell>().ToArray();
			if (cells.Length == 0) return;

			foreach (var cell in cells)
			{
				cell.position += delta;
				cell.origin += delta;
				cell.Update(editor);
			}

			// Optional: check validity & revert if invalid
			var gridPoints = cells.Select(c => new Vector2Int(
				Mathf.FloorToInt(c.position.x),
				Mathf.FloorToInt(c.position.z)));

			var extents = GeomUtils.GetBoundingRect(gridPoints, map.ContentBounds());
			if (!Map.ValidExtents(extents))
			{
				// revert
				foreach (var cell in cells) cell.Revert(editor);
			}

			OnSelectionChanged?.Invoke();
		}

		private void SetSelection(ISelectable[] value)
		{
			var old = _selection ?? Array.Empty<ISelectable>();
			var neu = value ?? Array.Empty<ISelectable>();

			// ─── Phase 1: leaving selection ───────────────────────
			foreach (var item in old.Except(neu))
			{
				if (item is Cell cell && cell.position != cell.origin)
				{
					map.RemoveTileAt(cell.origin);
					map.UpdateTileAt(cell.position, cell.variant);
				}
				item.Deselect(editor);
			}

			// ─── Phase 2: entering selection ──────────────────────
			foreach (var item in neu.Except(old))
				item.Select(editor);

			// ─── Phase 3: stayed selected ─────────────────────────
			foreach (var item in old.Intersect(neu))
				item.Update(editor);

			_selection = neu.Length == 0 ? null : neu;

			OnSelectionChanged?.Invoke();
		}
	}
}