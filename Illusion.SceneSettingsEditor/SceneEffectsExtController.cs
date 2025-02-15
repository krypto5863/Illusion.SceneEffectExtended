﻿using System.Collections.Generic;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace Illusion.SceneEffectsExtended
{
	internal class SceneEffectsExtController : SceneCustomFunctionController
	{
		protected override void OnSceneSave()
		{
			var pluginData = new PluginData
			{
				version = 1,
				data = new Dictionary<string, object>()
			};

			foreach (var serializeKit in SceneEffectsExtended.Serializers.Values)
			{
				pluginData.data[serializeKit.Name] = serializeKit.Serialize();
			}

			SetExtendedData(pluginData);
		}
		protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
		{
			if (operation != SceneOperationKind.Load)
			{
				return;
			}

			var extendedData = GetExtendedData();
			if (extendedData == null)
			{
				return;
			}

			SceneEffectsExtended.PluginLogger.LogDebug("Now loading information!");

			foreach (var keyPair in extendedData.data)
			{
				if (SceneEffectsExtended.Serializers.TryGetValue(keyPair.Key, out var serializeKit))
				{
#if DEBUG
					SceneEffectsExtended.PluginLogger.LogDebug($"Deserializing {keyPair.Key}.");
#endif
					serializeKit.Deserialize(keyPair.Value);
				}
				else
				{
					SceneEffectsExtended.PluginLogger.LogWarning($"No serialize information was defined for {keyPair.Key}! Was the setting removed?");
				}
			}

			DynamicGI.UpdateEnvironment();
		}
	}
}