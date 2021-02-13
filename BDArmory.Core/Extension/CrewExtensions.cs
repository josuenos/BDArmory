namespace BDArmory.Core.Extension
{
    public static class CrewExtensions
    {
        /// <summary>
        /// Reset the inventory of a crew member to the default of a chute and jetpack.
        /// </summary>
        /// <param name="crew">The crew member</param>
        public static void ResetInventory(this ProtoCrewMember crew)
        {
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
            {
                crew.ResetInventory_1_11();
            }
            else // Nothing, crew didn't have inventory before. Chute and jetpack were built into KerbalEVA class.
            {
            }
        }

        private static void ResetInventory_1_11(this ProtoCrewMember crew) // KSP has issues on older versions if this call is in the parent function.
        {
            crew.SetDefaultInventory(); // Reset the inventory to a chute and a jetpack.
        }
    }
}