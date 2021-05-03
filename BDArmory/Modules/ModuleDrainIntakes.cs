using UnityEngine;
using System.Collections.Generic;

namespace BDArmory.Modules
{
    public class ModuleDrainIntakes : PartModule
    {
        public float drainRate = 999;
        public float drainDuration = 20;
        private bool initialized = false;

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                drainDuration -= Time.deltaTime;
                if (drainDuration <= 0)
                {
                    using (List<Part>.Enumerator craftPart = vessel.parts.GetEnumerator())
                    {
                        using (List<ModuleResourceIntake>.Enumerator intake = vessel.FindPartModulesImplementing<ModuleResourceIntake>().GetEnumerator())
                            while (intake.MoveNext())
                            {
                                if (intake.Current == null) continue;
                                intake.Current.intakeEnabled = true;
                            }
                    }
                    part.RemoveModule(this);
                }
            }
            if (!initialized)
            {
                //Debug.Log("[BDArmory.ModuleDrainIntakes]: " + this.part.name + "choked!");
                initialized = true;
                using (List<Part>.Enumerator craftPart = vessel.parts.GetEnumerator())
                {
                    using (List<ModuleResourceIntake>.Enumerator intake = vessel.FindPartModulesImplementing<ModuleResourceIntake>().GetEnumerator())
                        while (intake.MoveNext())
                        {
                            if (intake.Current == null) continue;
                            intake.Current.intakeEnabled = false;
                        }
                }

            }
        }
    }
}

