using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		private IMapEdit iMap;
		private TileSelector tileAtlas => FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
		private Camera _camera => GetComponent<MainCameraController>()?.activeSystem?.camera;

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition);
		private ISelectable[] _selection = null;
		private ISelectable[] selection
		{
			get => _selection;
			set { Array.ForEach(_selection ?? Array.Empty<ISelectable>(), item => item.OnDeselect()); if (value?.Length is 1) value[0].OnSelect(iMap, _camera); _selection = value;}
		}

		// ─── Tile / Attachment state ─────────────────────────────────────────
		private enum ControllerMode
		{
			Idle,
			Evaluate,
			PlacingTile,
			SelectingTile,
			DraggingTile,
			DraggingAttachment
		}

		private ControllerMode mode = ControllerMode.Idle;
		private void SetMode(ControllerMode value) => mode = value;

		// ─── Unity / lifecycle ───────────────────────────────────────────────
		public void Awake()
		{
			Debug.Assert(null != tileAtlas, "TileAtlas not found!");
			if (null == tileAtlas) return;
			tileAtlas.OnTileSelected += (HashId newHash) => {EditorSelectionUtil.CurrentVariant = new Variant(newHash);
				SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle); };
			tileAtlas.CanOpenPalette = () => mode == ControllerMode.Idle;
		}

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += (Map map, bool resized, Vector3 originDelta) => { ResourceManager.ApplyMapChanges(map);
				if (resized) GridLinesUtil.UpdateSize(map.width, map.height); };

			Reset();
			GridLinesUtil.Update(transform, iMap?.Width ?? 32, iMap?.Height ?? 32, null != iMap ? iMap.TileRenderPosition(0) - new Vector3(0.5f, 0f, 0.5f) : Vector3.zero);
			if (isActiveAndEnabled) GridLinesUtil.Show();
		}

		private void Reset()
		{
			DeselectTile();
			EditorAttachmentUI.ClearPending();
			EditorMarkerUtil.ClearMapMarkers();
		}

		private void OnEnable()
		{
			Reset();

			var mainCameraController = GetComponent<MainCameraController>();
			if (null != mainCameraController)
			{
				mainCameraController.SetCameraSystem(CameraModeRegistry.Editor, false);
				mainCameraController.UpdateGestureControllerState();
			}

			tileAtlas?.gameObject.SetActive(true);
			GridLinesUtil.Show();
			SetMode(ControllerMode.Idle);
		}

		private void OnDisable()
		{
			tileAtlas?.gameObject.SetActive(false);
			GridLinesUtil.Hide();
			Reset();
		}

		private void Update()
		{
			if (!_camera) return;

			var mouseOverGUI = (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
				|| GUIUtility.hotControl != 0 || PlaceholderUI.IsMouseOverGui() || EditorAttachmentUI.sidePanel.IsMouseOver;

			ViewPreviewUtil.Update();
			EditorCameraMovement.UpdateCamera(ViewPreviewUtil.IsInFocus ? ViewPreviewUtil.PreviewCamera : _camera, currentWorld, inFocus: !mouseOverGUI);
			if (!ViewPreviewUtil.IsInFocus && mouseOverGUI) return;
			if (selection?.Length == 1 && selection[0] is MapAttachment a && a.OnGizmoInput(iMap, _camera)) return;

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
							EvaluateAttachments();
						if (InputX.GetMouseButtonHeld(0))
						{
							if (!StartTileDrag())
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
					var variant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, EditorSelectionUtil.CurrentVariant);
					EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), variant, false);

					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							iMap.UpdateTileAt(currentWorld, variant);
						if (InputX.GetMouseButtonUp(1))
						{
							EditorSelectionUtil.HideGhostMesh();
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.SelectingTile:
					if (InputX.GetMouseButtonDown(0))
					{
						if (!StartTileDrag())
							EditorCameraMovement.StartPanning(currentWorld);
					}
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(1))
							DeselectTile();
					}
					break;

				case ControllerMode.DraggingTile:
					if (InputX.GetMouseButton(0))
						UpdateTileDrag();
					if (InputX.GetMouseButtonUp(0))
					{
						SetMode(ControllerMode.SelectingTile);
						EndTileDrag();
					}
					break;

				case ControllerMode.DraggingAttachment:
					if (InputX.GetMouseButtonDown(0))
						beginWorld = currentWorld;
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							EvaluateAttachments();
						if (InputX.GetMouseButtonUp(1))
							CancelAttachmentMode();
					}
					else
					{
						if (InputX.GetMouseButton(0))
						{
							if (!StartAttachmentDrag())
								EditorCameraMovement.StartPanning(currentWorld);
							UpdateAttachmentDrag();
						}
					}
					break;
			}
		}

		private void OnGUI()
		{
			ViewPreviewUtil.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, iMap.VectorToIndex(beginWorld), selectable => SelectAttachments(selectable));
		}

		private void OnDestroy()
		{
			Reset();
			EditorSelectionUtil.DestroyGhostMesh();
		}

		// ─── All helper methods ──────────────────────────────────────────────
		private bool StartTileDrag()
		{
			beginWorld = currentWorld;//we could snap beginWorld to half resolution but it's hardly worth it: Map.HalfFloorVec(currentWorld);
			if (!SelectTile(currentWorld)) return false;
			SetMode(ControllerMode.DraggingTile);
			return true;
		}

		private void UpdateTileDrag()
		{
			var variant = iMap.GetVariantAt(beginWorld);
			var startWorld = variant.HasNav ? Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(beginWorld);
			var worldPos = Map.FullFloorVec(beginWorld) + currentWorld - startWorld;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = variant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;
			EditorSelectionUtil.UpdateGhostMesh(iMap, snapped + delta, variant, true);
		}

		private void EndTileDrag()
		{
			var variant = iMap.GetVariantAt(beginWorld);
			var startWorld = variant.HasNav ? Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(beginWorld);
			var worldPos = Map.FullFloorVec(beginWorld) + currentWorld - startWorld + variant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = variant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;
			if (snapped == Map.FullFloorVec(beginWorld) && delta == variant.delta) return;

			DeselectTile();
			delta.y = variant.delta.y;
			variant.delta = delta;
			iMap.RemoveTileAt(beginWorld);
			var index = iMap.UpdateTileAt(snapped, variant);
			if (-1 == index) index = iMap.UpdateTileAt(Map.FullFloorVec(beginWorld), variant);
			SelectTile(iMap.IndexToVector(index));
		}

		private bool SelectTile(Vector3 worldPos)
		{
			var tile = iMap.GetTile(worldPos);
			if (tile.gameObject == null) return false;

			Reset();
			EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(worldPos), iMap.GetVariantAt(worldPos), true);
			selection = new ISelectable[] { tile };
			SetMode(ControllerMode.SelectingTile);
			return true;
		}

		private void DeselectTile()
		{
			EditorSelectionUtil.HideGhostMesh();
			selection = null;
			SetMode(ControllerMode.Idle);
		}

		private bool StartAttachmentDrag()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld);
			if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachments(iMap.GetAttachments(tileIndex: cursorTile));
			return selection != null && selection.Length > 0;
		}

		private void UpdateAttachmentDrag()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld);
			var tile = iMap.VectorToIndex(currentWorld);
			if (tile == cursorTile || tile == -1 || selection == null || selection.Length == 0)
				return;
			cursorTile = iMap.VectorToIndex(beginWorld = currentWorld);
			var attSelection = selection.OfType<MapAttachment>().ToArray();
			foreach (var att in attSelection)
				att.tile = cursorTile;
			iMap.RefreshAttachments(attSelection);

			if (null != selection && selection.Length == 1 && selection[0] is MapAttachment ma)
				ma.OnDragInput(iMap, _camera);
			RebuildMarkers();
		}

		private void CancelAttachmentMode()
		{
			beginWorld = currentWorld;
			EvaluateAttachments();
			var cursorTile = iMap.VectorToIndex(beginWorld);
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)
			{
				Reset();
				SetMode(ControllerMode.Idle);
				return;
			}
			EditorAttachmentUI.RequestDelete();
		}

		private void SelectAttachments(ISelectable[] value = null)
		{
			selection = value;
			RebuildMarkers();
		}

		private void EvaluateAttachments()
		{
			var cursorTile = iMap.VectorToIndex(beginWorld);
			beginWorld = currentWorld;
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: cursorTile);
			if (EditorAttachmentUI.EvaluateSelection(attachmentsOnTile, cursorTile))
				SelectAttachments(attachmentsOnTile);

			RebuildMarkers();
			SetMode(ControllerMode.DraggingAttachment);
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