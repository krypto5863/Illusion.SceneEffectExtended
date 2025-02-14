using BepInEx;
using Funly.SkyStudio;
using HarmonyLib;
using IllusionUtility.GetUtility;
using JetBrains.Annotations;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace KKS.SceneEffectFixes
{
#if KKS
	[BepInProcess("CharaStudio")]
#else
	[BepInProcess("StudioNEOV2")]
#endif

	[BepInPlugin(Guid, DisplayName, Version)]
	public class SceneEffectFixes : BaseUnityPlugin
	{
		public const string Guid = "org.krypto5863.illusion.SceneEffectFixes";
		public const string DisplayName = "Scene Effects Fixes";
		public const string Version = "1.0";

		private void Awake()
		{
			Harmony.CreateAndPatchAll(typeof(Hooks));
		}

		private static Light _currentSun;
		private static Transform _currentSunTransform;

		[CanBeNull]
		private static SunShafts _currentSunShafts;
		[CanBeNull]
		private static SunShafts CurrentSunShafts 
		{
			get
			{
				if (_currentSunShafts == null || _currentSunShafts.gameObject != Camera.main)
				{
					_currentSunShafts = Camera.main.GetComponent<SunShafts>();
				}

				return _currentSunShafts;
			}
		}

		private static void UpdateSunShaftTransform()
		{
			if (Studio.Studio.Instance.sceneInfo.sunCaster != -1)
			{
				return;
			}

			if (_currentSun != RenderSettings.sun)
			{
				_currentSun = RenderSettings.sun;
				_currentSunTransform = _currentSun.transform?.parent?.FindLoop("Position")?.transform;
			}

			if (_currentSunTransform == null)
			{
				return;
			}

			var sunShafts = CurrentSunShafts;

			if (sunShafts == null)
			{
				return;
			}

			if (sunShafts.sunTransform != null && sunShafts.sunTransform == _currentSunTransform)
			{
				return;
			}

			sunShafts.sunTransform = _currentSunTransform;
		}

		private static class Hooks
		{
			[HarmonyPatch(typeof(RenderSettings), nameof(RenderSettings.sun), MethodType.Setter)]
			[HarmonyPatch(typeof(Studio.Studio), nameof(Studio.Studio.SetSunCaster))]
			[HarmonyPostfix]
			public static void UpdateSunShafts()
			{
				UpdateSunShaftTransform();
			}

			[HarmonyPatch(typeof(TimeOfDayController), nameof(TimeOfDayController.Awake))]
			[HarmonyPostfix]
			public static void EnableGiUpdating(TimeOfDayController __instance)
			{
				__instance.updateGlobalIllumination = true;
			}
		}
	}
}
