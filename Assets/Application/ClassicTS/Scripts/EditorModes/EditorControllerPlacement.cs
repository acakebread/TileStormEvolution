using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPlacement : EditorControllerMovement
	{
		private Variant placementVariant = default;

		// Dragging
		private Variant tileOriginalVariant;
		private Vector3 tileOriginalWorldPos;
		private bool isDragging;

		// Selection
		private int? selectedMapIndex = null;
		private (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		private const float SELECT_TINT_BRIGHTNESS = 1.35f;
		private static readonly Color SELECT_TINT = new(1.4f, 1.25f, 0.85f, 1f);

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
			tileSelector.CanOpenPalette = () => placementVariant.hash == ResourceManager.DefaultHash && null == selectedMapIndex;

			placementVariant = new Variant(ResourceManager.DefaultHash);
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
			DeselectTile();
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			if (placementVariant.hash == ResourceManager.DefaultHash && selectedMapIndex.HasValue)
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

			if (!camera) return;

			// ── Only handle stationary clicks here ─────────────────────────────
			if (!staticClick) return;
			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			int hitIndex = iMap.WorldToMapIndex(snapped);

			// ── Mouse DOWN ─────────────────────────────────────────────────────
			if (Input.GetMouseButtonDown(0))
			{
				if (placementVariant.hash == ResourceManager.DefaultHash)
				{
					if (selectedMapIndex.HasValue)
					{
						if (hitIndex == selectedMapIndex.Value)
						{
							var tile = iMap.GetTile(selectedMapIndex.Value);
							if (tile.gameObject != null)
							{
								var tilePos = tile.gameObject.transform.position;
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
				if (placementVariant.hash == ResourceManager.DefaultHash)
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
						SelectTile(hitIndex);
						return;
					}
					else
						DeselectTile();
				}
				else
					EditMapTile();
			}

			if (Input.GetMouseButtonUp(1))
			{
				DeselectTile();
				if (placementVariant.hash == ResourceManager.DefaultHash)
				{
					if (isDragging)
						EndDrag(false);
					else
						EditMapTile(erase: true);
				}
				else
				{
					placementVariant = new Variant(ResourceManager.DefaultHash);
				}
			}
		}

		private void StartDrag()
		{
			var tile = iMap.GetTile(selectedMapIndex.Value);
			if (tile.gameObject == null) return;

			isDragging = true;
			tileOriginalWorldPos = tile.gameObject.transform.position;
			tileOriginalVariant = iMap.GetVariantAt(selectedMapIndex.Value);
		}

		private void UpdateDragPosition()
		{
			var tile = iMap.GetTile(selectedMapIndex.Value);
			if (tile.gameObject == null) return;

			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			tile.gameObject.transform.position = snapped + tileOriginalVariant.delta;
		}

		private void EndDrag(bool commit)
		{
			if (!isDragging || !selectedMapIndex.HasValue) return;

			var oldIndex = selectedMapIndex.Value;

			var newSnapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			bool shouldMove = newSnapped != tileOriginalWorldPos;

			if (!commit || !shouldMove)
			{
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = tileOriginalWorldPos;
			}
			else
			{
				iMap.UpdateTileAt(tileOriginalWorldPos, ResourceManager.DefaultHash, Vector3.zero, 0f);

				iMap.UpdateTileAt(newSnapped, tileOriginalVariant.hash,
								  tileOriginalVariant.delta, tileOriginalVariant.angle);

				SelectTile(iMap.WorldToMapIndex(newSnapped)); // reselect
			}

			isDragging = false;
		}

		// In SelectTile:
		private void SelectTile(int mapIndex)
		{
			if (mapIndex == selectedMapIndex) return;
			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			selectedMapIndex = mapIndex;

			originalRenderersState = tile.gameObject.ApplySelectionHighlight(
				SELECT_TINT,
				SELECT_TINT_BRIGHTNESS,
				includeInactive: true);
		}

		// In DeselectTile:
		private void DeselectTile()
		{
			if (isDragging) EndDrag(false);

			if (!selectedMapIndex.HasValue) return;

			var tile = iMap.GetTile(selectedMapIndex.Value);
			if (tile.gameObject == null)
			{
				selectedMapIndex = null;
				return;
			}

			tile.gameObject.RestoreSelectionHighlight(originalRenderersState);

			originalRenderersState = null;
			selectedMapIndex = null;
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null) { EditorMeshUtil.HideGhostMesh(); return; }

			var snapped = Map.ScreenToWorldSnapped(camera, Input.mousePosition);
			var mapIndex = map.WorldToMapIndex(snapped);

			placementVariant.delta = Vector3.zero;
			placementVariant.angle  = 0f;

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
			iMap.UpdateTileAt(snapped, erase ? ResourceManager.DefaultHash : placementVariant.hash, placementVariant.delta, placementVariant.angle);
		}

		public override void OnDestroy()
		{
			if (isDragging) EndDrag(false);
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}