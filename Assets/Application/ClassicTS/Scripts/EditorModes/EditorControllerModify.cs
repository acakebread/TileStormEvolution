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

		// Tile selection and Attachment state
		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);
		private int cursorTile = -1;
		private Variant cursorVariant = new(ResourceManager.DefaultHash);
		private ISelectable[] selection = null;
		private Action unsubscribeTileSelectorAction;

		public EditorControllerModify(EditorController editorController) : base(editorController) { }

		private void SetMode(ControllerMode value) => mode = value;

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
			unsubscribeTileSelectorAction = () =>
			{
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;
				tileSelector.CanOpenPalette = () => false;
			};

			SetMode(ControllerMode.Idle);

			void OnTileSelectedFromPalette(HashId newHash)
			{
				DeselectTile();
				cursorVariant = new Variant(newHash);
				SetMode((newHash != ResourceManager.DefaultHash) ? ControllerMode.PlacingTile : ControllerMode.Idle);
			}
		}

		public override void OnDisable()
		{
			base.OnDisable();
			unsubscribeTileSelectorAction?.Invoke();
			unsubscribeTileSelectorAction = null;

			DeselectTile();
			ResetInputState();
		}

		public override void Update()
		{
			base.Update();
			var attSelection = selection?.OfType<MapAttachment>().ToArray() ?? Array.Empty<MapAttachment>();
			ViewAttachmentHandler.HandlePreviewCameraSync(iMap, camera, attSelection);
		}

		public override void OnGUI()
		{
			base.OnGUI();

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

		public override void OnDestroy()
		{
			DeselectTile();
			EditorSelectionUtil.DestroyGhostMesh();
		}

		protected override void HandleGizmoInput()
		{
			if (null == selection || 0 == selection.Length) return;

			var attSelection = selection.OfType<MapAttachment>().ToArray();
			if (attSelection.Length == 0) return;

			var firstType = attSelection[0].GetType();
			if (!attSelection.All(a => a.GetType() == firstType)) return;

			attSelection[0].OnGizmoInput(iMap, camera, attSelection);
		}

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
							cursorTile = iMap.VectorToIndex(currentWorld);
							EvaluateAttachment();
						}

						// check the hold timer
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

		// ===================================================================
		// Helpers
		// ===================================================================

		private bool StartTileDrag()
		{
			beginWorld = currentWorld;
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
				return;//no change so ok to just exit

			delta.y = cursorVariant.delta.y;//retore old delta height
			cursorVariant.delta = delta;
			iMap.RemoveTileAt(beginWorld);
			var index = iMap.UpdateTileAt(snapped, cursorVariant);
			if (-1 == index) index = iMap.UpdateTileAt(Map.FullFloorVec(beginWorld), cursorVariant);//operation failed restore old tile
			SelectTile(iMap.IndexToVector(index));
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
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)//no attachment here
			{
				EndAttachmentMode();
				return false;
			}

			if (-1 != cursorTile)
			{
				if (null == selection || selection.Length == 0 || (selection[0] is MapAttachment ma && ma.tile != cursorTile))
					SelectAttachment(iMap.GetAttachments(tileIndex: cursorTile));
				return true;
			}
			SelectAttachment();
			return true;
		}

		private void UpdateAttachmentDrag()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
			if (tile == cursorTile || -1 == tile || null == selection || 0 == selection.Length)
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
				if (null == selection || 1 != selection.Length) return;
				if (selection[0] is ITransformableAttachment transformable)
				{
					var worldPos = iMap.WorldPosition((selection[0] as MapAttachment).tile, transformable.Position);
					var worldRot = iMap.WorldRotation((selection[0] as MapAttachment).tile, transformable.Rotation);
					EditorTransformUtil.ShowAt(worldPos, worldRot, camera);
				}

				// only call if it's still MapAttachment
				if (selection[0] is MapAttachment ma)
					ma.OnDragInput(iMap, attSelection);
			}
		}

		private void EndAttachmentMode()
		{
			if (cursorTile < 0 || iMap.GetAttachments(tileIndex: cursorTile).Length == 0)//no attachment here
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

			if (null == selection || 1 != selection.Length)
			{
				cursorTile = -1;
				return;
			}
			HandleSelectionChanged();

			void HandleSelectionChanged()
			{
				if (null == selection || 0 == selection.Length) return;

				if (selection[0] is not MapAttachment first) return;

				var firstType = first.GetType();
				var atts = selection.OfType<MapAttachment>().ToArray();
				if (!atts.All(a => a.GetType() == firstType)) return;

				first.OnSelectionChanged(iMap, camera, atts);
			}
		}

		private void EvaluateAttachment()
		{
			var attachmentsOnTile = iMap.GetAttachments(tileIndex: cursorTile);

			if (null == attachmentsOnTile || 0 == attachmentsOnTile.Length)
			{
				if (-1 != cursorTile)
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

			var isWaypointMode = null != selection && selection.Length == 1 && selection[0] is Waypoint;

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
	}
}