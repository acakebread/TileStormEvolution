using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public interface IEditorScreenUI
    {
		void OnTileSelected(HashId selectedHash);
		bool CanOpenPalette();
	}

	public class EditorScreenUI : MonoBehaviour, ITileSelectorHandler
	{
		// Only one active handler at a time (editor use-case → simplest)
		private IEditorScreenUI _handler;

		public void Register(IEditorScreenUI handler)
		{
			if (_handler != null && _handler != handler)
			{
				Debug.LogWarning($"IEditorScreenUI: replacing previous handler {handler}");
			}
			_handler = handler;
		}

		public void Unregister(IEditorScreenUI handler)
		{
			if (_handler == handler)
				_handler = null;
		}

		[SerializeField] private TileSelector tileSelector;

		public void Awake()
		{
			if (null != tileSelector) tileSelector.Register(this);
			else Debug.LogError("tileSelector not set in inspector");

			//if (null != UIController.Instance?.tileSelector) TryRegisterTileSelector(UIController.Instance?.tileSelector?.GetComponent<TileSelector>());
			//UIController.OnTileSelectorReady += TryRegisterTileSelector;
			//void TryRegisterTileSelector(TileSelector tileSelector) => tileSelector?.Register(this);
		}

		public bool CanOpenPalette() => _handler == null || _handler.CanOpenPalette();
		public void OnTileSelected(HashId newHash) => _handler?.OnTileSelected(newHash);
	}
}