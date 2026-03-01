using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		public IMapEdit iMap;

		private bool gridEnabled = true;
		private bool postProcessingEnabled = false;

		public bool GridEnabled { get => gridEnabled; set => OnGridLinesToggled(value); }
		public bool PostProcessingEnabled { get => postProcessingEnabled; set => OnPostProcessingToggled(value); }

		// ─── drag-to-pan & input state ───────────────────────────────────────
		private bool isPanning;
		private Vector3 beginWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);

		private Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 3f;

		private bool touchStartOverGui = false;

		private bool IsMouseOverGUI()
			=> PlaceholderUI.IsMouseOverGui()
			|| GUIUtility.hotControl != 0
			|| (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
			|| EditorAttachmentUI.sidePanel.IsMouseOver;

		private Camera camera => mainCameraController?.activeSystem?.camera;

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

		private void SetMode(ControllerMode value) => mode = value;

		// ─── Other fields ────────────────────────────────────────────────────
		private Action unsubscribeMapAction;

		// ─── Unity / lifecycle ───────────────────────────────────────────────
		private void Awake() { }

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += OnMapEdited;
			unsubscribeMapAction = () => iMap.OnMapEdited -= OnMapEdited;

			if (!isActiveAndEnabled) return;

			UpdateGridLines(gridEnabled);
			ViewPreviewUtil.Hide();
			isPanning = false;
			ResetInputState();
			EnableEggbot(false);
		}

		public void Reset()
		{
			unsubscribeMapAction?.Invoke();
			unsubscribeMapAction = null;
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}

			UpdateGridLines(gridEnabled);
			UpdatePostProcessing(postProcessingEnabled);
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
			isPanning = false;

			unsubscribeTileSelectorAction?.Invoke();
			unsubscribeTileSelectorAction = null;

			DeselectTile();
			ResetInputState();
			GridLinesUtil.Hide();
			EnableEggbot(true);
		}

		private void Update()
		{
			var cameraEditor = gameCameraEditor;
			if (cameraEditor != null)
			{
				var volume = getVolume(cameraEditor.controller.gameObject);
				var distance = (cameraEditor.controller.transform.position - Map.CameraToWorld(cameraEditor.camera)).magnitude;
				VolumeUtils.SetDepthOfFieldDistance(volume, Mathf.Max(Mathf.Min(distance, cameraEditor.controller.transform.position.y * 3f), 1f));
			}

			if (InputX.GetMouseButtonDown(0) || InputX.GetMouseButtonDown(1))
			{
				if (InputX.GetMouseButtonDown(0))
					beginWorld = Map.ScreenToWorld(camera, InputX.mousePosition);
				mouseDownPos = InputX.mousePosition;
				mouseMovedBeyondThreshold = false;
				touchStartOverGui = IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();
			}

			if ((InputX.GetMouseButton(0) || InputX.GetMouseButton(1))
				&& Vector3.Distance(InputX.mousePosition, mouseDownPos) >= CLICK_THRESHOLD
				|| InputX.GetAxis("Mouse ScrollWheel") > 0.01f)
			{
				mouseMovedBeyondThreshold = true;
			}

			if (InputX.GetMouseButtonUp(0))
				isPanning = false;

			if (!InputX.GetMouseButton(0) && !InputX.GetMouseButton(1))
			{
				if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
					Debug.Log("rogue state - problem with InputX caching");
				touchStartOverGui = false;
			}

			ViewPreviewUtil.Update();

			if (ViewPreviewUtil.IsInFocus)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);
			}
			else
			{
				if (!touchStartOverGui)
				{
					var overGUI = (InputX.GetMouseButton(0) || InputX.GetMouseButton(1))
						? touchStartOverGui
						: IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();
					EditorCameraMovement.UpdateCamera(camera ? camera.transform : null, isMouseOverGui: overGUI);
				}
			}

			var attSelection = selection?.OfType<MapAttachment>().ToArray() ?? Array.Empty<MapAttachment>();
			ViewAttachmentHandler.HandlePreviewCameraSync(iMap, camera, attSelection);

			if (ViewPreviewUtil.IsInFocus) return;

			if (IsMouseOverGUI()) return;

			if (HandleGizmoInput())
			{
				EditorTransformUtil.UpdateTransformGizmoVisuals(camera);
				return;
			}

			if (isPanning)
			{
				if (currentWorld != Vector3.negativeInfinity)
					camera.transform.position += beginWorld - currentWorld;
			}

			if (GuiUtils.WasGuiActiveLastFrame)
			{
				mouseMovedBeyondThreshold = true;
				return;
			}

			if (camera)
				OnControl(!mouseMovedBeyondThreshold);

			void OnControl(bool staticClick)
			{
				switch (mode)
				{
					case ControllerMode.Idle:
						if (InputX.GetMouseButtonDown(0))
						{
							var variant = iMap.GetVariantAt(currentWorld);

							if (variant.IsDefaultEquivalent)
								StartPanning();
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
									StartPanning();
							}

							if (InputX.GetMouseButtonUp(1))
								iMap.UpdateTileAt(currentWorld, ResourceManager.DefaultHash);
						}
						else
						{
							if (InputX.GetMouseButton(0) && holdSelect)
							{
								holdSelect = false;
								StartPanning();
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
									StartPanning();
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
							cursorTile = iMap.CameraHitTile(camera, InputX.mousePosition);

						if (staticClick)
						{
							if (InputX.GetMouseButtonUp(0))
								EvaluateAttachment();

							if (InputX.GetMouseButtonUp(1))
							{
								cursorTile = iMap.CameraHitTile(camera, InputX.mousePosition);
								EvaluateAttachment();
								EndAttachmentMode();
							}
						}
						else
						{
							if (InputX.GetMouseButton(0))
							{
								if (!StartAttachmentDrag())
									StartPanning();
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

			EditorAttachmentUI.UpdateGUI(
				iMap,
				cursorTile,
				selectable =>
				{
					var attachments = selectable?.OfType<MapAttachment>().ToArray() ?? Array.Empty<MapAttachment>();
					SelectAttachment(attachments);
				}
			);
		}

		public void OnApplicationFocus(bool hasFocus)
		{
			EditorCameraMovement.OnApplicationFocus(hasFocus);
		}

		private void OnDestroy()
		{
			GridLinesUtil.Hide();
			if (iMap != null) iMap.OnMapEdited -= OnMapEdited;
			DeselectTile();
			EditorSelectionUtil.DestroyGhostMesh();
		}

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

		private void UpdatePostProcessing(bool enabled = true)
		{
			if (gameCameraEditor != null)
			{
				var volume = getVolume(gameCameraEditor.controller.gameObject);
				volume.enabled = enabled;
				VolumeUtils.EnableDepthOfField(volume, enabled);
				VolumeUtils.SetDepthOfFieldDistance(volume, 8f);
			}
		}

		private void OnGridLinesToggled(bool value) => UpdateGridLines(gridEnabled = value);
		private void OnPostProcessingToggled(bool value) => UpdatePostProcessing(postProcessingEnabled = value);

		private Volume getVolume(GameObject root) => root.GetComponentInChildren<Volume>(true);

		private MainCameraController mainCameraController => TryGetComponent<MainCameraController>(out var c) ? c : null;

		private GameCameraEditor gameCameraEditor
			=> mainCameraController != null && mainCameraController.activeSystem is GameCameraEditor editor ? editor : null;

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

			var attSelection = selection.OfType<MapAttachment>().ToArray();
			if (attSelection.Length == 0) return false;

			var firstType = attSelection[0].GetType();
			if (!attSelection.All(a => a.GetType() == firstType)) return false;

			return attSelection[0].OnGizmoInput(iMap, camera, attSelection);
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
			if (index == -1) index = iMap.UpdateTileAt(Map.FullFloorVec(beginWorld), cursorVariant);
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

			if (cursorTile != -1)
			{
				if (selection == null || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
					SelectAttachment(iMap.GetAttachments(tileIndex: cursorTile));
				return true;
			}

			SelectAttachment();
			return true;
		}

		private void UpdateAttachmentDrag()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
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
				if (selection[0] is ITransformableAttachment transformable)
				{
					var ma = selection[0] as MapAttachment;
					var worldPos = iMap.WorldPosition(ma.tile, transformable.Position);
					var worldRot = iMap.WorldRotation(ma.tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
				}

				if (selection[0] is MapAttachment ma2)
					ma2.OnDragInput(iMap, attSelection);
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

		private void SelectAttachment(MapAttachment[] attachments = null)
		{
			selection = attachments?.Length > 0 ? attachments.Cast<ISelectable>().ToArray() : null;

			ViewPreviewUtil.Hide();
			HideAllGizmos();
			RebuildMarkers();

			if (selection == null || selection.Length != 1)
			{
				cursorTile = -1;
				return;
			}

			if (selection[0] is not MapAttachment first) return;

			var firstType = first.GetType();
			var atts = selection.OfType<MapAttachment>().ToArray();
			if (!atts.All(a => a.GetType() == firstType)) return;

			first.OnSelectionChanged(iMap, camera, atts);
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

			for (int i = 0; i < tiles.Length; i++)
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

		private void StartPanning()
		{
			if (isPanning) return;
			isPanning = beginWorld != Vector3.negativeInfinity;
		}
	}
}