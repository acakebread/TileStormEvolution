using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour, IEditorScreenUI
	{
		public IMapEdit iMap;
		public Camera _camera => GetComponent<MainCameraController>()?.activeSystem?.camera;

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition, editAltitude);
		private Vector3 lastSnap = Vector3.zero;

		private ISelectable[] _selection = null;
		private ISelectable[] selection
		{
			get => _selection;

			set
			{
				var oldItems = _selection ?? Array.Empty<ISelectable>();
				var newItems = value ?? Array.Empty<ISelectable>();

				// 1. Deselect items that are no longer wanted
				var leaving = oldItems.Except(newItems).ToList();

				// Pass 1: only leaving cells → cleanup + deselect
				foreach (var iter in leaving)
				{
					if (iter is Cell cell && cell.position != cell.origin)
						iMap.RemoveTileAt(cell.origin);
					iter.Deselect(this);
				}

				// Pass 2: all cells that should now be visible
				foreach (var iter in leaving)
				{
					if (iter is Cell cell && cell.position != cell.origin)
						iMap.UpdateTileAt(cell.position, cell.variant);
				}

				// Preserve original null-when-empty convention
				_selection = newItems.Length == 0 ? null : newItems;

				// 2. Select newly added items
				foreach (var iter in newItems.Except(oldItems))
					iter.Select(this);

				// 3. Update items that were already selected and still are (to activate or deactivate gizmos)
				foreach (var iter in oldItems.Intersect(newItems))
					iter.Update(this);
			}
		}

		public bool IsMultiSelect => selection?.Length > 1;
		public bool HasSelection => selection?.Length > 0;

		private float editAltitude = 0f;
		private Variant atlasVariant = default;

		// ─── Tile / Attachment state ─────────────────────────────────────────
		private enum ControllerMode
		{
			Idle,
			Evaluate,
			PlacingTile,
			SelectTile,
			DragTile,
			SelectAttachment,
			DragAttachment
		}

		private ISelectable[] GetAttachmentsAsSelectables(int? tileIndex = null, Type[] filterTypes = null)
			=> iMap.GetAttachments(tileIndex, filterTypes).Cast<ISelectable>().ToArray();

		private ControllerMode mode = ControllerMode.Idle;
		private void SetMode(ControllerMode value) => mode = value;

		public bool CanOpenPalette() => mode == ControllerMode.Idle;
		public void OnTileSelected(HashId newHash)
		{
			atlasVariant = new Variant(newHash);
			SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle);
		}

		public void OnAltitudeChanged(float value)
		{
			editAltitude = value;
			UpdateSelectionAltitude(value);
			GridLinesUtil.Update(transform, iMap?.Width ?? 32, iMap?.Height ?? 32, null != iMap ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, editAltitude, -0.5f) : Vector3.zero);
			GridLinesUtil.Show();
		}

		// ─── Unity / lifecycle ───────────────────────────────────────────────
		public void Awake()
		{
			//GridLinesUtil.Initialise(transform, Vector3.zero);//not needed for now but plan to refactor GridLinesUtil
			TryRegisterEditorScreenUI(UIController.Instance?.editorScreenUI?.GetComponent<EditorScreenUI>());
			UIController.OnEditorScreenUIReady += TryRegisterEditorScreenUI;
			void TryRegisterEditorScreenUI(EditorScreenUI editorScreenUI) => editorScreenUI?.Register(this);
		}

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += (Map map, bool resized, Vector3 originDelta) => 
			{
				ResourceManager.ApplyMapChanges(map);
				if (resized)
				{
					GridLinesUtil.UpdateSize(map.width, map.height);
					AdjustSelectionOrigin(originDelta);
				}
			};

			GridLinesUtil.Update(transform, iMap?.Width ?? 32, iMap?.Height ?? 32, null != iMap ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, editAltitude, -0.5f) : Vector3.zero);
			if (!isActiveAndEnabled) return;
			GridLinesUtil.Show();
			ClearSelection();
			EditorAttachmentUI.ClearPending();
			EditorMarkerUtil.ClearMapMarkers();
			SetMode(ControllerMode.Idle);
		}

		private void OnEnable()
		{
			var mainCameraController = GetComponent<MainCameraController>();
			if (null != mainCameraController)
			{
				mainCameraController.SetCameraSystem(CameraModeRegistry.Editor, false);
				mainCameraController.UpdateGestureControllerState();
			}

			if (null != UIController.Instance?.editorScreenUI) UIController.Instance.editorScreenUI.SetActive(true);
			GridLinesUtil.Show();
			SetMode(ControllerMode.Idle);
		}

		private void OnDisable()
		{
			if (null != UIController.Instance?.editorScreenUI) UIController.Instance.editorScreenUI.SetActive(false);
			ClearSelection();
			GridLinesUtil.Hide();
			EditorAttachmentUI.ClearPending();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private void Update()
		{
			if (!_camera) return;

			var mouseOverGUI = (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
				|| GUIUtility.hotControl != 0 || PlaceholderUI.IsMouseOverGui() || EditorAttachmentUI.sidePanel.IsMouseOver;// || ViewPreviewUtil.IsMouseOverPreview();

			ViewPreviewUtil.Update();
			EditorCameraMovement.UpdateCamera(ViewPreviewUtil.IsInFocus ? ViewPreviewUtil.PreviewCamera : _camera, currentWorld, inFocus: !mouseOverGUI);
			if (!ViewPreviewUtil.IsInFocus && mouseOverGUI) return;
			if (selection?.Length == 1 && selection[0].OnGizmoInput(this)) return;
			if (ViewPreviewUtil.IsInFocus) return;

			switch (mode)
			{
				case ControllerMode.Idle:
					if (InputX.GetMouseButtonDown(0))
						SetMode(ControllerMode.Evaluate);
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
							iMap.InsertTileAt(currentWorld, new Variant(ResourceManager.DefaultHash));
					}
					break;

				case ControllerMode.Evaluate:
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							EvaluateAttachments();
							SetMode(ControllerMode.SelectAttachment);
						}
						if (InputX.GetMouseButtonHeld(0))
						{
							if (StartTileDrag())
								SetMode(ControllerMode.DragTile);
							else
							{
								EditorCameraMovement.StartPanning(currentWorld);
								SetMode(ControllerMode.Idle);
							}
						}
					}
					else
					{
						if (InputX.GetMouseButton(0))
						{
							EditorCameraMovement.StartPanning(currentWorld);
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.PlacingTile:
					var variant = MapUtils.NextVariantOnMap(iMap, currentWorld, atlasVariant);
					if (InputX.staticClick && InputX.GetMouseButtonUp(0))
						iMap.InsertTileAt(Map.FullFloorVec(currentWorld), variant);
					GhostMeshUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), variant, false);

					if (InputX.staticClick && InputX.GetMouseButtonUp(1))
					{
						GhostMeshUtil.HideGhostMesh();
						SetMode(ControllerMode.Idle);
					}
					break;

				case ControllerMode.SelectTile:
					if (InputX.GetMouseButtonDown(0))
					{
						if (StartTileDrag(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
							SetMode(ControllerMode.DragTile);
						else
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
						{
							ClearSelection();
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.DragTile:
					if (InputX.GetMouseButton(0))
						UpdateTileDrag();
					if (InputX.GetMouseButtonUp(0))
						SetMode(ControllerMode.SelectTile);
					break;

				case ControllerMode.SelectAttachment:
					if (InputX.GetMouseButtonDown(0))
					{
						EditorAttachmentUI.ClearPending();
						if (StartAttachmentDrag())
							SetMode(ControllerMode.DragAttachment);
						else
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
						{
							if (CancelAttachmentMode())
								SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.DragAttachment:
					if (InputX.GetMouseButton(0))
						UpdateAttachmentDrag();
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							EvaluateAttachments();
							SetMode(ControllerMode.SelectAttachment);
						}
					}
					else
					{
						if (InputX.GetMouseButtonUp(0))
							SetMode(ControllerMode.SelectAttachment);
					}
					break;
			}
		}

		private void OnGUI()
		{
			ViewPreviewUtil.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, iMap.VectorToIndex(beginWorld), selectable => SelectAttachments(selectable));
		}

		// ─── All helper methods ──────────────────────────────────────────────

		private void ClearSelection() { selection = null; iMap.ResizeMap(iMap.ContentBounds()); }

		private void AdjustSelectionOrigin(Vector3 delta)
		{
			if (Vector3.zero == delta) return;
			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				cell.origin += delta;
				cell.Update(this);
			}
		}

		private void UpdateSelectionAltitude(float value)
		{
			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				cell.position = new Vector3(cell.position.x, value, cell.position.z);
				cell.Update(this);
			}
		}

		private bool StartTileDrag(bool combine = false) 
		{ 
			lastSnap = Vector3.zero;
			beginWorld = currentWorld;

			var index = iMap.VectorToIndex(currentWorld);
			if (index == -1) return false;
			var isAlreadySelected = selection?.Any(s => s is Cell c && iMap.VectorToIndex(c.position) == index) == true;

			if (isAlreadySelected)
			{
				// Already selected → toggle behavior only when combine is true
				if (combine)
				{
					selection = selection.Where(s => s is not Cell c || iMap.VectorToIndex(c.position) != index).ToArray();
					return false;
				}
				return true;
			}

			var originalMesh = iMap.GetTile(currentWorld).gameObject;//already in selection and disabled
			if (null != originalMesh && false == originalMesh.activeSelf && !combine) return false;

			if (iMap.GetVariantAt(currentWorld).IsDefaultEquivalent) return false;

			// ───────────────────────────────────────────────
			// Not previously selected → normal selection logic
			// ───────────────────────────────────────────────

			if (!combine) ClearSelection();
			var newCell = new Cell(iMap, currentWorld);
			selection = selection == null ? new[] { newCell } : selection.Append(newCell).ToArray();
			return true;
		}

		private void UpdateTileDrag()
		{
			var cells = selection?.OfType<Cell>() ?? Enumerable.Empty<Cell>();
			if (!cells.Any()) return;

			var snappedDelta = selection?.Any(s => s is Cell c && c.variant.HasNav) == true ?
				Map.FullFloorVec(currentWorld) - Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(currentWorld) - Map.HalfFloorVec(beginWorld);

			if (Vector3LexComparer.ApproximatelyEqual(snappedDelta, lastSnap))
				return;

			var delta = snappedDelta - lastSnap;
			lastSnap = snappedDelta;

			var oldGridPoints = cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x), Mathf.FloorToInt(c.position.z)));
			var oldExtents = GeomUtils.GetBoundingRect(oldGridPoints, iMap.ContentBounds());

			foreach (var cell in cells)
			{
				cell.position += delta;
				cell.Update(this);
			}

			var gridPoints = cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x), Mathf.FloorToInt(c.position.z)));
			var extents = GeomUtils.GetBoundingRect(gridPoints, iMap.ContentBounds());
			if (Map.ValidExtents(extents))
			{
				iMap.ResizeMap(extents);//resize the map for the selection to apply
				lastSnap -= new Vector3(extents.x, 0f, extents.y);
				if(extents != oldExtents)
					foreach (var cell in cells) cell.Update(this);
			}
			else
				foreach (var cell in cells) cell.Revert(this);//reset selection to current map positions
		}

		private void EvaluateAttachments()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			var attachmentsOnTile = GetAttachmentsAsSelectables(tileIndex: cursorTile);
			if (EditorAttachmentUI.EvaluateSelection(attachmentsOnTile, cursorTile))
				SelectAttachments(attachmentsOnTile);
			MapUtils.RebuildMarkers(iMap, selection);
		}

		private bool StartAttachmentDrag()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachments(GetAttachmentsAsSelectables(tileIndex: cursorTile));
			return selection?.Length > 0;
		}

		private void UpdateAttachmentDrag()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			if (-1 == cursorTile) return;

			if (selection?.Length >= 1 && selection[0] is MapAttachment first && first.tile == cursorTile) return;
			foreach (var iter in selection ?? Array.Empty<ISelectable>())
			{
				if (iter is MapAttachment att) att.tile = cursorTile;
				iter.Update(this);
			}

			iMap.RefreshAttachments(selection.Cast<MapAttachment>().ToArray());
			MapUtils.RebuildMarkers(iMap, selection);
		}

		private bool CancelAttachmentMode()
		{
			if (selection?.Length > 0)
			{
				selection = null;
				MapUtils.RebuildMarkers(iMap, selection);
				return false;
			}
			SelectAttachments(GetAttachmentsAsSelectables(tileIndex: iMap.VectorToIndex(beginWorld = currentWorld)));
			if (selection?.Length > 0)
			{
				EditorAttachmentUI.RequestDelete();
				return false;
			}
			EditorMarkerUtil.ClearMapMarkers();
			return true;
		}

		private void SelectAttachments(ISelectable[] value = null)
		{
			selection = value;
			MapUtils.RebuildMarkers(iMap, selection);
		}
	}
}