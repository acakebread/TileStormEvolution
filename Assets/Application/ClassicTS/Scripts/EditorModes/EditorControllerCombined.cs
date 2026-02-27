using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerCombined : EditorControllerMovement
	{
		private enum ControllerMode
		{
			Idle,
			PlacingTile,
			SelectedTile,
			DraggingTile,

			SelectedAttachment,
			DraggingAttachment
		}

		private ControllerMode mode = ControllerMode.Idle;
		private bool holdSelect;
		private float holdTime;

		// Tile state
		private Vector3 startWorld;
		private Variant cursorVariant = new(ResourceManager.DefaultHash);

		// Attachment state
		private int pendingTile = -1;
		private MapAttachment[] selection = null;

		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);

		public EditorControllerCombined(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();

			var tileSelector = Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => mode == ControllerMode.Idle;

			ResetState();
			SetMode(ControllerMode.Idle);
		}

		public override void OnDisable()
		{
			var tileSelector = Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
			{
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;
				tileSelector.CanOpenPalette = () => false;
			}

			ResetState();
			base.OnDisable();
		}

		private void ResetState()
		{
			EditorSelectionUtil.HideGhostMesh();
			EditorTransformUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
			EditorAttachmentUI.ClearPending();

			selection = null;
			pendingTile = -1;
			// NO DeselectTile() here — avoids crash on startup when startWorld is invalid
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

			if (mode is ControllerMode.SelectedAttachment or ControllerMode.DraggingAttachment)
				HandleGizmoInput();

			switch (mode)
			{
				case ControllerMode.Idle:
					HandleIdle(staticClick);
					break;

				case ControllerMode.PlacingTile:
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

				case ControllerMode.SelectedAttachment:
				case ControllerMode.DraggingAttachment:
					HandleAttachmentInput(staticClick);
					break;
			}
		}

		private void HandleIdle(bool staticClick)
		{
			if (InputX.GetMouseButtonDown(0))
			{
				var hitTileIndex = iMap.CameraHitTile(camera, InputX.mousePosition);
				var variant = iMap.CameraHitVariant(camera, InputX.mousePosition);

				if (variant.IsDefaultEquivalent)
				{
					StartPanning();
					return;
				}

				var attachmentsHere = iMap.GetAttachments(hitTileIndex);

				if (attachmentsHere.Length == 0)
				{
					// Tile only
					SelectTile(currentWorld);
				}
				else
				{
					var tile = iMap.GetTile(hitTileIndex);
					if (tile.gameObject == null)
					{
						// Attachments only
						SelectAttachments(attachmentsHere);
					}
					else
					{
						// Both → popup
						pendingTile = hitTileIndex;
						EditorAttachmentUI.RequestSelect();
					}
				}
			}

			if (staticClick)
			{
				if (holdSelect)
				{
					holdSelect = false;
					SelectTile(currentWorld);
				}

				if (holdSelect && Time.time - holdTime >= 0.25f)
				{
					holdSelect = false;
					if (!StartDrag())
						StartPanning();
				}

				if (InputX.GetMouseButtonUp(1))
					iMap.UpdateTileAt(currentWorld, ResourceManager.DefaultHash);
			}
			else if (InputX.GetMouseButton(0) && holdSelect)
			{
				holdSelect = false;
				StartPanning();
			}
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
				return;

			delta.y = cursorVariant.delta.y;
			cursorVariant.delta = delta;
			iMap.RemoveTileAt(startWorld);
			var index = iMap.UpdateTileAt(snapped, cursorVariant);
			if (index == -1) index = iMap.UpdateTileAt(Map.FullFloorVec(startWorld), cursorVariant);
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
			startWorld = cursorVariant.HasNav ? Map.FullFloorVec(worldPos) : Map.HalfFloorVec(worldPos);
			SetMode(ControllerMode.SelectedTile);

			return true;
		}

		private void DeselectTile()
		{
			EditorSelectionUtil.HideGhostMesh();
			var tile = iMap.GetTile(startWorld);
			if (tile.gameObject != null)
				tile.gameObject.SetActive(true);
			SetMode(ControllerMode.Idle);
		}

		// Attachment handlers
		private void HandleAttachmentInput(bool staticClick)
		{
			if (InputX.GetMouseButtonDown(0)) HandleAttachmentLeftDown();
			if (InputX.GetMouseButtonDown(1)) HandleAttachmentRightDown();
			if (InputX.GetMouseButton(0)) HandleAttachmentLeftDrag();

			if (staticClick)
			{
				if (InputX.GetMouseButtonUp(0)) HandleAttachmentLeftUp();
				if (InputX.GetMouseButtonUp(1)) HandleAttachmentRightUp();
			}
		}

		private void HandleAttachmentLeftDown()
		{
			pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);
			var atts = iMap.GetAttachments(pendingTile);

			if (atts.Length == 0)
			{
				StartPanning();
				return;
			}

			SelectAttachments(atts);
		}

		private void HandleAttachmentRightDown()
		{
			pendingTile = iMap.CameraHitTile(camera, InputX.mousePosition);
		}

		private void HandleAttachmentLeftDrag()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
			if (tile == pendingTile || tile < 0 || selection == null || selection.Length == 0)
				return;

			pendingTile = tile;
			foreach (var att in selection)
			{
				att.tile = pendingTile;
				iMap.RefreshAttachment(att);
			}
			RebuildAttachmentMarkers();
		}

		private void HandleAttachmentLeftUp()
		{
			var attsHere = iMap.GetAttachments(pendingTile);
			if (attsHere.Length == 0 && pendingTile >= 0)
			{
				EditorAttachmentUI.RequestAdd();
			}
			else if (attsHere.Length > 1)
			{
				EditorAttachmentUI.RequestSelect();
			}
			else
			{
				EditorAttachmentUI.ClearPending();
				SelectAttachments(attsHere);
			}
			RebuildAttachmentMarkers();
		}

		private void HandleAttachmentRightUp()
		{
			var tile = iMap.CameraHitTile(camera, InputX.mousePosition);
			if (tile >= 0 && iMap.GetAttachments(tile).Length > 0)
			{
				pendingTile = tile;
				EditorAttachmentUI.RequestDelete();
				SelectAttachments(iMap.GetAttachments(pendingTile));
				return;
			}
			SelectAttachments(null);
		}

		private void SelectAttachments(MapAttachment[] atts)
		{
			selection = atts?.Length > 0 ? atts : null;
			EditorTransformUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();

			if (selection?.Length == 1)
			{
				var att = selection[0];
				if (att is ITransformableAttachment t)
				{
					var wPos = iMap.WorldPosition(att.tile, t.Position);
					var wRot = iMap.WorldRotation(att.tile, t.Rotation);
					EditorTransformUtil.ShowAt(wPos, wRot, camera);
				}
				att.OnSelectionChanged(iMap, camera, selection);
			}

			RebuildAttachmentMarkers();

			SetMode(selection != null ? ControllerMode.SelectedAttachment : ControllerMode.Idle);
		}

		private void RebuildAttachmentMarkers()
		{
			if (selection == null || selection.Length == 0)
			{
				EditorMarkerUtil.ClearMapMarkers();
				return;
			}

			var tiles = selection.Select(a => a.tile).Distinct().ToArray();
			var positions = tiles.Select(t => iMap.TileRenderPosition(t)).ToArray();
			var colors = Enumerable.Repeat(new Color(0f, 0.7f, 1f, 0.7f), tiles.Length).ToArray();

			var selectedTile = selection[0].tile;
			var selectedIndex = System.Array.IndexOf(tiles, selectedTile);

			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		protected override void HandleGizmoInput()
		{
			if (selection == null || selection.Length == 0) return;
			var first = selection[0];
			if (!selection.All(a => a.GetType() == first.GetType())) return;
			first.OnGizmoInput(iMap, camera, selection);
		}

		public override void OnGUI()
		{
			base.OnGUI();
			EditorAttachmentUI.UpdateGUI(iMap, selection, atts => SelectAttachments(atts), pendingTile);
		}

		public override void OnDestroy()
		{
			DeselectTile();
			selection = null;
			EditorSelectionUtil.DestroyGhostMesh();
			EditorTransformUtil.Hide();
			EditorMarkerUtil.ClearMapMarkers();
		}
	}
}