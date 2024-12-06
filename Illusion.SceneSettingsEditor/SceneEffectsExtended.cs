using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using Illusion.UGUI;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Studio.UI;
using MessagePack;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;

#if KKS
using Tonemapping = AmplifyColor.Tonemapping;
#endif

namespace Illusion.SceneEffectsExtended
{
#if KKS
	[BepInProcess("CharaStudio")]
#else
	[BepInProcess("StudioNEOV2")]
#endif

	[BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
	[BepInDependency(ExtendedSave.GUID)]
	[BepInPlugin(Guid, DisplayName, Version)]
	internal class SceneEffectsExtended : BaseUnityPlugin
	{
		public const string Guid = "com.krypto.plugin.sceneeffectsextended";
		public const string DisplayName = "SceneEffectsExtended";
		public const string Version = "1.0";

		//internal static ConfigEntry<float> UiPanelScale;
		internal static SceneEffectsExtended Instance;
		internal static ManualLogSource PluginLogger => Instance.Logger;

		private static readonly int[] ValidReflectionResolutions = {
			128,    // Low resolution (blurry reflections, better performance)
			256,    // Medium-low resolution
			512,    // Medium resolution (balance between quality and performance)
			1024,   // High resolution (sharper reflections, more memory usage)
			2048    // Very high resolution (maximum quality, highest memory usage)
		};

		private void Awake()
		{
			Instance = this;

			StudioSaveLoadApi.RegisterExtraBehaviour<SceneEffectsExtController>(Guid);
			StudioAPI.StudioLoadedChanged += StudioAPI_StudioLoadedChanged;
		}

		private static void StudioAPI_StudioLoadedChanged(object sender, EventArgs e)
		{
#if KKS
			var acesCategory = new SceneEffectsCategory("Color Adjustment");
			CreateAceControlCategory(acesCategory);

			var ambientOcclusion = new SceneEffectsCategory("Ambient Occlusion");
			CreateAmbientOcclusionCategory(ambientOcclusion);
#endif

			var depthOfField = new SceneEffectsCategory("Depth of Field");
			CreateDepthOfFieldCategory(depthOfField);

			var vignetteCategory = new SceneEffectsCategory("Vignette");
			CreateVignetteCategory(vignetteCategory);

			var fogCategory = new SceneEffectsCategory("Fog");
			CreateFogCategory(fogCategory);

			var bloomCategory = new SceneEffectsCategory("Bloom");
			CreateBloomCategory(bloomCategory);

			var sunShafts = new SceneEffectsCategory("GodRays");
			CreateSunShaftCategory(sunShafts);

			var environmentCategory = new SceneEffectsCategory("Environment");
			CreateLightingSection(environmentCategory);
			CreateReflectionSection(environmentCategory);
		}

		internal static readonly Dictionary<string, SerializeKit>
			Serializers = new Dictionary<string, SerializeKit>();

		private static SceneEffectsSliderSet AddSliderAndSync(SceneEffectsCategory category, string serializeName, string label, Func<float> getValue, Action<float> setValue, float minValue, float maxValue)
		{
			var slider = category.AddSliderSet(label, f => { }, getValue(), minValue, maxValue);
			SliderSynchronizer.AddMonitor(slider.Slider, getValue, setValue);

			Serializers[serializeName] = new SerializeKit(serializeName, () => getValue(), o =>
			{
				setValue((float)o);
			});

			return slider;
		}
		private static SceneEffectsSliderSet AddIntSliderAndSync(SceneEffectsCategory category, string serializeName, string label, Func<float> getValue, Action<float> setValue, int minValue, int maxValue)
		{
			var slider = AddSliderAndSync(category, serializeName, label, getValue, setValue, minValue, maxValue);
			slider.Slider.wholeNumbers = true;

			return slider;
		}
		private static SceneEffectsToggleSet AddToggleAndSync(SceneEffectsCategory category, string serializeName, string label, Func<bool> getValue, Action<bool> setValue)
		{
			var toggle = category.AddToggleSet(label, f => { }, getValue());
			ToggleSynchronizer.AddMonitor(toggle.Toggle, getValue, setValue);

			Serializers[serializeName] = new SerializeKit(serializeName, () => getValue(), o =>
			{
				setValue((bool)o);
			});

			return toggle;
		}
		private static SceneEffectsDropdownSet AddEnumDropdownAndSync<T>(SceneEffectsCategory category, string serializeName, string label, Func<Enum> getValue, Action<Enum> setValue)
		{
			var options = Enum.GetNames(typeof(T)).ToList();
			var dropdown = category.AddDropdownSet(label, r => { }, options, Enum.GetName(typeof(T), getValue()));
			EnumDropDownSynchronizer.AddMonitor(dropdown.Dropdown, typeof(T), getValue, setValue);

			Serializers[serializeName] = new SerializeKit(serializeName, () => Enum.GetName(typeof(T), getValue()), o =>
			{
				var enumString = (string)o;
				var value = Enum.Parse(typeof(T), enumString);
				setValue((Enum)value);
			});

			return dropdown;
		}
		private static SceneEffectsDropdownSet AddIntDropdownAndSync(SceneEffectsCategory category, string serializeName, string label, IEnumerable<int> options, Func<int> getValue, Action<int> setValue)
		{
			var stringOptions = options.Select(r => r.ToString()).ToList();
			var defaultOptionIndex = stringOptions.FindIndex(r => int.Parse(r) == getValue());
			var defaultOption = stringOptions[defaultOptionIndex];
			var dropdown = category.AddDropdownSet(label, r => { }, stringOptions, defaultOption);
			IntDropDownSynchronizer.AddMonitor(dropdown.Dropdown, getValue, setValue);

			Serializers[serializeName] = new SerializeKit(serializeName, () => getValue(), o =>
			{
				setValue((int)o);
			});

			return dropdown;
		}
		private static SceneEffectsColorPickerSet AddColorAndSync(SceneEffectsCategory category, string serializeName, string label, Func<Color> getValue, Action<Color> setValue)
		{
			var colorPicker = category.AddColorPickerSet(label, setValue, getValue());
			ImageColorSynchronizer.AddMonitor(colorPicker.ColorImage, getValue, color =>
			{
				colorPicker.SetValue(color);
			});

			Serializers[serializeName] = new SerializeKit(serializeName, () => MessagePackSerializer.Serialize(getValue()), o =>
			{
				var value = MessagePackSerializer.Deserialize<Color>(o as byte[]);
				setValue(value);
			});

			return colorPicker;
		}
		private static string PrintFullName<T>(string memberName)
		{
			return $"{typeof(T).Name}.{memberName}";
		}
		private static void MergeCategory(SceneEffectsCategory category, GameObject content,
			GameObject header, int contentOffset = 0)
		{
			category.Content.transform.SetSiblingIndex(content.transform.GetSiblingIndex() + contentOffset);
			category.Header.transform.SetSiblingIndex(category.Content.transform.GetSiblingIndex());

			var collapseButton = header.GetComponentInChildren<Button>();
			collapseButton.onClick.AddListener(() =>
			{
				category.Content.SetActive(!content.activeSelf);
			});

			category.Header.gameObject.SetActive(false);
		}
#if KKS
		private static void CreateAceControlCategory(SceneEffectsCategory category)
		{
			var acesEffect = Camera.main.GetComponent<AmplifyColorEffect>();

			AddToggleAndSync(category, PrintFullName<AmplifyColorEffect>(nameof(enabled)), "Active", () => acesEffect.enabled, b => acesEffect.enabled = b);
			AddEnumDropdownAndSync<Tonemapping>(category, PrintFullName<AmplifyColorEffect>(nameof(AmplifyColorEffect.Tonemapper)), "Tonemapper", () => acesEffect.Tonemapper,
				@enum => { acesEffect.Tonemapper = (Tonemapping)@enum; });
			AddSliderAndSync(category, PrintFullName<AmplifyColorEffect>(nameof(AmplifyColorEffect.Exposure)), "Exposure", () => acesEffect.Exposure, f => acesEffect.Exposure = f, 0, 5);

			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Amplify Color Effect");
			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Amplify Color Effect");

			MergeCategory(category, content, header, 1);
		}

		private static void CreateAmbientOcclusionCategory(SceneEffectsCategory category)
		{
			var aoEffect = Camera.main.GetComponent<AmplifyOcclusionEffect>();

			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.Intensity)), "Intensity", () => aoEffect.Intensity,
				f => { aoEffect.Intensity = f; }, 0, 2);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.PowerExponent)), "Power Exponent", () => aoEffect.PowerExponent,
				f => { aoEffect.PowerExponent = f; }, 0.1f, 4);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.Bias)), "Bias", () => aoEffect.Bias,
				f => { aoEffect.Bias = f; }, 0, 0.5f);

			category.AddLabelSet("Fade");

			AddToggleAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(aoEffect.enabled)), "Enabled", () => aoEffect.FadeEnabled, b =>
			{
				aoEffect.FadeEnabled = b;
			});

			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.FadeStart)), "Start", () => aoEffect.FadeStart,
				f => { aoEffect.FadeStart = f; }, 0, 100);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.FadeLength)), "Length", () => aoEffect.FadeLength,
				f => { aoEffect.FadeLength = f; }, 0, 100);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.FadeToIntensity)), "To Intensity", () => aoEffect.FadeToIntensity,
				f => { aoEffect.FadeToIntensity = f; }, 0, 2);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.FadeToRadius)), "To Radius", () => aoEffect.FadeToRadius,
				f => { aoEffect.FadeToRadius = f; }, 0, 2);
			AddSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.FadeToPowerExponent)), "To Power Exponent", () => aoEffect.FadeToPowerExponent,
				f => { aoEffect.FadeToPowerExponent = f; }, 0.1f, 4);

			category.AddLabelSet("Blur");

			AddToggleAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(aoEffect.BlurEnabled)), "Enabled", () => aoEffect.BlurEnabled, b =>
			{
				aoEffect.BlurEnabled = b;
			});
			AddIntSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.BlurRadius)), "Radius", () => aoEffect.BlurRadius,
				f => { aoEffect.BlurRadius = (int)f; }, 1, 4);

			AddIntSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.BlurPasses)), "Passes", () => aoEffect.BlurPasses,
				f => { aoEffect.BlurPasses = (int)f; }, 1, 4);

			AddIntSliderAndSync(category, PrintFullName<AmplifyOcclusionEffect>(nameof(AmplifyOcclusionEffect.BlurSharpness)), "Sharpness", () => aoEffect.BlurSharpness,
				f => { aoEffect.BlurSharpness = f; }, 0, 10);

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Amplify Occlusion Effect");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Amplify Occlusion Effect");

			MergeCategory(category, content, header, 1);
		}
