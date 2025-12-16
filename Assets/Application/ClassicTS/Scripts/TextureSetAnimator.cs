using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] _frames;
		private MeshRenderer _targetRenderer;
		private Material replacementMaterial;
		private int _currentFrame = 0;
		private float _timer = 0f;

		public delegate void TextureChangedHandler(Texture2D newTexture);
		public event TextureChangedHandler OnTextureChanged;

		[HideInInspector] public bool IsEmissive => MaterialUtils.isEmissive(replacementMaterial);

		public void Initialize(TextureSequence sequence, Material replacement = null)
		{
			_targetRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (_targetRenderer == null || sequence == null || sequence.ResolvedFrames.Length == 0)
			{
				Destroy(this);
				return;
			}

			_frames = sequence.ResolvedFrames;
			replacementMaterial = replacement;

			if (replacement)
			{
				_targetRenderer.material = replacement;

				// Copy tiling/offset FROM original prefab material TO the replacement
				replacement.mainTextureOffset = _targetRenderer.sharedMaterial.mainTextureOffset;
				replacement.mainTextureScale = _targetRenderer.sharedMaterial.mainTextureScale;

				if (MaterialUtils.isEmissive(replacementMaterial))
					replacement.EnableKeyword("_EMISSION");
			}

			_currentFrame = 0;
			_timer = 0f;

			ApplyFrame(0);
		}

		public void ApplyFrame(int index)
		{
			if (_targetRenderer.material == null)
				_targetRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

			if (_frames == null || index < 0 || index >= _frames.Length) return;

			var tex = _frames[index].texture;
			_targetRenderer.material.mainTexture = tex; // replace main texture for animation

			if (MaterialUtils.isEmissive(replacementMaterial))
				_targetRenderer.material.SetTexture("_EmissionMap", tex);

			OnTextureChanged?.Invoke(tex);
		}

		void Update()
		{
			if (_frames.Length <= 1) return;

			_timer += Time.deltaTime;
			if (_timer >= _frames[_currentFrame].duration)
			{
				_timer -= _frames[_currentFrame].duration;
				_currentFrame = (_currentFrame + 1) % _frames.Length;
				ApplyFrame(_currentFrame);
			}
		}

		void OnDestroy()
		{
			//not sure how to handle this - I don't think we allocate replacement any more
			//if (_targetRenderer != null && _targetRenderer.material != null && _targetRenderer.material != _baseMaterial)//_baseMaterial = legacy property
			//	Destroy(_targetRenderer.material);  // Only destroy if we instanced it
		}
	}
}