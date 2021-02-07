using BDArmory.FX;
using BDArmory.UI;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleDrainFuel : PartModule
    {
        public float drainRate = 1;
        public float drainDuration = 20;
        private int fuelLeft = 0;
        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
            }
            base.OnStart(state);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                drainDuration -= Time.deltaTime;
                if (drainDuration > 0)
                {
                    PartResource fuel = part.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
                    var engine = part.FindModuleImplementing<ModuleEngines>();
                    if (engine != null)
                    {
                        if (engine.enabled)
                        {
                            if (fuel != null)
                            {
                                if (fuel.amount > 0)
                                {
                                    part.RequestResource("LiquidFuel", (double)(drainRate * Time.deltaTime));
                                    fuelLeft++;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (fuel != null)
                        {
                            if (fuel.amount >= 0)
                            {
                                part.RequestResource("LiquidFuel", (double)(drainRate * Time.deltaTime));
                                fuelLeft++;
                            }
                        }
                        PartResource ox = part.Resources.Where(pr => pr.resourceName == "Oxidizer").FirstOrDefault();
                        if (ox != null)
                        {
                            if (ox.amount >= 0)
                            {
                                part.RequestResource("Oxidizer", (double)(drainRate * Time.deltaTime));
                                fuelLeft++;
                            }
                        }
                        PartResource mp = part.Resources.Where(pr => pr.resourceName == "MonoPropellant").FirstOrDefault();
                        if (ox != null)
                        {
                            if (ox.amount >= 0)
                            {
                                part.RequestResource("MonoPropellant", (double)(drainRate * Time.deltaTime));
                                fuelLeft++;
                            }
                        }
                    }
                    if (fuelLeft == 0)
                    {
                        foreach (var existingLeakFX in part.GetComponentsInChildren<FuelLeakFX>())
                        {
                            existingLeakFX.lifeTime = 0; //kill leak FX
                        }
                    }
                    fuelLeft = 0;
                }
                else
                {
                    foreach (var existingLeakFX in part.GetComponentsInChildren<FuelLeakFX>())
                    {
                        existingLeakFX.lifeTime = 0; //kill leak FX
                    }
                    part.RemoveModule(this);
                }
            }
        }
    }
}

