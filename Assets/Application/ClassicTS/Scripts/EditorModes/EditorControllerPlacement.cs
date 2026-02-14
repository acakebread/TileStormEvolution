using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public enum EditorMode
	{
		Idle,       // nothing selected, no placement active → can select, pan, start placing
		Placing,    // active tile placement from palette (ghost visible)
		Selected,   // tile is highlighted/selected → can start drag or deselect
		Dragging    // actively dragging a selected tile (mouse button held)
	}

	public class EditorControllerPlacement : EditorControllerMovement
	{
		private Variant placementVariant = new Variant(ResourceManager.DefaultHash);
		private EditorMode mode = EditorMode.Idle;

		// Selection
		private Vector3 selectedMapPos;
		private (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		public EditorControllerPlacement(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();

			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector == null)
			{
				Debug.LogError("TileSelector not found!");
				return;
			}

			tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			tileSelector.CanOpenPalette = () => mode == EditorMode.Idle;

			placementVariant = new Variant(ResourceManager.DefaultHash);
			mode = EditorMode.Idle;
		}

		public override void OnDisable()
		{
			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;

			if (mode == EditorMode.Dragging) EndDrag(false);
			DeselectTile();

			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			DeselectTile();
			placementVariant = new Variant(newHash);
			mode = (newHash != ResourceManager.DefaultHash) ? EditorMode.Placing : EditorMode.Idle;
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			// ── Ghost & drag handling ───────────────────────────────────────
			if (mode == EditorMode.Placing)
			{
				if (placementVariant.hash == ResourceManager.DefaultHash)
				{
					EditorMeshUtil.HideGhostMesh();
				}
				else
				{
					var def = ResourceManager.GetDefinition(placementVariant.hash);
					UpdateGhostMesh(camera, iMap, def);
				}
			}
			else
			{
				EditorMeshUtil.HideGhostMesh();
			}

			// Handle active drag movement
			if (mode == EditorMode.Dragging)
			{
				if (Input.GetMouseButton(0))
				{
					UpdateDragPosition();
				}
				else
				{
					EndDrag(true);
				}
			}

			if (!camera) return;
			if (!staticClick) return;

			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			int hitIndex = iMap.WorldToMapIndex(snapped);

			// ── Mouse DOWN ─────────────────────────────────────────────────────
			if (Input.GetMouseButtonDown(0))
			{
				if (mode == EditorMode.Dragging)
				{
					// should not happen - already handled above
					return;
				}

				if (mode != EditorMode.Placing || placementVariant.hash == ResourceManager.DefaultHash)
				{
					if (mode == EditorMode.Selected)
					{
						int selectedIndex = iMap.WorldToMapIndex(selectedMapPos);
						if (hitIndex == selectedIndex)
						{
							var tile = iMap.GetTile(selectedIndex);
							if (tile.gameObject != null)
							{
								StartDrag();
								return;
							}
						}
						else
						{
							DeselectTile();
							return;
						}
					}
				}

				// Normal panning on empty/default
				var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
				var selDef = ResourceManager.GetDefinition(variant.hash);
				if (selDef?.IsDefaultEquivalent() ?? true)
				{
					StartPanning();
				}
			}

			// ── Mouse UP ───────────────────────────────────────────────────────
			if (Input.GetMouseButtonUp(0))
			{
				if (mode == EditorMode.Dragging)
				{
					EndDrag(true);
					return;
				}

				if (mode != EditorMode.Placing || placementVariant.hash == ResourceManager.DefaultHash)
				{
					if (hitIndex == -1)
					{
						DeselectTile();
						return;
					}

					var currentVariant = iMap.GetVariantAt(hitIndex);
					var currentDef = ResourceManager.GetDefinition(currentVariant.hash);

					if (currentDef != null && !currentDef.IsDefaultEquivalent())
					{
						SelectTile(snapped);
						return;
					}
					else
					{
						DeselectTile();
					}
				}
				else
				{
					EditMapTile();
				}
			}

			if (Input.GetMouseButtonUp(1))
			{
				DeselectTile();

				if (mode == EditorMode.Placing && placementVariant.hash != ResourceManager.DefaultHash)
				{
					placementVariant = new Variant(ResourceManager.DefaultHash);
					mode = EditorMode.Idle;
				}
				else if (mode == EditorMode.Dragging)
				{
					EndDrag(false);
				}
				else
				{
					EditMapTile(erase: true);
				}
			}
		}

		private void StartDrag()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			placementVariant = iMap.GetVariantAt(index);
			mode = EditorMode.Dragging;
		}

		private void UpdateDragPosition()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);
			if (tile.gameObject == null) return;

			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			tile.gameObject.transform.position = snapped + placementVariant.delta;
		}

		private void EndDrag(bool commit)
		{
			var newSnapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			var shouldMove = newSnapped !=- selectedMapPos;

			if (!commit || !shouldMove)
			{
				var oldIndex = iMap.WorldToMapIndex(selectedMapPos);
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = selectedMapPos;
			}
			else
			{
				RestoreSelectedTile(); // remove highlight
				iMap.RemoveTileAt(selectedMapPos);
				iMap.UpdateTileAt(newSnapped, placementVariant.hash, placementVariant.delta, placementVariant.angle);
			}

			SelectTile(newSnapped + Map.OriginDelta);// this is here until click and drag starts working properly and then the mode will be set to idle
			//mode = EditorMode.Idle;
		}

		private void SelectTile(Vector3 worldPos)
		{
			int mapIndex = iMap.WorldToMapIndex(worldPos);

			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			selectedMapPos = worldPos;
			mode = EditorMode.Selected;

			const float SELECT_TINT_BRIGHTNESS = 1.35f;
			Color SELECT_TINT = new Color(1.4f, 1.25f, 0.85f, 1f);

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);
		}

		private void DeselectTile()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);

			if (null != tile.gameObject)
				tile.gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
			mode = EditorMode.Idle;
		}

		private void RestoreSelectedTile()
		{
			if (mode != EditorMode.Selected && mode != EditorMode.Dragging) return;

			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);

			if (tile.gameObject == null)
				return;

			tile.gameObject.RestoreSelectionHighlight(originalRenderersState);
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null) { EditorMeshUtil.HideGhostMesh(); return; }

			var snapped = Map.ScreenToWorldSnapped(cam, Input.mousePosition);
			var mapIndex = map.WorldToMapIndex(snapped);

			placementVariant.delta = Vector3.zero;
			placementVariant.angle = 0f;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);
				var selDef = ResourceManager.GetDefinition(placementVariant.hash);
				var isDefault = selDef?.IsDefault() ?? false;

				if (current.hash != 0 && !isDefault && current.hash == placementVariant.hash)
				{
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;
					int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					placementVariant.delta = new Vector3(current.delta.x, deltas[dIdx], current.delta.z);
					placementVariant.angle = angles[aIdx];
				}
			}

			EditorMeshUtil.UpdateGhostMesh(placementVariant, snapped, mapIndex == -1);
		}

		private void EditMapTile(bool erase = false)
		{
			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			iMap.UpdateTileAt(snapped,
							  erase ? ResourceManager.DefaultHash : placementVariant.hash,
							  placementVariant.delta,
							  placementVariant.angle);
		}

		public override void OnDestroy()
		{
			if (mode == EditorMode.Dragging) EndDrag(false);
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}