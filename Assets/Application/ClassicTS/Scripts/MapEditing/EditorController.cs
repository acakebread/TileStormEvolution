using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour, IEditorScreenUI
	{
		private IMapEdit iMap;
		private Camera _camera => GetComponent<MainCameraController>()?.activeSystem?.camera;

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition, editAltitude);
		private ISelectable[] _selection = null;
		private ISelectable[] selection
		{
			get => _selection;
			set { Array.ForEach(_selection ?? Array.Empty<ISelectable>(), item => item.OnDeselect(iMap, _camera)); Array.ForEach(value ?? Array.Empty<ISelectable>(), item => item.OnSelect(iMap, _camera)); _selection = value; }
		}
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
					
					if (originDelta != Vector3.zero)
					{
						foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
						{
							cell.origin += originDelta;
							cell.position += originDelta;
						}
					}
				}

				//selection = selection?.ToArray();//restore selection state after map change
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
			if (selection?.Length == 1 && selection[0].OnGizmoInput(iMap, _camera)) return;
			if (ViewPreviewUtil.IsInFocus) return;

			switch (mode)
			{
				case ControllerMode.Idle:
					if (InputX.GetMouseButtonDown(0))
						SetMode(ControllerMode.Evaluate);
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
							iMap.UpdateTileAt(currentWorld, ResourceManager.DefaultHash);
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
						iMap.UpdateTileAt(Map.FullFloorVec(currentWorld), variant);
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
						//if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
						//{
						//	var index = iMap.VectorToIndex(currentWorld);
						//	if (selection?.Any(s => s is Cell c && iMap.VectorToIndex(c.origin) == index) == false)
						//		ClearSelection();
						//}

						if (StartTileDrag(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
						//if (StartTileDrag(true))
						{
							SetMode(ControllerMode.DragTile);
							UpdateRotateGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
						}
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
		private void UpdateRotateGizmo() { if (selection?.Length > 1) EditorDirectionUtil.Hide(); }//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		private void UpdateViewGizmo() { if (selection?.Length > 1) { ViewPreviewUtil.Hide(); EditorTransformUtil.Hide(); EditorFrustumUtil.Hide(); } }//temporary workaround for preview system - for now do not allow in multiselect mode

		private void UpdateSelectionAltitude(float value)
		{
			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
				cell.origin.y = cell.position.y = value;

			selection = selection?.ToArray();//restore selection state

			UpdateRotateGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}

		private bool StartTileDrag(bool combine = false) => SelectTile(beginWorld = currentWorld, combine);

		private void UpdateTileDrag()
		{
			var snappedDelta = iMap.GetVariantAt(beginWorld).HasNav ?
				Map.FullFloorVec(currentWorld) - Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(currentWorld) - Map.HalfFloorVec(beginWorld);

			foreach (var cell in selection?.OfType<Cell>() ?? Array.Empty<Cell>())
			{
				cell.position = cell.origin + snappedDelta;
				cell.OnUpdate(iMap, _camera);
			}
			UpdateRotateGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}

		private void EndTileDrag()
		{
			var cells = selection?.OfType<Cell>() ?? Enumerable.Empty<Cell>();
			if (!cells.Any()) return;

			var extents = GeomUtils.PointArrayBoundsInt((new[] { new Vector2Int(0, 0), new Vector2Int(iMap.Width - 1, iMap.Height - 1) }).Concat(
				cells.Select(c => new Vector2Int(Mathf.FloorToInt(c.position.x), Mathf.FloorToInt(c.position.z)))));

			if (!Map.ValidExtents(extents))
			{
				//reset selection to current map positions
				foreach (var cell in cells) cell.position = cell.origin;
				selection = selection?.ToArray();//restore selection state//selection = cells.OfType<ISelectable>().ToArray();
				UpdateRotateGizmo();
				return;
			}

			iMap.ResizeMap(extents);//resize the map for the selection to apply

			var copy = selection.OfType<Cell>();
			ClearSelection();

			foreach (var cell in copy)
			{
				if (cell.position != cell.origin)
					iMap.RemoveTileAt(cell.origin);
			}

			foreach (var cell in copy)
			{
				if (cell.position == cell.origin) continue;
				iMap.UpdateTileAt(cell.position, cell.variant, false);
				cell.origin = cell.position;
			}

			//restore selection
			selection = copy.OfType<ISelectable>().ToArray();

			iMap.UpdateTileAt(copy.First().position, copy.First().variant);//workaround to crop map after drag changes extents
			//iMap.CropToContent(true);//need to make sure  onmapchanged is invoked or we can't use this instead of above

			selection = selection?.ToArray();//restore selection state

			UpdateRotateGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}

		private bool SelectTile(Vector3 worldPos, bool combine = false)
		{
			if (iMap.GetVariantAt(worldPos).IsDefaultEquivalent) return false;
			var index = iMap.VectorToIndex(worldPos);
			if (-1 == index) return false;
			if (selection?.Any(s => s is Cell c && iMap.VectorToIndex(c.origin) == index) == true)
				return true;
			if (false == combine) ClearSelection();
			var newCell = new Cell(iMap, worldPos);
			selection = selection == null ? new[] { newCell } : selection.Append(newCell).ToArray();
			UpdateRotateGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
			return true;
		}

		private void ClearSelection() => selection = null;

		private void EvaluateAttachments()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: cursorTile);
			if (EditorAttachmentUI.EvaluateSelection(attachmentsOnTile, cursorTile))
				SelectAttachments(attachmentsOnTile);
			MapUtils.RebuildMarkers(iMap, selection);
			UpdateViewGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}

		private bool StartAttachmentDrag()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachments(iMap.GetAttachments(tileIndex: cursorTile));
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
				selection[0].OnUpdate(iMap, _camera);
			iMap.RefreshAttachments(attSelection);
			MapUtils.RebuildMarkers(iMap, selection);
			UpdateViewGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}

		private bool CancelAttachmentMode()
		{
			if (selection?.Length > 0)
			{
				selection = null;
				MapUtils.RebuildMarkers(iMap, selection);
				return false;
			}
			SelectAttachments(iMap.GetAttachments(tileIndex: iMap.VectorToIndex(beginWorld = currentWorld)));
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
			UpdateViewGizmo();//temporary workaround for rotate gizmo - for now do not allow in multiselect mode
		}
	}
}