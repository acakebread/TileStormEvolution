using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public interface IEditorScreenUI
    {
		void OnAltitudeChanged(float value);
	}

	public class EditorScreenUI : MonoBehaviour
	{
		private IEditorScreenUI _receiver;

		internal IEditorScreenUI Receiver
		{
			get => _receiver;
			set => _receiver = value;
		}

		[SerializeField] private UnityEngine.UI.Slider altitudeSlider;

		public void Awake()
		{
			altitudeSlider?.onValueChanged.AddListener(value => _receiver?.OnAltitudeChanged(value * 0.2f));

			ReadyCallbackRegistry.Raise(this);
		}
	}
}