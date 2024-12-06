using System;

namespace Illusion.SceneEffectsExtended
{
	/// <summary>
	/// A simple class meant to help the serialize/deser functions by abstracting it.
	/// </summary>
	internal class SerializeKit
	{
		public readonly string Name;
		public readonly Func<object> Serialize;
		public readonly Action<object> Deserialize;

		public SerializeKit(string name, Func<object> serializeFunc, Action<object> deserializeAction)
		{
			Name = name;
			Serialize = serializeFunc;
			Deserialize = deserializeAction;
		}
	}
}