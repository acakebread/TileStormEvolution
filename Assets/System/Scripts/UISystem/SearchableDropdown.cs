using System;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	public class SearchableDropdown : MonoBehaviour
	{
		[SerializeField] private TMP_InputField searchInput;
		[SerializeField] private TMP_Dropdown dropdown;

		private List<TMP_Dropdown.OptionData> allOptions;

		void Start()
		{
			allOptions = new List<TMP_Dropdown.OptionData>(dropdown.options);
			searchInput.onValueChanged.AddListener(FilterOptions);
		}

		void FilterOptions(string filter)
		{
			dropdown.options = allOptions
				.Where(opt => string.IsNullOrEmpty(filter) || opt.text.Contains(filter, StringComparison.OrdinalIgnoreCase))
				.ToList();

			dropdown.RefreshShownValue();  // important to update display
		}
	}
}