using System;
using System.Linq;

namespace BDArmory.ModIntegration
{
	public static class ModuleManager
	{
		public static bool CheckForModuleManager()
		{
			using var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator();
			while (a.MoveNext())
				if (a.Current.FullName.Split([','])[0] == "ModuleManager")
					return true;
			return false;
		}
	}
}