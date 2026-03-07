using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public interface IEditorScreenUI
    {
		void OnTileSelected(HashId selectedHash);
		bool CanOpenPalette();
		void OnAltitudeChanged(float value);
	}

	public class EditorScreenUI : MonoBehaviour, ITileSelectorHandler
	{
		private IEditorScreenUI _handler;// Only one active handler at a time

		public void Register(IEditorScreenUI handler)
		{
			if (_handler != null && _handler != handler)
				Debug.LogWarning($"IEditorScreenUI: replacing previous handler {handler}");
			_handler = handler;
		}

		public void Unregister(IEditorScreenUI handler)
		{
			if (_handler == handler)
				_handler = null;
		}

		[SerializeField] private TileSelector tileSelector;
		[SerializeField] private UnityEngine.UI.Slider altitudeSlider;

		public void Awake()
		{
			if (null != tileSelector) tileSelector.Register(this);// we don't really need the register system in TileSelector any more but leave for now
			else Debug.LogError("tileSelector not set in inspector");

			altitudeSlider?.onValueChanged.AddListener((value) => _handler?.OnAltitudeChanged(value * 0.2f));
		}

		public bool CanOpenPalette() => _handler == null || _handler.CanOpenPalette();
		public void OnTileSelected(HashId newHash) => _handler?.OnTileSelected(newHash);
	}
}