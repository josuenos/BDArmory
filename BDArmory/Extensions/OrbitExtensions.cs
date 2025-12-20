namespace BDArmory.Extensions
{
  public static class OrbitExtensions
  {
    public static Vector3d GetPrograde(this Orbit o, double UT)
    {
      if ((Versioning.version_major == 1 && Versioning.version_minor > 11) || Versioning.version_major > 1) // Introduced in 1.12
        return GetPrograde_1_12(o, UT);
      return GetPrograde_pre_1_12(o, UT);
    }
    static Vector3d GetPrograde_1_12(Orbit o, double UT) => o.Prograde(UT);
    static Vector3d GetPrograde_pre_1_12(Orbit o, double UT) => o.getOrbitalVelocityAtUT(UT).normalized; // FIXME Is this correct?
  }
}