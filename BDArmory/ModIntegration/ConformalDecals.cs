using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BDArmory.Settings;

namespace BDArmory.ModIntegration
{
	[KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
	public class ConformalDecals : MonoBehaviour
	{
		public static ConformalDecals Instance;
		public static bool hasConformalDecals = false;
		static Assembly CDAssembly = null;
		Type MCDModType = null;
		Func<object, bool> CDisAttachedFieldGetter = null;
		Action<object, bool> CDisAttachedFieldSetter = null;

		void Awake()
		{
			if (Instance is not null) Destroy(Instance);
			Instance = this;
		}

		void Start()
		{
			CheckForConformalDecals();
			if (hasConformalDecals)
			{
				GetMCDModType();
				GetMCDIsAttachedField();
			}
			else
			{
				Destroy(this); // Destroy ourselves to not take up any further CPU cycles.
			}
		}

		public static void CheckForConformalDecals()
		{
			if (hasConformalDecals) return; // Already checked and found.
			using var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator();
			while (a.MoveNext())
			{
				if (a.Current.FullName.StartsWith("ConformalDecals"))
				{
					CDAssembly = a.Current;
					hasConformalDecals = true;
					if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.ModIntegration.ConformalDecals]: Conformal Decals mod detected: {CDAssembly.FullName}.");
					return;
				}
			}
		}

		public void GetMCDModType()
		{
			if (!hasConformalDecals) return;
			foreach (var t in CDAssembly.GetTypes())
			{
				if (t == null) continue;
				if (t.Name == "ModuleConformalDecal")
				{
					MCDModType = t;
					return;
				}
			}
			Debug.LogError($"[BDArmory.ModIntegration.ConformalDecals]: Failed to find ModuleConformalDecal despite ConformalDecals mod being detected!");
		}
		public void GetMCDIsAttachedField()
		{
			if (MCDModType == null) return;
			try
			{
				var fieldInfo = MCDModType.GetField("_isAttached", BindingFlags.NonPublic | BindingFlags.Instance);
				CDisAttachedFieldGetter = ReflectionUtils.CreateGetter<object, bool>(fieldInfo);
				CDisAttachedFieldSetter = ReflectionUtils.CreateSetter<object, bool>(fieldInfo);
			}
			catch (Exception e)
			{
				Debug.LogError($"[BDArmory.ConformalDecals]: Failed to find ModuleConformalDecals._isAttached. Has ConformalDecals changed? {e.Message}");
			}
		}

		public object GetMCDComponent(Part p)
		{
			if (MCDModType == null) return null;
			return p.GetComponent(MCDModType);
		}
		public bool GetMCDIsAttached(object MCDComponent)
		{
			if (MCDComponent == null) return false;
			return CDisAttachedFieldGetter(MCDComponent);
		}
		public void SetMCDIsAttached(object MCDComponent, bool value)
		{
			CDisAttachedFieldSetter(MCDComponent, value);
		}
	}
}