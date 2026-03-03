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
		private TileSelector tileSelector => FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
		private Camera _camera => GetComponent<MainCameraController>()?.activeSystem?.camera;

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition);
		private int cursorTile = -1;
		private Variant cursorVariant = new(ResourceManager.DefaultHash);
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
			SelectedTile,
			DraggingTile,
			UpdateAttachment
		}

		private ControllerMode mode = ControllerMode.Idle;
		private void SetMode(ControllerMode value) => mode = value;

		// ─── Unity / lifecycle ───────────────────────────────────────────────
		public void Awake()
		{
			Debug.Assert(null != tileSelector, "TileSelector not found!");
			if (null == tileSelector) return;
			tileSelector.OnTileSelected += (HashId newHash) => {
				cursorVariant = new Variant(newHash);
				SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle);
			};
			tileSelector.CanOpenPalette = () => mode == ControllerMode.Idle;
		}

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += OnMapEdited;
			Reset();

			GridLinesUtil.Update(transform, iMap?.Width ?? 32, iMap?.Height ?? 32, null != iMap ? iMap.TileRenderPosition(0) - new Vector3(0.5f, 0f, 0.5f) : Vector3.zero);
			if (isActiveAndEnabled) GridLinesUtil.Show();
		}

		private void Reset()
		{
			DeselectTile();
			selection = null;
			cursorTile = -1;
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

			tileSelector?.gameObject.SetActive(true);
			GridLinesUtil.Show();
			SetMode(ControllerMode.Idle);
		}

		private void OnDisable()
		{
			tileSelector?.gameObject.SetActive(false);
			GridLinesUtil.Hide();
			Reset();
		}

		private void Update()
		{
			if (!_camera) return;

			if (InputX.GetMouseButtonDown(0))
				beginWorld = currentWorld;

			ViewPreviewUtil.Update();
			if (ViewPreviewUtil.IsInFocus)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera, currentWorld);
				ViewAttachmentHandler.HandlePreviewCameraSync(iMap, _camera, selection[0]);
				return;
			}
			else
				EditorCameraMovement.UpdateCamera(_camera, currentWorld, inFocus: !IsMouseOverGUI());

			if (IsMouseOverGUI()) return;

			if (HandleGizmoInput())
			{
				EditorTransformUtil.UpdateTransformGizmoVisuals(_camera);
				return;
			}

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
							cursorTile = iMap.VectorToIndex(beginWorld);
							EvaluateAttachment();
						}

						if (InputX.GetMouseButtonHeld(0))
						{
							if (!StartTileDrag())
							{
								EditorCameraMovement.StartPanning(beginWorld);
								SetMode(ControllerMode.Idle);
							}
						}
					}
					else
					{
						if (InputX.GetMouseButton(0))
						{
							EditorCameraMovement.StartPanning(beginWorld);
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.PlacingTile:
					var variant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, cursorVariant);
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

				case ControllerMode.SelectedTile:
					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonDown(0))
						{
							if (!StartTileDrag())
								EditorCameraMovement.StartPanning(beginWorld);
						}

						if (InputX.GetMouseButtonUp(1))
							DeselectTile();
					}
					break;

				case ControllerMode.DraggingTile:
					if (InputX.GetMouseButton(0))
						UpdateTileDrag();

					if (InputX.GetMouseButtonUp(0))
					{
						SetMode(ControllerMode.SelectedTile);
						EndTileDrag();
					}
					break;

				case ControllerMode.UpdateAttachment:
					if (InputX.GetMouseButtonDown(0))
						cursorTile = iMap.CameraHitTile(_camera, InputX.mousePosition);

					if (InputX.staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							EvaluateAttachment();

						if (InputX.GetMouseButtonUp(1))
						{
							cursorTile = iMap.CameraHitTile(_camera, InputX.mousePosition);
							EvaluateAttachment();
							EndAttachmentMode();
						}
					}
					else
					{
						if (InputX.GetMouseButton(0))
						{
							if (!StartAttachmentDrag())
								EditorCameraMovement.StartPanning(beginWorld);
							UpdateAttachmentDrag();
						}
					}
					break;
			}

			static bool IsMouseOverGUI()
				=> (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
				|| GUIUtility.hotControl != 0
				|| PlaceholderUI.IsMouseOverGui()
				|| EditorAttachmentUI.sidePanel.IsMouseOver;

			bool HandleGizmoInput()
			{
				if (selection == null || selection.Length == 0 || selection[0] is not MapAttachment attachment) return false;
				return attachment.OnGizmoInput(iMap, _camera);
			}
		}

		private void OnGUI()
		{
			ViewPreviewUtil.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, cursorTile, selectable => SelectAttachment(selectable));
		}

		private void OnDestroy()
		{
			Reset();
			EditorSelectionUtil.DestroyGhostMesh();
		}

		// ─── Map events ──────────────────────────────────────────────────────
		private void OnMapEdited(Map map, bool resized, Vector3 originDelta)
		{
			ResourceManager.ApplyMapChanges(map);
			if (resized)
				GridLinesUtil.UpdateSize(map.width, map.height);
		}

		// ─── All helper methods ──────────────────────────────────────────────
		private bool StartTileDrag()
		{
			if (!SelectTile(currentWorld))
				return false;
			SetMode(ControllerMode.DraggingTile);
			return true;
		}

		private void UpdateTileDrag()
		{
			var startWorld = cursorVariant.HasNav ? Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(beginWorld);
			var worldPos = Map.FullFloorVec(beginWorld) + currentWorld - startWorld;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = cursorVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;
			EditorSelectionUtil.UpdateGhostMesh(iMap, snapped + delta, cursorVariant, true);
		}

		private void EndTileDrag()
		{
			var startWorld = cursorVariant.HasNav ? Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(beginWorld);
			var worldPos = Map.FullFloorVec(beginWorld) + currentWorld - startWorld + cursorVariant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = cursorVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;

			if (snapped == Map.FullFloorVec(beginWorld) && delta == cursorVariant.delta)
				return;

			delta.y = cursorVariant.delta.y;
			cursorVariant.delta = delta;
			iMap.RemoveTileAt(beginWorld);
			var index = iMap.UpdateTileAt(snapped, cursorVariant);
			if (-1 == index) index = iMap.UpdateTileAt(Map.FullFloorVec(beginWorld), cursorVariant);
			DeselectTile();
			SelectTile(iMap.IndexToVector(index));
		}

		private bool SelectTile(Vector3 worldPos)
		{
			var tile = iMap.GetTile(worldPos);
			if (tile.gameObject == null)
				return false;

			Reset();
			tile.gameObject.SetActive(false);
			cursorVariant = iMap.GetVariantAt(worldPos);
			EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(worldPos), cursorVariant, true);
			selection = new ISelectable[] { tile };
			SetMode(ControllerMode.SelectedTile);
			return true;
		}

		private void DeselectTile()
		{
			EditorSelectionUtil.HideGhostMesh();
			foreach (var tile in selection?.OfType<Tile>().Where(t => t.gameObject != null) ?? Enumerable.Empty<Tile>())
				tile.gameObject.SetActive(true);
			selection = null;
			SetMode(ControllerMode.Idle);
		}

		private bool StartAttachmentDrag()
		{
			if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachment(iMap.GetAttachments(tileIndex: cursorTile));
			return selection != null && selection.Length > 0;
		}

		private void UpdateAttachmentDrag()
		{
			var tile = iMap.CameraHitTile(_camera, InputX.mousePosition);
			if (tile == cursorTile || tile == -1 || selection == null || selection.Length == 0)
				return;

			cursorTile = tile;

			var attSelection = selection.OfType<MapAttachment>().ToArray();
			foreach (var att in attSelection)
				att.tile = cursorTile;
			iMap.RefreshAttachments(attSelection);

			HandleDragInput();
			RebuildMarkers();

			void HandleDragInput()
			{
				if (selection == null || selection.Length == 0 || selection.Length > 1 || selection[0] is not MapAttachment ma) return;
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = iMap.WorldPosition(ma.tile, transformable.Position);
					var worldRot = iMap.WorldRotation(ma.tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, _camera);
				}
				ma.OnDragInput(iMap);
			}
		}

		private void EndAttachmentMode()
		{
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)
			{
				Reset();
				SetMode(ControllerMode.Idle);
				return;
			}
			EditorAttachmentUI.RequestDelete();
		}

		private void SelectAttachment(ISelectable[] value = null)
		{
			selection = value;
			RebuildMarkers();
		}

		private void EvaluateAttachment()
		{
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: cursorTile);

			if (attachmentsOnTile == null || attachmentsOnTile.Length == 0)
			{
				if (cursorTile != -1)
					EditorAttachmentUI.RequestAdd();
			}
			else if (attachmentsOnTile.Length > 1)
			{
				EditorAttachmentUI.RequestSelect();
			}
			else
			{
				EditorAttachmentUI.ClearPending();
				SelectAttachment(attachmentsOnTile);
			}

			RebuildMarkers();
			SetMode(ControllerMode.UpdateAttachment);
		}

		private void RebuildMarkers()
		{
			var tiles = iMap?.GetAttachments()?.Select(a => a.tile)?.Distinct()?.ToArray() ?? Array.Empty<int>();

			if (tiles.Length == 0)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

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