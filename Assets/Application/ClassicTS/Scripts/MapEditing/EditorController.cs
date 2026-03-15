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
		private ISelectable[] _selection = null;
		private ISelectable[] selection
		{
			get => _selection;
			set
			{
				Array.ForEach(_selection ?? Array.Empty<ISelectable>(), item => item.OnDeselect(this));
				Array.ForEach((_selection = value is { Length: 0 } ? null : value) ?? Array.Empty<ISelectable>(),item => item.OnSelect(this));
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
					UpdateSelection(originDelta);
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
					{
						EndTileDrag();
						SetMode(ControllerMode.SelectTile);
					}
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
		private void UpdateSelection(Vector3 originDelta)
		{
			if (Vector3.zero == originDelta) return;
			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				cell.origin += originDelta;
				cell.position += originDelta;
				cell.OnUpdate(this);
			}
		}

		private void UpdateSelectionAltitude(float value)
		{
			if (0f == value) return;
			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				cell.position.y = value;
				cell.OnUpdate(this);
			}
		}

		private bool StartTileDrag(bool combine = false) => SelectTile(beginWorld = currentWorld, combine);

		private void UpdateTileDrag()
		{
			var snappedDelta = iMap.GetVariantAt(beginWorld).HasNav ?
				Map.FullFloorVec(currentWorld) - Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(currentWorld) - Map.HalfFloorVec(beginWorld);

			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				var alt = cell.position.y;
				cell.position = cell.origin + snappedDelta;
				cell.position.y = alt;
				cell.OnUpdate(this);
			}
		}

		private void EndTileDrag()
		{
			var cells = selection?.OfType<Cell>() ?? Enumerable.Empty<Cell>();
			if (!cells.Any()) return;

			var gridPoints = cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x),Mathf.FloorToInt(c.position.z)));
			var extents = GeomUtils.GetBoundingRect(gridPoints, new RectInt(0, 0, iMap.Width, iMap.Height));

			if (!Map.ValidExtents(extents))
			{
				//reset selection to current map positions
				foreach (var cell in cells)
				{
					cell.position = cell.origin;
					cell.OnUpdate(this);
				}
				return;
			}

			iMap.ResizeMap(extents);//resize the map for the selection to apply - suppress cropping

			var copy = cells;
			ClearSelection();

			foreach (var cell in copy)
			{
				if (cell.position != cell.origin)
					iMap.RemoveTileAt(cell.origin);
			}

			foreach (var cell in copy)
			{
				if (cell.position == cell.origin) continue;
				iMap.UpdateTileAt(cell.position, cell.variant);
				cell.origin = cell.position;
			}

			//restore selection
			selection = copy.OfType<ISelectable>().ToArray();//restore selection before bounding map
			iMap.ResizeMap(iMap.ContentBounds());

			Array.ForEach(selection ?? Array.Empty<ISelectable>(), item => item.OnUpdate(this));
			//selection = selection?.ToArray();//restore selection state - required because we have been using 'copy'
		}

		private bool SelectTile(Vector3 worldPos, bool combine = false)
		{
			if (iMap.GetVariantAt(worldPos).IsDefaultEquivalent) return false;
			var index = iMap.VectorToIndex(worldPos);
			if (index == -1) return false;

			// Check if this position is already in the current selection
			var isAlreadySelected = selection?.Any(s => s is Cell c && iMap.VectorToIndex(c.origin) == index) == true;

			if (isAlreadySelected)
			{
				// Already selected → toggle behavior only when combine is true
				if (combine)
					selection = selection.Where(s => s is not Cell c || iMap.VectorToIndex(c.origin) != index).ToArray();
				return true;
			}

			// ───────────────────────────────────────────────
			// Not previously selected → normal selection logic
			// ───────────────────────────────────────────────

			if (!combine) ClearSelection();
			var newCell = new Cell(iMap, worldPos);
			selection = selection == null ? new[] { newCell } : selection.Append(newCell).ToArray();
			return true;
		}

		private void ClearSelection() => selection = null;

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
			var attSelection = selection.OfType<MapAttachment>().ToArray();
			if (attSelection?.Length >= 1 && attSelection[0].tile == cursorTile) return;
			foreach (var att in attSelection) 
				att.tile = cursorTile;
			if (selection?.Length == 1)
				selection[0].OnUpdate(this);
			iMap.RefreshAttachments(attSelection);
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