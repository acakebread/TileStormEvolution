using UnityEngine;

namespace ClassicTilestorm
{
	public class TextureSetAnimator : MonoBehaviour
	{
		private TextureFrame[] _frames;
		private MeshRenderer _targetRenderer;
		private int _currentFrame = 0;
		private float _timer = 0f;

		public delegate void TextureChangedHandler(Texture2D newTexture);
		public event TextureChangedHandler OnTextureChanged;

		public void Initialize(TextureSequence sequence)
		{
			_targetRenderer = GetComponentInChildren<MeshRenderer>(true);
			if (_targetRenderer == null || sequence == null || sequence.ResolvedFrames.Length == 0)
			{
				Destroy(this);
				return;
			}

			_frames = sequence.ResolvedFrames;
			_currentFrame = 0;
			_timer = 0f;
			ApplyFrame(0);
		}

		public void ApplyFrame(int index)
		{
			if (_targetRenderer.material == null)
				_targetRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

			var tex = _frames[index].texture;
			_targetRenderer.material.mainTexture = tex;
			OnTextureChanged?.Invoke(tex);
		}

		void Update()
		{
			if (_frames.Length <= 1) return;

			_timer += Time.deltaTime;
			if (_timer >= _frames[_currentFrame].fDuration)
			{
				_timer -= _frames[_currentFrame].fDuration;
				_currentFrame = (_currentFrame + 1) % _frames.Length;
				ApplyFrame(_currentFrame);
			}
		}

		void OnDestroy()
		{
			if (_targetRenderer != null && _targetRenderer.material != null)
				Destroy(_targetRenderer.material);
		}
	}
}