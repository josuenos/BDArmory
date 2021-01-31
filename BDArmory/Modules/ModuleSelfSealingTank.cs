using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.Modules
{
    class ModuleSelfSealingTank : PartModule
    {
        [KSPField(isPersistant = true)]
        public bool SSTank = false;

        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_SSTank", active = true)]//Self-Sealing Tank
        public void ToggleTankOption()
        {
            SSTank = !SSTank;

            if (SSTank == false)
            {
                Events["ToggleTankOption"].guiName = Localizer.Format("#LOC_BDArmory_SSTank_On");//"Disable self-sealing tank"
                for (int i = 0; i < origValues.Count; i++)
                {
                    using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                        while (resource.MoveNext())
                        {
                            if (resource.Current == null) continue;
                            resource.Current.maxAmount = origValues[resource.Current.resourceName];
                        }
                }
                part.mass = (float)origMass;
            }
            else
            {
                Events["ToggleTankOption"].guiName = Localizer.Format("#LOC_BDArmory_SSTank_Off");//"Disable self-sealing tank"

                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        if (origValues.ContainsKey(resource.Current.resourceName))
                        {
                            resource.Current.maxAmount = (origValues[resource.Current.resourceName] * 0.9f);
                        }
                        else
                        {
                            origValues.Add(resource.Current.resourceName, resource.Current.maxAmount);
                            resource.Current.maxAmount *= 0.9;
                        }
                        resource.Current.amount = resource.Current.maxAmount;
                    }

                part.mass = partmass;
            }
            Misc.Misc.RefreshAssociatedWindows(part);
        }

        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DryMass")]//Dry mass
        public float partmass = 0f;

        private double origMass;
        private Dictionary<string, double> origValues;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            origValues = new Dictionary<string, double>();
            using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                while (resource.MoveNext())
                {
                    if (resource.Current == null) continue;
                    origValues.Add(resource.Current.resourceName, resource.Current.maxAmount);
                }

            origMass = part.mass;
            partmass = part.mass * 1.5f;
        }

    }
}