#endif

		private static void CreateDepthOfFieldCategory(SceneEffectsCategory category)
		{
			var dofEffect = Camera.main.GetComponent<DepthOfField>();

			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.focalLength)), "Focal Length", () => dofEffect.focalLength,
				f => { dofEffect.focalLength = f; }, 0, 300);
			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.maxBlurSize)), "Max Blur Size", () => dofEffect.maxBlurSize,
				f => { dofEffect.maxBlurSize = f; }, 0, 10);
			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.foregroundOverlap)), "Foreground Overlap", () => dofEffect.foregroundOverlap,
				f => { dofEffect.foregroundOverlap = f; }, 0, 2);
			AddToggleAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.visualizeFocus)), "Visualize Focus", () => dofEffect.visualizeFocus,
				f => { dofEffect.visualizeFocus = f; });
			AddToggleAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.highResolution)), "High Res", () => dofEffect.highResolution,
				f => { dofEffect.highResolution = f; });
			AddToggleAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.nearBlur)), "Near Blur", () => dofEffect.nearBlur,
				f => { dofEffect.nearBlur = f; });
			AddEnumDropdownAndSync<DepthOfField.BlurType>(category,
				PrintFullName<DepthOfField>(nameof(DepthOfField.blurType)), "Defocus Type", () => dofEffect.blurType,
				@enum => { dofEffect.blurType = (DepthOfField.BlurType)@enum; });

			category.AddLabelSet("Bokeh/DX11");

			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.dx11BokehIntensity)), "Intensity", () => dofEffect.dx11BokehIntensity,
				f => { dofEffect.dx11BokehIntensity = f; }, 0, 10);
			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.dx11BokehThreshold)), "Threshold", () => dofEffect.dx11BokehThreshold,
				f => { dofEffect.dx11BokehThreshold = f; }, 0, 1);
			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.dx11SpawnHeuristic)), "Spawn Heuristic", () => dofEffect.dx11SpawnHeuristic,
				f => { dofEffect.dx11SpawnHeuristic = f; }, 0, 1);
			AddSliderAndSync(category, PrintFullName<DepthOfField>(nameof(DepthOfField.dx11BokehScale)), "Scale", () => dofEffect.dx11BokehScale,
				f => { dofEffect.dx11BokehScale = f; }, 0, 10);

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Depth of Field");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Depth of Field");

			MergeCategory(category, content, header, 1);
		}

		private static void CreateVignetteCategory(SceneEffectsCategory category)
		{
			var chromaticAndVignetteEffect = Camera.main.GetComponent<VignetteAndChromaticAberration>();
			/*
			AddEnumDropdownAndSync<VignetteAndChromaticAberration.AberrationMode>(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.mode)), "Mode", () => chromaticAndVignetteEffect.mode,
				@enum =>
				{
					chromaticAndVignetteEffect.mode = (AberrationMode)@enum;
				});
			*/

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.intensity)), "Intensity", () => chromaticAndVignetteEffect.intensity,
				f =>
				{
					chromaticAndVignetteEffect.intensity = f;
				}, 0, 1);

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.luminanceDependency)), "Luminance Dependency", () => chromaticAndVignetteEffect.luminanceDependency,
				f =>
				{
					chromaticAndVignetteEffect.luminanceDependency = f;
				}, 0, 1);

			category.AddLabelSet("Chromatic Aberration");

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.chromaticAberration)), "Intensity", () => chromaticAndVignetteEffect.chromaticAberration,
				f =>
				{
					chromaticAndVignetteEffect.chromaticAberration = f;
				}, 0, 50);

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.axialAberration)), "Axial Aberration", () => chromaticAndVignetteEffect.axialAberration,
				f =>
				{
					chromaticAndVignetteEffect.axialAberration = f;
				}, 0, 50);

			category.AddLabelSet("Blur");

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.blur)), "Intensity", () => chromaticAndVignetteEffect.blur,
				f =>
				{
					chromaticAndVignetteEffect.blur = f;
				}, 0, 1);

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.blurSpread)), "Blur Spread", () => chromaticAndVignetteEffect.blurSpread,
				f =>
				{
					chromaticAndVignetteEffect.blurSpread = f;
				}, 0, 10);

			AddSliderAndSync(category,
				PrintFullName<VignetteAndChromaticAberration>(nameof(VignetteAndChromaticAberration.blurDistance)), "Blur Distance", () => chromaticAndVignetteEffect.blurDistance,
				f =>
				{
					chromaticAndVignetteEffect.blurDistance = f;
				}, 0, 100);

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Vignette");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Vignette");

			MergeCategory(category, content, header, 1);
		}
		private static void CreateFogCategory(SceneEffectsCategory category)
		{
			AddSliderAndSync(category,
				PrintFullName<RenderSettings>(nameof(RenderSettings.fogEndDistance)), "Ending Distance", () => RenderSettings.fogEndDistance,
				f =>
				{
					RenderSettings.fogEndDistance = f;
				}, 0, 100);

			AddEnumDropdownAndSync<FogMode>(category,
				PrintFullName<RenderSettings>(nameof(RenderSettings.fogMode)), "Mode", () => RenderSettings.fogMode,
				f =>
				{
					RenderSettings.fogMode = (FogMode)f;
				});

			AddSliderAndSync(category,
				PrintFullName<RenderSettings>(nameof(RenderSettings.fogDensity)), "Density", () => RenderSettings.fogDensity,
				f =>
				{
					RenderSettings.fogDensity = f;
				}, 0, 100);

			var globalFog = Camera.main.GetComponent<GlobalFog>();

			AddSliderAndSync(category,
				PrintFullName<GlobalFog>(nameof(GlobalFog.heightDensity)), "Height Density", () => globalFog.heightDensity,
				f =>
				{
					globalFog.heightDensity = f;
				}, 0, 0.1f);

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Fog");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Fog");

			MergeCategory(category, content, header, 1);
		}

		private static void CreateBloomCategory(SceneEffectsCategory category)
		{
			var bloomEffect = Camera.main.GetComponent<BloomAndFlares>();

			AddIntSliderAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.bloomBlurIterations)), "Blur Iterations",
				() => bloomEffect.bloomBlurIterations, f =>
				{
					bloomEffect.bloomBlurIterations = (int)f;
				}, 0, 20);

			category.AddLabelSet("Lens Flares");

			AddToggleAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.lensflares)), "Active",
				() => bloomEffect.lensflares,
				b =>
				{
					bloomEffect.lensflares = b;
					bloomEffect.lensflareMode = b ? LensflareStyle34.Ghosting : LensflareStyle34.Anamorphic;
				});

			AddSliderAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.lensflareIntensity)), "Intensity",
				() => bloomEffect.lensflareIntensity, f =>
				{
					bloomEffect.lensflareIntensity = f;
				}, 0, 20);

			AddSliderAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.lensflareThreshold)), "Threshold",
				() => bloomEffect.lensflareThreshold, f =>
				{
					bloomEffect.lensflareThreshold = f;
				}, 0, 1);

			AddColorAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.flareColorA)), "Flare A",
				() => bloomEffect.flareColorA,
				color =>
				{
					bloomEffect.flareColorA = color;
				});

			AddColorAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.flareColorB)), "Flare B",
				() => bloomEffect.flareColorB,
				color =>
				{
					bloomEffect.flareColorB = color;
				});

			AddColorAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.flareColorC)), "Flare C",
				() => bloomEffect.flareColorC,
				color =>
				{
					bloomEffect.flareColorC = color;
				});

			AddColorAndSync(category, PrintFullName<BloomAndFlares>(nameof(BloomAndFlares.flareColorD)), "Flare D",
				() => bloomEffect.flareColorD,
				color =>
				{
					bloomEffect.flareColorD = color;
				});

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Bloom And Flares");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Bloom And Flares");

			MergeCategory(category, content, header, 1);
		}
		private static void CreateSunShaftCategory(SceneEffectsCategory category)
		{
			var sunShaftEffect = Camera.main.GetComponent<SunShafts>();

			AddEnumDropdownAndSync<SunShafts.SunShaftsResolution>(category,
				PrintFullName<SunShafts>(nameof(SunShafts.resolution)), "Resolution",
				() => sunShaftEffect.resolution, @enum =>
				{
					sunShaftEffect.resolution = (SunShafts.SunShaftsResolution)@enum;
				}
			);

			AddSliderAndSync(category, PrintFullName<SunShafts>(nameof(SunShafts.sunShaftIntensity)), "Intensity",
				() => sunShaftEffect.sunShaftIntensity,
				f => { sunShaftEffect.sunShaftIntensity = f; }, 0, 10);

			AddSliderAndSync(category, PrintFullName<SunShafts>(nameof(SunShafts.maxRadius)), "Max Radius",
				() => sunShaftEffect.maxRadius,
				f => { sunShaftEffect.maxRadius = f; }, 0, 1);

			AddSliderAndSync(category, PrintFullName<SunShafts>(nameof(SunShafts.sunShaftBlurRadius)), "Blur Radius",
				() => sunShaftEffect.sunShaftBlurRadius,
				f => { sunShaftEffect.sunShaftBlurRadius = f; }, 0, 10);

			AddIntSliderAndSync(category, PrintFullName<SunShafts>(nameof(SunShafts.radialBlurIterations)), "Radial Blur Iterations",
				() => sunShaftEffect.radialBlurIterations,
				f => { sunShaftEffect.radialBlurIterations = (int)f; }, 1, 4);

			var header =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Image Sun Shafts");
			var content =
				GameObject.Find(
					"StudioScene/Canvas Main Menu/04_System/01_Screen Effect/Screen Effect/Viewport/Content/Sun Shafts");

			MergeCategory(category, content, header, 1);
		}

		private static void CreateLightingSection(SceneEffectsCategory category)
		{
			category.AddLabelSet("Environment Lighting");
			var dropDown = AddEnumDropdownAndSync<AmbientMode>(category, PrintFullName<RenderSettings>(nameof(RenderSettings.ambientMode)), "Source", () => RenderSettings.ambientMode, @enum =>
			{
				RenderSettings.ambientMode = (AmbientMode)@enum;
				DynamicGI.UpdateEnvironment();
			});

			var intensitySlider = AddSliderAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.ambientIntensity)), "Intensity Multiplier",
				() => RenderSettings.ambientIntensity,
				f => { RenderSettings.ambientIntensity = f; }, 0, 8);

			var skyColor = AddColorAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.ambientSkyColor)), "Sky", () => RenderSettings.ambientSkyColor, color =>
			{
				RenderSettings.ambientSkyColor = color;
			});
			var equatorColor = AddColorAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.ambientEquatorColor)), "Equator", () => RenderSettings.ambientEquatorColor, color =>
			{
				RenderSettings.ambientEquatorColor = color;
			});
			var groundColor = AddColorAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.ambientGroundColor)), "Ground", () => RenderSettings.ambientGroundColor, color =>
			{
				RenderSettings.ambientGroundColor = color;
			});

			dropDown.Dropdown.onValueChanged.AddListener(m =>
			{
				var enumValue = (AmbientMode)Enum.Parse(typeof(AmbientMode), dropDown.Dropdown.options[dropDown.Dropdown.value].text);

				skyColor.Button.interactable = false;
				equatorColor.Button.interactable = false;
				groundColor.Button.interactable = false;

				intensitySlider.Slider.interactable = false;
				intensitySlider.Button.interactable = false;
				intensitySlider.Input.interactable = false;

				switch (enumValue)
				{
					case AmbientMode.Skybox:
						intensitySlider.Slider.interactable = true;
						intensitySlider.Button.interactable = true;
						intensitySlider.Input.interactable = true;
						break;

					case AmbientMode.Trilight:
						skyColor.Button.interactable = true;
						equatorColor.Button.interactable = true;
						groundColor.Button.interactable = true;
						break;

					case AmbientMode.Flat:
						skyColor.Button.interactable = true;
						break;

					default:
						skyColor.Button.interactable = true;
						equatorColor.Button.interactable = true;
						groundColor.Button.interactable = true;

						intensitySlider.Slider.interactable = true;
						intensitySlider.Button.interactable = true;
						intensitySlider.Input.interactable = true;
						break;
				}
			});

			dropDown.Dropdown.onValueChanged.Invoke(dropDown.Value);
		}

		private static void CreateReflectionSection(SceneEffectsCategory category)
		{
			category.AddLabelSet("Environment Reflections");

			AddEnumDropdownAndSync<DefaultReflectionMode>(category, PrintFullName<RenderSettings>(nameof(RenderSettings.defaultReflectionMode)), "Source", () => RenderSettings.defaultReflectionMode,
				@enum =>
				{
					RenderSettings.defaultReflectionMode = (DefaultReflectionMode)@enum;
					DynamicGI.UpdateEnvironment();
				});

			AddIntDropdownAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.defaultReflectionResolution)), "Resolution", ValidReflectionResolutions,
				() => RenderSettings.defaultReflectionResolution,
				i =>
				{
					RenderSettings.defaultReflectionResolution = i;
					DynamicGI.UpdateEnvironment();
				});

			AddSliderAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.reflectionIntensity)), "Intensity Multiplier", () => RenderSettings.reflectionIntensity, f =>
			{
				RenderSettings.reflectionIntensity = f;
			}, 0, 1);

			AddIntSliderAndSync(category, PrintFullName<RenderSettings>(nameof(RenderSettings.reflectionBounces)), "Bounces", () => RenderSettings.reflectionBounces, f =>
			{
				RenderSettings.reflectionBounces = (int)f;
			}, 1, 5);
		}
	}
}