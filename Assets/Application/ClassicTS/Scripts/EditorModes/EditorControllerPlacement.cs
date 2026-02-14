using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPlacement : EditorControllerMovement
	{
		private Variant placementVariant = new Variant(ResourceManager.DefaultHash);
		private bool isInPlacementMode = false;   // true = placing specific tile from palette, false = selection/drag/erase/idle
		private bool isInDraggingMode = false;    // ← NEW: replaces selectedMapPos null-check for "something is selected"

		// Dragging
		private Vector3 tileOriginalWorldPos;
		private bool isDragging;

		// Selection — now always valid Vector3 when something is selected
		private Vector3 selectedMapPos;                    // ← CHANGED: no longer nullable
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
			tileSelector.CanOpenPalette = () => !isInPlacementMode && !isInDraggingMode;   // ← CHANGED

			placementVariant = new Variant(ResourceManager.DefaultHash);
			isInPlacementMode = false;
			DeselectTile();
		}

		public override void OnDisable()
		{
			var tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (tileSelector != null)
				tileSelector.OnTileSelected -= OnTileSelectedFromPalette;

			if (isDragging) EndDrag(false);
			DeselectTile();

			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			placementVariant = new Variant(newHash);
			isInPlacementMode = (newHash != ResourceManager.DefaultHash);
			DeselectTile();
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			// ── Ghost & drag handling ───────────────────────────────────────
			if (isInPlacementMode)
			{
				if (placementVariant.hash == ResourceManager.DefaultHash)
				{
					EditorMeshUtil.HideGhostMesh();

					if (isDragging)
					{
						if (Input.GetMouseButton(0))
							UpdateDragPosition();
						else
							EndDrag(true);
					}
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

				if (isDragging)
				{
					if (Input.GetMouseButton(0))
						UpdateDragPosition();
					else
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
				if (!isInPlacementMode || placementVariant.hash == ResourceManager.DefaultHash)
				{
					if (isInDraggingMode)   // ← CHANGED
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
				if (!isInPlacementMode || placementVariant.hash == ResourceManager.DefaultHash)
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
						SelectTile(snapped);   // pass position
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

				if (isInPlacementMode && placementVariant.hash != ResourceManager.DefaultHash)
				{
					placementVariant = new Variant(ResourceManager.DefaultHash);
					isInPlacementMode = false;
				}
				else
				{
					if (isDragging)
						EndDrag(false);
					else
						EditMapTile(erase: true);
				}
			}
		}

		private void StartDrag()
		{
			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);
			if (tile.gameObject == null) return;

			isDragging = true;
			tileOriginalWorldPos = tile.gameObject.transform.position;
			placementVariant = iMap.GetVariantAt(index);
			isInPlacementMode = false;
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
			if (!isDragging || !isInDraggingMode) return;   // ← CHANGED

			int oldIndex = iMap.WorldToMapIndex(selectedMapPos);
			var newSnapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			bool shouldMove = (newSnapped - tileOriginalWorldPos).sqrMagnitude > 0.001f;

			if (!commit || !shouldMove)
			{
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = tileOriginalWorldPos;
			}
			else
			{
				RestoreSelectedTile(); // remove highlight
				iMap.RemoveTileAt(tileOriginalWorldPos);
				iMap.UpdateTileAt(newSnapped, placementVariant.hash, placementVariant.delta, placementVariant.angle);
				newSnapped += Map.OriginDelta;
				selectedMapPos = Vector3.negativeInfinity;          // this is effectively treated as a flag by select tile so it needs to be reset - we can deal with this later
				SelectTile(newSnapped);               // re-highlight at new position
			}

			isDragging = false;
			//these lines are not needed
			//isInPlacementMode = false;
			//placementVariant = new Variant(ResourceManager.DefaultHash);
		}

		private void SelectTile(Vector3 worldPos)
		{
			int mapIndex = iMap.WorldToMapIndex(worldPos);

			if (isInDraggingMode)   // ← CHANGED
			{
				int oldIndex = iMap.WorldToMapIndex(selectedMapPos);
				if (mapIndex == oldIndex) return;
			}

			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			selectedMapPos = worldPos;
			isInDraggingMode = true;   // ← NEW: set flag

			const float SELECT_TINT_BRIGHTNESS = 1.35f;
			Color SELECT_TINT = new Color(1.4f, 1.25f, 0.85f, 1f);

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);

			isInPlacementMode = false;
		}

		private void DeselectTile()
		{
			if (isDragging) EndDrag(false);

			if (!isInDraggingMode) return;   // ← CHANGED

			int index = iMap.WorldToMapIndex(selectedMapPos);
			var tile = iMap.GetTile(index);

			if (tile.gameObject == null)
			{
				isInDraggingMode = false;
				return;
			}

			tile.gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
			isInDraggingMode = false;   // ← CHANGED
			isInPlacementMode = false;
		}

		private void RestoreSelectedTile()
		{
			if (!isInDraggingMode) return;   // ← CHANGED

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
			if (isDragging) EndDrag(false);
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}