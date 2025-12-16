using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] _frames;
		private MeshRenderer _targetRenderer;
		private Material _replacementMaterial;  // renamed for clarity
		private int _currentFrame = 0;
		private float _timer = 0f;

		public delegate void TextureChangedHandler(Texture2D newTexture);
		public event TextureChangedHandler OnTextureChanged;

		[HideInInspector] public bool IsEmissive => _replacementMaterial != null && MaterialUtils.isEmissive(_replacementMaterial);

		public void Initialize(TextureSequence sequence, Material replacement = null)
		{
			_targetRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (_targetRenderer == null || sequence == null || sequence.ResolvedFrames.Length == 0)
			{
				Destroy(this);
				return;
			}

			_frames = sequence.ResolvedFrames;
			_replacementMaterial = replacement;

			if (replacement != null)
			{
				// Apply replacement material (this instances it)
				_targetRenderer.material = replacement;

				// Preserve original tiling/offset from prefab
				replacement.mainTextureOffset = _targetRenderer.sharedMaterial.mainTextureOffset;
				replacement.mainTextureScale = _targetRenderer.sharedMaterial.mainTextureScale;

				if (MaterialUtils.isEmissive(replacement))
					replacement.EnableKeyword("_EMISSION");
			}

			_currentFrame = 0;
			_timer = 0f;

			ApplyFrame(0);
		}

		public void ApplyFrame(int index)
		{
			if (_targetRenderer.material == null) return;
			if (_frames == null || index < 0 || index >= _frames.Length) return;

			var tex = _frames[index].texture;

			// Always animate main texture
			_targetRenderer.material.mainTexture = tex;

			// If using a replacement material that's emissive, also animate emission map
			if (_replacementMaterial != null && MaterialUtils.isEmissive(_replacementMaterial))
			{
				_targetRenderer.material.SetTexture("_EmissionMap", tex);
			}

			OnTextureChanged?.Invoke(tex);
		}

		void Update()
		{
			if (_frames == null || _frames.Length <= 1) return;

			_timer += Time.deltaTime;
			if (_timer >= _frames[_currentFrame].duration)
			{
				_timer -= _frames[_currentFrame].duration;
				_currentFrame = (_currentFrame + 1) % _frames.Length;
				ApplyFrame(_currentFrame);
			}
		}

		// Safe to leave empty — no manual material allocation
		// Unity handles cleanup of instanced materials
		// void OnDestroy() { }
	}
}