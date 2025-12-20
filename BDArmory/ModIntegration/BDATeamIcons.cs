// Legacy BDA Team Icons

using System;
using System.Linq;

namespace BDArmory.ModIntegration
{
	public static class LegacyTeamIcons
	{
		public static bool CheckForLegacyTeamIcons()
		{
			using var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator();
			while (a.MoveNext())
				if (a.Current.FullName.Split([','])[0] == "BDATeamIcons")
					return true;
			return false;
		}
	}
}