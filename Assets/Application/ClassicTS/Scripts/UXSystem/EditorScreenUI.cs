using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public interface IEditorScreenUI
    {
		void OnAltitudeChanged(float value);
	}

	public class EditorScreenUI : UIPanel
	{
		private IEditorScreenUI _receiver;

		internal IEditorScreenUI Receiver
		{
			get => _receiver;
			set => _receiver = value;
		}

		[SerializeField] private UnityEngine.UI.Slider altitudeSlider;

		protected override void Awake()
		{
			base.Awake();
			altitudeSlider?.onValueChanged.AddListener(value => _receiver?.OnAltitudeChanged(value * 0.2f));

			ReadyCallbackRegistry.Raise(this);
		}
	}
}