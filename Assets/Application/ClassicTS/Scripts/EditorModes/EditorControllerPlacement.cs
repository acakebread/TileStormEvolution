using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPlacement : EditorControllerMovement
	{
		private enum ControllerMode
		{
			Idle,       // nothing selected, no placement active → can select, pan, start placing
			Placing,    // active tile placement from palette (ghost visible)
			Selected,   // tile is highlighted/selected → can start drag or deselect
			Dragging    // actively dragging a selected tile (mouse button held)
		}

		private ControllerMode mode = ControllerMode.Idle;
		private bool holdSelect;
		private float holdTime;

		// Selection
		private Vector3 startWorld;
		private Vector3 currentWorld => Map.ScreenToWorld(camera, InputX.mousePosition);

		private Variant cursorVariant = new(ResourceManager.DefaultHash);

		public EditorControllerPlacement(EditorController editorController) : base(editorController) { }

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

			DeselectTile();
			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			DeselectTile();
			cursorVariant = new Variant(newHash);
			SetMode((newHash != ResourceManager.DefaultHash) ? ControllerMode.Placing : ControllerMode.Idle);
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
						var variant = iMap.CameraHitVariant(camera, InputX.mousePosition);
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
							if (holdSelect)
							{
								holdSelect = false;
								SelectTile(currentWorld);
							}
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

				case ControllerMode.Placing:
					// Continuous ghost update in placing mode
					cursorVariant = EditorSelectionUtil.NextVariantOnMap(iMap, currentWorld, cursorVariant);
					EditorSelectionUtil.UpdateGhostMesh(iMap, Map.FullFloorVec(currentWorld), cursorVariant, false);

					if (staticClick)
					{
						if (InputX.GetMouseButtonUp(0))
							iMap.UpdateTileAt(currentWorld, cursorVariant.hash, cursorVariant.delta, cursorVariant.angle);

						if (InputX.GetMouseButtonUp(1))
						{
							EditorSelectionUtil.HideGhostMesh();
							SetMode(ControllerMode.Idle);
						}
					}
					break;

				case ControllerMode.Selected:
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

				case ControllerMode.Dragging:
					if (InputX.GetMouseButtonDown(0))
					{
						if (!StartDrag())
							SetMode(ControllerMode.Idle);
					}

					if (InputX.GetMouseButton(0))
						UpdateDrag();

					if (InputX.GetMouseButtonUp(0))
					{
						SetMode(ControllerMode.Selected);
						EndDrag();
					}
					break;
			}
		}

		private bool StartDrag()
		{
			if (!SelectTile(currentWorld))
				return false;
			SetMode(ControllerMode.Dragging);
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
			var startVariant = iMap.GetVariantAt(startWorld);
			var worldPos = Map.FullFloorVec(startWorld) + currentWorld - startWorld + startVariant.delta;
			var snapped = Map.FullFloorVec(worldPos);
			var delta = startVariant.HasNav ? Vector3.zero : Map.HalfFloorVec(worldPos) - snapped;

			if (snapped == Map.FullFloorVec(startWorld) && delta == startVariant.delta)
				return;//no change so ok to just exit

			delta.y = startVariant.delta.y;//retore old delta height
			startVariant.delta = delta;
			iMap.RemoveTileAt(startWorld);
			var index = iMap.UpdateTileAt(snapped, startVariant);
			if (-1 == index) index = iMap.UpdateTileAt(Map.FullFloorVec(startWorld), startVariant);//operation failed restore old tile
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
			startWorld = cursorVariant.HasNav ? Map.FullFloorVec(worldPos) : Map.HalfFloorVec(worldPos);
			SetMode(ControllerMode.Selected);

			return true;
		}

		public override void OnDestroy()
		{
			DeselectTile();
			EditorSelectionUtil.DestroyGhostMesh();
		}
	}
}