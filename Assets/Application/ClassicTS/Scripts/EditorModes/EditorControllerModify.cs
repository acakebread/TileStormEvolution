using System;
using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerModify : EditorControllerMovement
	{
		private enum ControllerMode
		{
			Idle,               // nothing selected, no placement active → can select, pan, start placing
			PlacingTile,        // active tile placement from palette (ghost visible)
			SelectedTile,       // tile is selected → can start drag or deselect
			DraggingTile,       // actively dragging a selected tile (mouse button held)
			UpdateAttachment    // editing attachments
		}

		private ControllerMode mode = ControllerMode.Idle;
		private bool holdSelect;
		private float holdTime;

		// Selection
		private Vector3 startWorld => cursorVariant.HasNav? Map.FullFloorVec(beginWorld) : Map.HalfFloorVec(beginWorld);
		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);

		private Variant cursorVariant = new(ResourceManager.DefaultHash);

		// Attachment state
		private int pendingTile = -1;
		private MapAttachment[] selection = null;

		public EditorControllerModify(EditorController editorController) : base(editorController) { }

		// ===================================================================
		// Lifecycle
		// ===================================================================
		public override void OnMapLoaded()
		{
			base.OnMapLoaded();
			ResetInputState();
		}

		public override void OnEnable()
		{
			base.OnEnable();
			ResetInputState();

			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => mode == ControllerMode.Idle;

			SetMode(ControllerMode.Idle);
		}

		public override void OnDisable()
		{
			base.OnDisable();
			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
			{
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;
				tileSelector.CanOpenPalette = () => false;
			}

			DeselectTile();
			ResetInputState();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			DeselectTile();
			cursorVariant = new Variant(newHash);
			SetMode((newHash != ResourceManager.DefaultHash) ? ControllerMode.PlacingTile : ControllerMode.Idle);
		}

		private void SetMode(ControllerMode value) => mode = value;

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!camera) return;

			switch (mode)
			{
				case ControllerMode.Idle:
					if (InputX.GetMouseButtonDown(0))
					{
						var variant = iMap.GetVariantAt(currentWorld);

						if (variant.IsDefaultEquivalent)
							StartPanning(); // immediate panning on default/empty
						else
						{
							holdTime = Time.time;
							holdSelect = true; // start the hold timer
						}
					}

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
						{
							holdSelect = false;
							pendingTile = iMap.VectorToIndex(currentWorld);
							SelectAttachemnt();
						}

						// check the hold timer
						if (holdSelect && Time.time - holdTime >= 0.25f)
						{
							holdSelect = false;
							if (!StartDrag())
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
							StartPanning(); // immediate panning when moved during hold
						}
					}
					break;

				case ControllerMode.PlacingTile:
					// Continuous ghost update in placing mode
					cursorVariant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, cursorVariant);
					EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), cursorVariant, false);

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							iMap.UpdateTileAt(currentWorld, cursorVariant);

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
							if (!StartDrag())
								StartPanning();
						}

						if (InputX.GetMouseButtonUp(1))
							DeselectTile();
					}
					break;

				case ControllerMode.DraggingTile:
					if (InputX.GetMouseButton(0))
						UpdateDrag();

					if (InputX.GetMouseButtonUp(0))
					{
						SetMode(ControllerMode.SelectedTile);
						EndDrag();
					}
					break;

				case ControllerMode.UpdateAttachment:

					if (InputX.GetMouseButtonDown(0))
						pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							SelectAttachemnt();

						if (InputX.GetMouseButtonUp(1))
						{
							pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);
							SelectAttachemnt();
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

		private bool StartAttachmentDrag()
		{
			if (pendingTile < 0 || iMap.GetAttachments(tileIndex: pendingTile).Length == 0)//no attachment here
			{
				EndAttachmentMode();
				return false;
			}

			if (-1 != pendingTile)
			{
				if (null == selection || selection.Length == 0 || selection[0].tile != pendingTile)
					Select(iMap.GetAttachments(tileIndex: pendingTile));
				return true;
			}
			Select();
			return true;
		}

		private void UpdateAttachmentDrag()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
			if (tile == pendingTile || -1 == tile || null == selection || 0 == selection.Length)
				return;

			pendingTile = tile;
			if (null == selection) return;
			foreach (var att in selection)
			{
				att.tile = pendingTile;
				iMap.RefreshAttachment(att);
			}
			HandleDragInput();
			RebuildMarkers();

			void HandleDragInput()
			{
				if (null == selection || 1 != selection.Length) return;
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = iMap.WorldPosition(selection[0].tile, transformable.Position);
					var worldRot = iMap.WorldRotation(selection[0].tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
				}
				selection[0].OnDragInput(iMap, selection);
			}
		}

		private void EndAttachmentMode()
		{
			if (pendingTile < 0 || iMap.GetAttachments(tileIndex: pendingTile).Length == 0)//no attachment here
			{
				pendingTile = -1;
				Select();
				EditorAttachmentUI.ClearPending();
				HideAllGizmos();
				SetMode(ControllerMode.Idle);
				return;
			}
			EditorAttachmentUI.RequestDelete();
		}

		private bool StartDrag()
		{
			if (!SelectTile(currentWorld))
				return false;
			SetMode(ControllerMode.DraggingTile);
			return true;
		}

		private void UpdateDrag()
		{
			var worldPos = Map.FullFloorVec(startWorld) + currentWorld - startWorld;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = cursorVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;
			EditorSelectionUtil.UpdateGhostMesh(iMap, snapped + delta, cursorVariant, true);
		}

		private void EndDrag()
		{
			var worldPos = Map.FullFloorVec(startWorld) + currentWorld - startWorld + cursorVariant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = cursorVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;

			if (snapped == Map.FullFloorVec(startWorld) && delta == cursorVariant.delta)
				return;//no change so ok to just exit

			delta.y = cursorVariant.delta.y;//retore old delta height
			cursorVariant.delta = delta;
			iMap.RemoveTileAt(startWorld);
			var index = iMap.UpdateTileAt(snapped, cursorVariant);
			if (-1 == index) index = iMap.UpdateTileAt(Map.FullFloorVec(startWorld), cursorVariant);//operation failed restore old tile
			SelectTile(iMap.IndexToVector(index));
		}

		private void DeselectTile()
		{
			EditorSelectionUtil.HideGhostMesh();
			var tile = iMap.GetTile(startWorld);
			if (null != tile.gameObject) tile.gameObject.SetActive(true);
			SetMode(ControllerMode.Idle);
		}

		private bool SelectTile(Vector3 worldPos)
		{
			DeselectTile();

			var tile = iMap.GetTile(worldPos);
			if (null == tile.gameObject)
				return false;

			tile.gameObject.SetActive(false);
			cursorVariant = iMap.GetVariantAt(worldPos);
			EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(worldPos), cursorVariant, true);
			beginWorld = worldPos;
			SetMode(ControllerMode.SelectedTile);

			return true;
		}

		public override void OnDestroy()
		{
			DeselectTile();
			EditorSelectionUtil.DestroyGhostMesh();
		}

		// ===================================================================
		// Helpers
		// ===================================================================

		private void Select(MapAttachment[] attachments = null)
		{
			selection = attachments?.Length > 0 ? attachments : null;

			ViewPreviewUtil.Hide();
			HideAllGizmos();
			RebuildMarkers();

			if (null == selection || 1 != selection.Length)
			{
				pendingTile = -1;
				return;
			}
			HandleSelectionChanged();

			void HandleSelectionChanged()
			{
				if (null == selection || 0 == selection.Length) return;
				var firstType = selection[0].GetType();
				if (!selection.All(a => a.GetType() == firstType)) return;
				selection[0].OnSelectionChanged(iMap, camera, selection);
			}
		}

		private void SelectAttachemnt()
		{
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: pendingTile);

			if (null == attachmentsOnTile || 0 == attachmentsOnTile.Length)
			{
				if (-1 != pendingTile)
					EditorAttachmentUI.RequestAdd();
			}
			else if (attachmentsOnTile.Length > 1)
			{
				EditorAttachmentUI.RequestSelect();
			}
			else
			{
				EditorAttachmentUI.ClearPending();
				Select(attachmentsOnTile);
			}

			RebuildMarkers();

			SetMode(ControllerMode.UpdateAttachment);
		}

		private void ResetInputState()
		{
			selection = null;
			pendingTile = -1;
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

			var isWaypointMode = null != selection && selection.Length == 1 && selection[0] is Waypoint;

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);

				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile)
					? new Color(0f, 1f, 1f, 0.5f)
					: new Color(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0) ? selection[0].tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		protected override void HandleGizmoInput()
		{
			if (null == selection || 0 == selection.Length) return;
			var firstType = selection[0].GetType();
			if (!selection.All(a => a.GetType() == firstType)) return;
			selection[0].OnGizmoInput(iMap, camera, selection);
		}

		public override void OnGUI()
		{
			base.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, atts => Select(atts), pendingTile);
		}

		public override void Update()
		{
			base.Update();
			ViewAttachmentHandler.HandlePreviewCameraSync(iMap, camera, selection);
		}
	}
}
