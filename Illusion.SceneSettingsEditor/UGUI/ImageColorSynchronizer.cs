using System;
using UnityEngine;
using UnityEngine.UI;

namespace Illusion.UGUI
{
	public class ImageColorSynchronizer : MonoBehaviour
	{
		private Image _image;
		private Func<Color> _checkFunc;
		private Action<Color> _extraPostAction;
		public static ImageColorSynchronizer AddMonitor(Image image, Func<Color> onCheckFunc, Action<Color> extraPostAction = null)
		{
			var valueMonitor = image.gameObject.AddComponent<ImageColorSynchronizer>();
			valueMonitor._image = image;
			valueMonitor._checkFunc = onCheckFunc;
			valueMonitor._extraPostAction = extraPostAction;
			return valueMonitor;
		}
		public void Update()
		{
			var value = _checkFunc.Invoke();
			if (AreColorsEqualIgnoringAlpha(value, _image.color))
			{
				return;
			}
			var noAlphaColor = new Color(value.r, value.g, value.b, 1.0f);
			_image.color = noAlphaColor;
			_extraPostAction?.Invoke(noAlphaColor);
		}

		public static bool AreColorsEqualIgnoringAlpha(Color color1, Color color2)
		{
			// Directly compare RGB channels without creating new Color objects
			return Mathf.Approximately(color1.r, color2.r) &&
				   Mathf.Approximately(color1.g, color2.g) &&
				   Mathf.Approximately(color1.b, color2.b);
		}
	}
}