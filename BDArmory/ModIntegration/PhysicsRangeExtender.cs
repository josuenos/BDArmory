using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BDArmory.ModIntegration
{
	public static class PhysicsRangeExtender
	{
		static bool havePRE = false;
		static PropertyInfo modEnabled = null; // bool property
		static PropertyInfo PRERange = null; // int property
		static MethodInfo UpdateRanges = null; // Method for updating ranges after setting PRERange.
		public static bool CheckForPhysicsRangeExtender()
		{
			using var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator();
			while (a.MoveNext())
			{
				if (a.Current.FullName.Split([','])[0] == "PhysicsRangeExtender")
				{
					havePRE = true;
					foreach (var t in a.Current.GetTypes())
					{
						if (t == null) continue;
						if (t.Name == "PreSettings")
						{
							modEnabled = t.GetProperty("ModEnabled", BindingFlags.Public | BindingFlags.Static);
							PRERange = t.GetProperty("GlobalRange", BindingFlags.Public | BindingFlags.Static);
						}
						if (t.Name == "PhysicsRangeExtender")
						{
							UpdateRanges = t.GetMethod("UpdateRanges", BindingFlags.Public | BindingFlags.Static);
						}
					}
					break;
				}
			}
			return havePRE;
		}
		public static bool IsPREEnabled => modEnabled != null && (bool)modEnabled.GetValue(null);
		public static float GetPRERange()
		{
			if (PRERange == null) return 0;
			return (int)PRERange.GetValue(null) * 1000f;
		}
		public static bool SetPRERange(int range)
		{
			if (PRERange == null) return false;
			try
			{
				PRERange.SetValue(null, range / 1000);
				UpdateRanges.Invoke(null, [false]);
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to update PRE range: {e.Message}");
				return false;
			}
			return true;
		}
	}
}