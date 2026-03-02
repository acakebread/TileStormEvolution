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

		private bool gridEnabled = true;
		private bool postProcessingEnabled = false;

		public bool GridEnabled { get => gridEnabled; set => OnGridLinesToggled(value); }
		public bool PostProcessingEnabled { get => postProcessingEnabled; set => OnPostProcessingToggled(value); }

		// ─── input state ───────────────────────────────────────
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(_camera, InputX.mousePosition);

		private bool touchStartOverGui = false;

		// ─── Tile / Attachment state ─────────────────────────────────────────
		private enum ControllerMode
		{
			Idle,
			PlacingTile,
			SelectedTile,
			DraggingTile,
			UpdateAttachment
		}

		private ControllerMode mode = ControllerMode.Idle;
		private bool holdSelect;
		private float holdTime;

		private int cursorTile = -1;
		private Variant cursorVariant = new(ResourceManager.DefaultHash);
		private ISelectable[] selection = null;
		private Action unsubscribeTileSelectorAction;

		private bool IsMouseOverGUI()
			=> (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
			|| GUIUtility.hotControl != 0
			|| PlaceholderUI.IsMouseOverGui()
			|| EditorAttachmentUI.sidePanel.IsMouseOver;

		private MainCameraController mainCameraController => TryGetComponent<MainCameraController>(out var c) ? c : null;
		private Camera _camera => mainCameraController?.activeSystem?.camera;

		private void OnGridLinesToggled(bool value) => UpdateGridLines(gridEnabled = value);
		private void OnPostProcessingToggled(bool value) => mainCameraController?.EnableEditorPostProcessing(postProcessingEnabled = value);

		private void SetMode(ControllerMode value) => mode = value;

		// ─── Unity / lifecycle ───────────────────────────────────────────────

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;

			iMap.OnMapEdited += OnMapEdited;
			if (!isActiveAndEnabled) return;

			UpdateGridLines(gridEnabled);
			ViewPreviewUtil.Hide();
			EditorCameraMovement.isPanning = false;
			ResetInputState();
			EnableEggbot(false);
		}

		public void Reset()
		{
			if (iMap != null) iMap.OnMapEdited -= OnMapEdited;
			GridLinesUtil.Hide();
			DeselectTile();
			EditorSelectionUtil.DestroyGhostMesh();
		}

		private void OnEnable()
		{
			if (null != mainCameraController)
			{
				mainCameraController.SetCameraSystem(CameraModeRegistry.Editor, false);
				mainCameraController.UpdateGestureControllerState();
				mainCameraController.EnableEditorPostProcessing(postProcessingEnabled);
			}

			UpdateGridLines(gridEnabled);
			ViewPreviewUtil.Hide();
			ResetInputState();
			SetMode(ControllerMode.Idle);
			EnableEggbot(false);

			var tileSelector = FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => mode == ControllerMode.Idle;
			unsubscribeTileSelectorAction = () =>
			{
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;
				tileSelector.CanOpenPalette = () => false;
			};

			void OnTileSelectedFromPalette(HashId newHash)
			{
				DeselectTile();
				cursorVariant = new Variant(newHash);
				SetMode(newHash != ResourceManager.DefaultHash ? ControllerMode.PlacingTile : ControllerMode.Idle);
			}
		}

		private void OnDisable()
		{
			ViewPreviewUtil.Hide();
			EditorCameraMovement.isPanning = false;

			unsubscribeTileSelectorAction?.Invoke();
			unsubscribeTileSelectorAction = null;

			DeselectTile();
			ResetInputState();
			GridLinesUtil.Hide();
			EnableEggbot(true);
		}

		private void Update()
		{
			if (InputX.GetMouseButtonDown(0) || InputX.GetMouseButtonDown(1))
			{
				if (InputX.GetMouseButtonDown(0))
					beginWorld = currentWorld;
				touchStartOverGui = IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();
			}

			if (!InputX.GetMouseButton(0) && !InputX.GetMouseButton(1))
				touchStartOverGui = false;

			ViewPreviewUtil.Update();
			if (ViewPreviewUtil.IsInFocus)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform, currentWorld);
				ViewAttachmentHandler.HandlePreviewCameraSync(iMap, _camera, selection[0]);
				return;
			}
			else
				EditorCameraMovement.UpdateCamera(_camera ? _camera.transform : null, currentWorld, inFocus: !IsMouseOverGUI());

			if (IsMouseOverGUI()) return;

			if (HandleGizmoInput())
			{
				EditorTransformUtil.UpdateTransformGizmoVisuals(_camera);
				return;
			}

			if (GuiUtils.WasGuiActiveLastFrame)
			{
				InputX.mouseMovedBeyondThreshold = true;
				return;
			}

			if (_camera)
				OnControl(!InputX.mouseMovedBeyondThreshold);

			void OnControl(bool staticClick)
			{
				switch (mode)
				{
					case ControllerMode.Idle:
						if (InputX.GetMouseButtonDown(0))
						{
							var variant = iMap.GetVariantAt(currentWorld);

							if (variant.IsDefaultEquivalent)
								EditorCameraMovement.StartPanning(beginWorld);
							else
							{
								holdTime = Time.time;
								holdSelect = true;
							}
						}

						if (staticClick)
						{
							if (InputX.GetMouseButtonUp(0))
							{
								holdSelect = false;
								cursorTile = iMap.VectorToIndex(currentWorld);
								EvaluateAttachment();
							}

							if (holdSelect && Time.time - holdTime >= 0.25f)
							{
								holdSelect = false;
								if (!StartTileDrag())
									EditorCameraMovement.StartPanning(beginWorld);
							}

							if (InputX.GetMouseButtonUp(1))
								iMap.UpdateTileAt(currentWorld, ResourceManager.DefaultHash);
						}
						else
						{
							if (InputX.GetMouseButton(0) && holdSelect)
							{
								holdSelect = false;
								EditorCameraMovement.StartPanning(beginWorld);
							}
						}
						break;

					case ControllerMode.PlacingTile:
						var nextVariant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, cursorVariant);
						EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), nextVariant, false);

						if (staticClick)
						{
							if (InputX.GetMouseButtonUp(0))
								iMap.UpdateTileAt(currentWorld, nextVariant);

							if (InputX.GetMouseButtonUp(1))
							{
								EditorSelectionUtil.HideGhostMesh();
								SetMode(ControllerMode.Idle);
							}
						}
						break;

					case ControllerMode.SelectedTile:
						if (staticClick)
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

						if (staticClick)
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
			}
		}

		private void OnGUI()
		{
			ViewPreviewUtil.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, cursorTile, selectable => SelectAttachment(selectable));
		}

		private void OnDestroy() => Reset();

		// ─── Helpers (grid, post, eggbot) ────────────────────────────────────
		private void EnableEggbot(bool value)
		{
			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (eggbotController != null) eggbotController.gameObject.SetActive(value);
		}

		private void UpdateGridLines(bool enabled = true)
			=> GridLinesUtil.Show(
				transform,
				iMap != null ? iMap.Width : 32,
				iMap != null ? iMap.Height : 32,
				gridEnabled = enabled,
				offset: iMap != null ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, 0f, -0.5f) : Vector3.zero
			);

		// ─── Map events ──────────────────────────────────────────────────────
		private void OnMapEdited(Map map, bool resized, Vector3 originDelta)
		{
			if (map == null) return;
			ResourceManager.ApplyMapChanges(map);
			if (!resized) return;
			if (gridEnabled) GridLinesUtil.UpdateSize(map.width, map.height);
			// originDelta handling was empty in original → left as-is
		}

		// ─── Input / control logic ───────────────────────────────────────────
		private bool HandleGizmoInput()
		{
			if (selection == null || selection.Length == 0) return false;
			if (selection[0] is not MapAttachment attachment) return false;
			return attachment.OnGizmoInput(iMap, _camera);
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
			SelectTile(iMap.IndexToVector(index));
		}

		private bool SelectTile(Vector3 worldPos)
		{
			DeselectTile();

			var tile = iMap.GetTile(worldPos);
			if (tile.gameObject == null)
				return false;

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
			SetMode(ControllerMode.Idle);
		}

		private bool StartAttachmentDrag()
		{
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)
			{
				EndAttachmentMode();
				return false;
			}

			if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
				SelectAttachment(iMap.GetAttachments(tileIndex: cursorTile));

			return true;
		}

		private void UpdateAttachmentDrag()
		{
			var tile = iMap.CameraHitTile(_camera, InputX.mousePosition);
			if (tile == cursorTile || tile == -1 || selection == null || selection.Length == 0)
				return;

			cursorTile = tile;

			var attSelection = selection.OfType<MapAttachment>().ToArray();
			foreach (var att in attSelection)
			{
				att.tile = cursorTile;
				iMap.RefreshAttachment(att);
			}

			HandleDragInput();
			RebuildMarkers();

			void HandleDragInput()
			{
				if (selection == null || selection.Length != 1) return;
				var ma = (MapAttachment)selection[0];
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = iMap.WorldPosition(ma.tile, transformable.Position);
					var worldRot = iMap.WorldRotation(ma.tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, _camera);
				}

				if (null != ma)
					ma.OnDragInput(iMap);
			}
		}

		private void EndAttachmentMode()
		{
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)
			{
				cursorTile = -1;
				SelectAttachment();
				EditorAttachmentUI.ClearPending();
				HideAllGizmos();
				SetMode(ControllerMode.Idle);
				return;
			}
			EditorAttachmentUI.RequestDelete();
		}

		private void SelectAttachment(ISelectable[] value = null)
		{
			ViewPreviewUtil.Hide();
			HideAllGizmos();
			RebuildMarkers();

			selection = value;
			if (selection == null || selection.Length != 1)
			{
				cursorTile = -1;
				return;
			}

			selection[0].OnSelectionChanged(iMap, _camera);
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

		private void ResetInputState()
		{
			selection = null;
			cursorTile = -1;
			EditorAttachmentUI.ClearPending();
			HideAllGizmos();
		}

		private void HideAllGizmos()
		{
			EditorTransformUtil.Hide();
			EditorPrimitiveUtil.Hide();
			EditorFrustumUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
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

				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile)
					? new Color(0f, 1f, 1f, 0.5f)
					: new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0 && selection[0] is MapAttachment ma) ? ma.tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		//public void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);//disabled for now as it wasn't working properly anyway
	}
}