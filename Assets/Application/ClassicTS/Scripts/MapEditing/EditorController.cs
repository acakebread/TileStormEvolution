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
			set { Array.ForEach(_selection ?? Array.Empty<ISelectable>(), item => item.OnDeselect(iMap, _camera)); if (value?.Length is 1) value[0].OnSelect(iMap, _camera); _selection = value;}
		}
		private float editAltitude = 0f;

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
			EditorSelectionUtil.CurrentVariant = new Variant(newHash);
			SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle);
		}

		public void OnAltitudeChanged(float value)
		{
			editAltitude = value;
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
			iMap.OnMapEdited += (Map map, bool resized, Vector3 originDelta) => { ResourceManager.ApplyMapChanges(map);
				if (resized) GridLinesUtil.UpdateSize(map.width, map.height); };

			GridLinesUtil.Update(transform, iMap?.Width ?? 32, iMap?.Height ?? 32, null != iMap ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, editAltitude, -0.5f) : Vector3.zero);
			if (!isActiveAndEnabled) return;
			GridLinesUtil.Show();
			DeselectTile();
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
			DeselectTile();
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
					var current = EditorSelectionUtil.CurrentVariant;
					var variant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, current);
					if (InputX.staticClick && InputX.GetMouseButtonUp(0))
						iMap.UpdateTileAt(Map.FullFloorVec(currentWorld), variant);
					EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), variant, false);
					EditorSelectionUtil.CurrentVariant = current;//restore placement - ToDo remove storage of CurrentVariant from EditorSelectionUtil

					if (InputX.staticClick && InputX.GetMouseButtonUp(1))
					{
						EditorSelectionUtil.HideGhostMesh();
						SetMode(ControllerMode.Idle);
					}
					break;

				case ControllerMode.SelectTile:
					if (InputX.GetMouseButtonDown(0))
					{
						if (StartTileDrag())
							SetMode(ControllerMode.DragTile);
						else
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
						{
							DeselectTile();
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
		private Vector3 snappedWorld => iMap.GetVariantAt(beginWorld).HasNav ? 
			Map.FullFloorVec(currentWorld) : (Map.FullFloorVec(beginWorld) + Map.HalfFloorVec(currentWorld) - Map.HalfFloorVec(beginWorld));

		private bool StartTileDrag() => SelectTile(beginWorld = currentWorld);

		private void UpdateTileDrag()
		{
			if (selection?.Length != 1 || selection[0] is not Cell cell) return;
			cell.position = snappedWorld + new Vector3(cell.variant.delta.x, 0f, cell.variant.delta.z);
			selection[0].OnUpdate(iMap, _camera);
		}

		private void EndTileDrag()
		{
			if (selection?.Length != 1 || selection[0] is not Cell cell) return;

			cell.position = snappedWorld + new Vector3(cell.variant.delta.x, 0f, cell.variant.delta.z);
			if (cell.position == cell.startPosition) return;//unchanged - do not alter map

			DeselectTile();
			iMap.RemoveTileAt(beginWorld);
			var index = iMap.UpdateTileAt(cell.position, cell.variant);
			if (-1 == index) index = iMap.UpdateTileAt(beginWorld, cell.variant);
			SelectTile(iMap.IndexToVector(index) + Vector3.up * editAltitude);
		}

		private bool SelectTile(Vector3 worldPos)
		{
			var tile = iMap.GetTile(worldPos);
			if (null == tile.gameObject) return false;
			selection = new ISelectable[] { new Cell(iMap, worldPos) };
			return true;
		}

		private void DeselectTile() => selection = null;

		private void EvaluateAttachments()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: cursorTile);
			if (EditorAttachmentUI.EvaluateSelection(attachmentsOnTile, cursorTile))
				SelectAttachments(attachmentsOnTile);
			RebuildMarkers();
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
			RebuildMarkers();
		}

		private bool CancelAttachmentMode()
		{
			if (selection?.Length > 0)
			{
				selection = null;
				RebuildMarkers();
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
			RebuildMarkers();
		}

		private void RebuildMarkers()
		{
			var tiles = iMap?.GetAttachments()?.Select(a => a.tile)?.Distinct()?.ToArray() ?? Array.Empty<int>();
			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];
			var isWaypointMode = selection != null && selection.Length == 1 && selection[0] is Waypoint;

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);
				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile) ? new (0f, 1f, 1f, 0.5f) : new (0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0 && selection[0] is MapAttachment ma) ? ma.tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);
			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}
	}
}