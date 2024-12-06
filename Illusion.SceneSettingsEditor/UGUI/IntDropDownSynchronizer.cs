using System;
using UnityEngine;
using UnityEngine.UI;

namespace Illusion.UGUI
{
	public class IntDropDownSynchronizer : MonoBehaviour
	{
		private Dropdown _dropdown;
		private int _previousValue;
		private Func<int> _checkFunc;
		private Action<int> _onValueChanged;
		private bool _isSyncing;
		public static IntDropDownSynchronizer AddMonitor(Dropdown dropDown, Func<int> onCheckFunc, Action<int> onValueChangedAction)
		{
			var valueMonitor = dropDown.gameObject.AddComponent<IntDropDownSynchronizer>();
			dropDown.onValueChanged.AddListener(valueMonitor.OnDropdownValueChanged);
			valueMonitor._dropdown = dropDown;
			valueMonitor._checkFunc = onCheckFunc;
			valueMonitor._onValueChanged = onValueChangedAction;
			return valueMonitor;
		}

		public void OnDropdownValueChanged(int value)
		{
			if (_isSyncing)
			{
				return;
			}

			int.TryParse(_dropdown.options[value].text, out var newValue);
			_onValueChanged.Invoke(newValue);
		}

		public void Update()
		{
			var value = _checkFunc.Invoke();
			if (value.Equals(_previousValue))
			{
				return;
			}

			_isSyncing = true;
			_dropdown.value = _dropdown.options.FindIndex(r => r.text.Equals(value.ToString()));
			_dropdown.RefreshShownValue();
			_previousValue = value;
			_isSyncing = false;
		}
	}
}