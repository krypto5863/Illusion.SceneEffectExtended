using System;
using UnityEngine;
using UnityEngine.UI;

namespace Illusion.UGUI
{
	public class EnumDropDownSynchronizer : MonoBehaviour
	{
		private Dropdown _dropdown;
		private Enum _previousValue;
		private Func<Enum> _checkFunc;
		private Action<Enum> _onValueChanged;
		private Type _enumType;
		private bool _isSyncing;
		public static EnumDropDownSynchronizer AddMonitor(Dropdown dropDown, Type enumType, Func<Enum> onCheckFunc, Action<Enum> onValueChangedAction)
		{
			var valueMonitor = dropDown.gameObject.AddComponent<EnumDropDownSynchronizer>();
			dropDown.onValueChanged.AddListener(valueMonitor.OnDropdownValueChanged);
			valueMonitor._dropdown = dropDown;
			valueMonitor._checkFunc = onCheckFunc;
			valueMonitor._onValueChanged = onValueChangedAction;
			valueMonitor._enumType = enumType;
			return valueMonitor;
		}

		public void OnDropdownValueChanged(int value)
		{
			if (_isSyncing)
			{
				return;
			}

			var enumValue = (Enum)Enum.Parse(_enumType, _dropdown.options[_dropdown.value].text);
			_onValueChanged.Invoke(enumValue);
		}

		public void Update()
		{
			var value = _checkFunc.Invoke();
			if (value.Equals(_previousValue))
			{
				return;
			}

			_isSyncing = true;
			_dropdown.value = _dropdown.options.FindIndex(r => r.text.Equals(Enum.GetName(_enumType, value)));
			_dropdown.RefreshShownValue();
			_previousValue = value;
			_isSyncing = false;
		}
	}
}