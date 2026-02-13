using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private HashId selectedHashId;
		private float previewAngle = 0f;
		private Vector3 previewDelta = new Vector3();

		// Selection
		private int? selectedMapIndex = null;
		private Vector3 originalTileScale;
		private (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

		// Dragging
		private bool isDragging;
		private Vector3 tileOriginalWorldPos;
		private Variant tileOriginalVariant;

		private const float SELECTED_SCALE_FACTOR = 1.12f;
		private const float SELECT_TINT_BRIGHTNESS = 1.35f;
		private static readonly Color SELECT_TINT = new Color(1.4f, 1.25f, 0.85f, 1f);

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

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
			tileSelector.CanOpenPalette = () => selectedHashId == ResourceManager.DefaultHash && null == selectedMapIndex;

			selectedHashId = ResourceManager.DefaultHash;
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
			selectedHashId = newHash;
			previewAngle = 0f;
			previewDelta = Vector3.zero;
			DeselectTile();
		}

		public override void Update()
		{
			base.Update();

			if (selectedHashId == ResourceManager.DefaultHash && selectedMapIndex.HasValue)
			{
				EditorMeshUtil.HideGhostMesh();

				if (isDragging)
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
			}
			else
			{
				var def = ResourceManager.GetDefinition(selectedHashId);
				UpdateGhostMesh(camera, iMap, def);
			}
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);

			if (!camera) return;

			// ── Only handle stationary clicks here ─────────────────────────────
			if (!staticClick) return;

			var mouseWorld = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(mouseWorld);
			int hitIndex = iMap.WorldToMapIndex(snapped);

			// ── Mouse DOWN ─────────────────────────────────────────────────────
			if (Input.GetMouseButtonDown(0))
			{
				if (selectedHashId == ResourceManager.DefaultHash)
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
				if (isDragging)
				{
					EndDrag(true);
					return;
				}

				if (selectedHashId == ResourceManager.DefaultHash)
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
				if (selectedHashId == ResourceManager.DefaultHash)
				{
					if (isDragging)
					{
						EndDrag(false);
					}
					else
					{
						EditMapTile(erase: true);
					}
				}
				else
				{
					selectedHashId = ResourceManager.DefaultHash;
					previewAngle = 0f;
					previewDelta = Vector3.zero;
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

			var mouseWorld = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(mouseWorld);

			tile.gameObject.transform.position = snapped + tileOriginalVariant.delta;
		}

		private void EndDrag(bool commit)
		{
			if (!isDragging || !selectedMapIndex.HasValue) return;

			var oldIndex = selectedMapIndex.Value;

			var mouseWorld = Map.ScreenToWorld(camera, Input.mousePosition);
			var newSnapped = Map.SnappedMapPosition(mouseWorld);

			bool shouldMove = newSnapped != tileOriginalWorldPos;

			if (!commit || !shouldMove)
			{
				var tile = iMap.GetTile(oldIndex);
				if (tile.gameObject != null)
					tile.gameObject.transform.position = tileOriginalWorldPos;
			}
			else
			{
				//// Step 1: clear stale selection state BEFORE map changes destroy the object
				//DeselectTile();

				// Step 2: erase old position (destroys old GameObject)
				iMap.UpdateTileAt(tileOriginalWorldPos, ResourceManager.DefaultHash, Vector3.zero, 0f);

				// Step 3: place at new position (creates new GameObject)
				iMap.UpdateTileAt(newSnapped, tileOriginalVariant.hash,
								  tileOriginalVariant.delta, tileOriginalVariant.angle);

				SelectTile(iMap.WorldToMapIndex(newSnapped));//reselect
			}

			isDragging = false;

			//selectedHashId = ResourceManager.DefaultHash;
			//selectedMapIndex = null;
			//var reselect = selectedMapIndex ?? -1;
			//selectedMapIndex = null;
			//SelectTile(reselect);
		}

		private void SelectTile(int mapIndex)
		{
			if (mapIndex == selectedMapIndex) return;
			DeselectTile();

			var tile = iMap.GetTile(mapIndex);
			if (tile.gameObject == null) return;

			selectedMapIndex = mapIndex;

			var allRenderers = tile.gameObject.CollectAllRenderers(true);
			if (allRenderers.Length == 0) return;

			originalRenderersState = new (Renderer, Material[])?[allRenderers.Length];

			for (int i = 0; i < allRenderers.Length; i++)
			{
				var rend = allRenderers[i];
				if (rend == null) continue;

				var originals = rend.sharedMaterials;
				if (originals == null || originals.Length == 0) continue;

				originalRenderersState[i] = (rend, (Material[])originals.Clone());

				var tinted = new Material[originals.Length];
				for (int m = 0; m < originals.Length; m++)
				{
					if (originals[m] == null) { tinted[m] = null; continue; }
					var copy = new Material(originals[m]);
					copy.color = originals[m].color * SELECT_TINT * SELECT_TINT_BRIGHTNESS;
					tinted[m] = copy;
				}
				rend.materials = tinted;
			}

			originalTileScale = tile.gameObject.transform.localScale;
			tile.gameObject.transform.localScale = originalTileScale * SELECTED_SCALE_FACTOR;
		}

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

			if (originalRenderersState == null)
			{
				selectedMapIndex = null;
				return;
			}

			var allRenderers = tile.gameObject.CollectAllRenderers(true);

			for (int i = 0; i < originalRenderersState.Length && i < allRenderers.Length; i++)
			{
				var backup = originalRenderersState[i];
				if (!backup.HasValue) continue;
				var (expected, mats) = backup.Value;
				if (allRenderers[i] != expected || mats == null) continue;
				allRenderers[i].materials = mats;
			}

			tile.gameObject.transform.localScale = originalTileScale;

			originalRenderersState = null;
			selectedMapIndex = null;
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null) { EditorMeshUtil.HideGhostMesh(); return; }

			var worldPos = Map.ScreenToWorld(cam, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			var mapIndex = map.WorldToMapIndex(snapped);

			previewAngle = 0f;
			previewDelta = Vector3.zero;

			if (mapIndex != -1)
			{
				var current = map.GetVariantAt(mapIndex);
				var selDef = ResourceManager.GetDefinition(selectedHashId);
				bool isDefault = selDef?.IsDefault() ?? false;

				if (current.hash != 0 && !isDefault && current.hash == selectedHashId)
				{
					float[] angles = { 0f, 90f, 180f, 270f };
					float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

					int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;
					int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;

					aIdx = (aIdx + 1) % angles.Length;
					if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

					previewAngle = angles[aIdx];
					previewDelta = new Vector3(current.delta.x, deltas[dIdx], current.delta.z);
				}
			}

			bool oob = mapIndex == -1;
			var variant = new Variant(selectedHashId, previewDelta, previewAngle);
			EditorMeshUtil.UpdateGhostMesh(variant, snapped, oob);
		}

		private void EditMapTile(bool erase = false)
		{
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			var snapped = Map.SnappedMapPosition(worldPos);
			int idx = iMap.WorldToMapIndex(snapped);

			if (idx == -1 || erase)
			{
				var hash = erase ? ResourceManager.DefaultHash : selectedHashId;
				iMap.UpdateTileAt(snapped, hash, Vector3.zero, 0f);
				return;
			}

			iMap.UpdateTileAt(snapped, selectedHashId, previewDelta, previewAngle);
		}

		public override void OnDestroy()
		{
			if (isDragging) EndDrag(false);
			DeselectTile();
			EditorMeshUtil.DestroyGhostMesh();
		}
	}
}