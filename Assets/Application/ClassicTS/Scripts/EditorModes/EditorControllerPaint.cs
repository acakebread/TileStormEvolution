using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorControllerPaint : EditorControllerMovement
	{
		private TileSelector _tileSelector;

		private HashId selectedHashId;
		private float previewAngle = 0f;
		private Vector3 previewDelta = new();

		public EditorControllerPaint(EditorController editorController) : base(editorController) { }

		public override void OnEnable()
		{
			base.OnEnable();

			_tileSelector = UnityEngine.Object.FindAnyObjectByType<TileSelector>(FindObjectsInactive.Include);
			if (_tileSelector == null)
			{
				Debug.LogError("TileSelector not found in scene!");
				return;
			}

			//_tileSelector.Initialize();

			// Subscribe to tile selection event
			_tileSelector.OnTileSelected += OnTileSelectedFromPalette;
			_tileSelector.CanOpenPalette = () => selectedHashId == _tileSelector.DefaultHash;

			selectedHashId = _tileSelector.DefaultHash;
		}

		public override void OnDisable()
		{
			if (_tileSelector != null)
				_tileSelector.OnTileSelected -= OnTileSelectedFromPalette;

			base.OnDisable();
		}

		private void OnTileSelectedFromPalette(HashId newHash)
		{
			selectedHashId = newHash;
			previewAngle = 0f;
			previewDelta = Vector3.zero;
		}

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			bool mouseOverPaletteY = _tileSelector.IsMouseOverPalette();
			if (!mouseOverPaletteY && Input.GetMouseButtonDown(0))
			{
				var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
				var selDef = ResourceManager.GetDefinition(variant.hash);
				var isDefault = selDef?.IsDefaultEquivalent() ?? true;
				if (isDefault)
					StartPanning();
			}

			if (IsMouseOverGUI() || IsGuiControlActive()) return;

			var def = ResourceManager.GetDefinition(selectedHashId);
			UpdateGhostMesh(camera, iMap, def);
		}

		protected override void OnControl(bool staticClick)
		{
			base.OnControl(staticClick);
			if (!staticClick) return;

			if (Input.GetMouseButtonUp(0) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
			{
				if (selectedHashId == _tileSelector.DefaultHash)
				{
					var variant = iMap.CameraHitVariant(camera, Input.mousePosition);
					var selDef = ResourceManager.GetDefinition(variant.hash);
					var isDefault = selDef?.IsDefaultEquivalent() ?? true;
					if (!isDefault)
					{
						selectedHashId = variant.hash;
						previewAngle = variant.angle;
						previewDelta = variant.delta;
					}
				}
				else
				{
					EditMapTile();
				}
			}

			if (Input.GetMouseButtonUp(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) < 5f)
			{
				if (selectedHashId == _tileSelector.DefaultHash)
					EditMapTile(erase: true);
				else
				{
					selectedHashId = _tileSelector.DefaultHash;
					previewAngle = 0f;
					previewDelta = Vector3.zero;
				}
			}
		}

		private void UpdateGhostMesh(Camera cam, IMapEdit map, Definition def)
		{
			if (def == null)
			{
				EditorMeshUtil.HideGhostMesh();
				return;
			}

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
			int px = Mathf.FloorToInt(snapped.x);
			int pz = Mathf.FloorToInt(snapped.z);
			int idx = iMap.WorldToMapIndex(snapped);

			if (idx == -1 || erase)
			{
				var hash = erase ? _tileSelector.DefaultHash : selectedHashId;
				iMap.UpdateTileAt(px, pz, hash, Vector3.zero, 0f);
				return;
			}

			iMap.UpdateTileAt(px, pz, selectedHashId, previewDelta, previewAngle);
		}

		public override void OnDestroy() => EditorMeshUtil.DestroyGhostMesh();

		protected override bool IsMouseOverGUI() => base.IsMouseOverGUI() || _tileSelector.IsMouseOverPalette();
	}
}