using BDArmory.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.Modules;

namespace BDArmory.Misc
{
    class BattleDamageHandler
    {
        public static void CheckDamageFX(Part part, float caliber, float penetrationFactor, bool explosivedamage, string attacker, RaycastHit hitLoc)
        {
            if (!BDArmorySettings.BATTLEDAMAGE || BDArmorySettings.PAINTBALL_MODE) return;

            double damageChance = Mathf.Clamp((BDArmorySettings.BD_DAMAGE_CHANCE * ((1 - part.GetDamagePercentatge()) * 10) * (penetrationFactor / 2)), 0, 100); //more heavily damaged parts more likely to take battledamage

            if (BDArmorySettings.BD_TANKS)
            {
                if (part.HasFuel() && penetrationFactor > 1.2)
                {
                    BulletHitFX.AttachLeak(hitLoc, part, caliber, explosivedamage, attacker);
                }
                if (part.isBattery())
                {
                    var alreadyburning = part.GetComponentInChildren<FireFX>();
                    if (alreadyburning == null)
                    {
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (explosivedamage)
                        {
                            Diceroll *= 0.66;
                        }
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Battery Dice Roll: " + Diceroll);
                        if (Diceroll <= BDArmorySettings.BD_DAMAGE_CHANCE)
                        {
                            BulletHitFX.AttachFire(hitLoc, part, caliber, attacker);
                        }
                    }
                }
            }
            //AmmoBins
            if (BDArmorySettings.BD_AMMOBINS)
            {
                var ammo = part.FindModuleImplementing<ModuleCASE>();
                if (ammo != null)
                {
                    double Diceroll = UnityEngine.Random.Range(0, 100);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Ammo TAC DiceRoll: " + Diceroll + "; needs: " + damageChance);
                    if (Diceroll <= (damageChance) && part.GetDamagePercentatge() < 0.95f)
                    {
                        ammo.SourceVessel = attacker;
                        ammo.DetonateIfPossible();
                    }
                }
            }
            //Propulsaion Damage
            if (BDArmorySettings.BD_PROPULSION)
            {
                if (part.isEngine() && part.GetDamagePercentatge() < 0.95f) //first hit's free
                {
                    foreach (var engine in part.GetComponentsInChildren<ModuleEngines>())
                    {
                        if (engine.thrustPercentage > 0) //engines take thrust damage per hit
                        {
                            //engine.maxThrust -= ((engine.maxThrust * 0.125f) / 100); // doesn't seem to adjust thrust; investigate
                            //engine.thrustPercentage -= ((engine.maxThrust * 0.125f) / 100); //workaround hack
                            engine.thrustPercentage *= (1 - (((1 - part.GetDamagePercentatge()) * (penetrationFactor / 4)) / BDArmorySettings.BD_PROP_DAM_RATE)); //AP does bonus damage
                            Mathf.Clamp(engine.thrustPercentage, 0.15f, 1); //even heavily damaged engines will still put out something
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: engine thrust: " + engine.thrustPercentage);
                        }
                        if (part.GetDamagePercentatge() < 0.75f || (part.GetDamagePercentatge() < 0.82f && penetrationFactor > 2))
                        {
                            var leak = part.GetComponentInChildren<FuelLeakFX>();
                            if (leak == null)
                            {
                                BulletHitFX.AttachLeak(hitLoc, part, caliber, explosivedamage, attacker);
                            }
                        }
                        if (part.GetDamagePercentatge() < 0.50f || (part.GetDamagePercentatge() < 0.625f && penetrationFactor > 2))
                        {
                            var alreadyburning = part.GetComponentInChildren<FireFX>();
                            if (alreadyburning == null)
                            {
                                BulletHitFX.AttachFire(hitLoc, part, caliber, attacker);
                            }
                        }
                        if (part.GetDamagePercentatge() < 0.25f)
                        {
                            if (engine.EngineIgnited)
                            {
                                engine.PlayFlameoutFX(true);
                                engine.Shutdown(); //kill a badly damaged engine and don't allow restart
                                engine.allowRestart = false;
                            }
                        }
                    }
                    foreach (var enginefx in part.GetComponentsInChildren<ModuleEnginesFX>())
                    {
                        if (enginefx.thrustPercentage > 0) //engines take thrust damage per hit
                        {
                            //engine.maxThrust -= ((engine.maxThrust * 0.125f) / 100); // doesn't seem to adjust thrust; investigate
                            //engine.thrustPercentage -= ((engine.maxThrust * 0.125f) / 100); //workaround hack
                            enginefx.thrustPercentage *= (1 - (((1 - part.GetDamagePercentatge()) * (penetrationFactor / 4)) / BDArmorySettings.BD_PROP_DAM_RATE)); //AP does bonus damage
                            Mathf.Clamp(enginefx.thrustPercentage, 0.15f, 1); //even heavily damaged engines will still put out something
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: engine thrust: " + enginefx.thrustPercentage);
                        }
                        if (part.GetDamagePercentatge() < 0.75f || (part.GetDamagePercentatge() < 0.82f && penetrationFactor > 2))
                        {
                            //engine.atmosphereCurve =    //mod atmosphereCurve  to decrease Isp, increase fuel use?
                        }
                        if (part.GetDamagePercentatge() < 0.25f)
                        {
                            if (enginefx.EngineIgnited)
                            {
                                enginefx.PlayFlameoutFX(true);
                                enginefx.Shutdown(); //kill a badly damaged engine and don't allow restart
                                enginefx.allowRestart = false;
                            }
                        }
                    }
                }
                if (BDArmorySettings.BD_INTAKES) //intake damage
                {
                    var intake = part.FindModuleImplementing<ModuleResourceIntake>();
                    if (intake != null)
                    {
                        float HEBonus = 1;
                        if (explosivedamage)
                        {
                            HEBonus = 2;
                        }
                        intake.intakeSpeed *= (1 - (((1 - part.GetDamagePercentatge()) * HEBonus) / BDArmorySettings.BD_PROP_DAM_RATE)); //HE does bonus damage
                        Mathf.Clamp((float)intake.intakeSpeed, 0, 99999);

                        intake.area *= (1 - (((1 - part.GetDamagePercentatge()) * HEBonus) / BDArmorySettings.BD_PROP_DAM_RATE)); //HE does bonus damage
                        Mathf.Clamp((float)intake.area, 0.0002f, 99999); //even shredded intake ducting will still get some air to engines
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Intake damage: Current Area: " + intake.area + "; Intake Speed: " + intake.intakeSpeed);
                    }
                }
                if (BDArmorySettings.BD_GIMBALS) //engine gimbal damage
                {
                    var gimbal = part.FindModuleImplementing<ModuleGimbal>();
                    if (gimbal != null)
                    {
                        double HEBonus = 1;
                        if (explosivedamage)
                        {
                            HEBonus = 1.5;
                        }
                        //gimbal.gimbalRange *= (1 - (((1 - part.GetDamagePercentatge()) * HEBonus) / BDArmorySettings.BD_PROP_DAM_RATE)); //HE does bonus damage
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Gimbal DiceRoll: " + Diceroll);
                        if (Diceroll <= (BDArmorySettings.BD_DAMAGE_CHANCE * HEBonus))
                        {
                            gimbal.enabled = false;
                            gimbal.gimbalRange = 0;
                        }
                    }
                }
            }
            //Aero Damage
            if (BDArmorySettings.BD_AEROPARTS)
            {
                float HEBonus = 1;
                if (explosivedamage)
                {
                    HEBonus = 2; //explosive rounds blow bigger holes in wings
                }
                Mathf.Clamp(penetrationFactor, 0.1f, 3);
                HEBonus /= penetrationFactor; //faster rounds punch cleaner holes
                float liftDam = ((caliber / 10000) * HEBonus) * BDArmorySettings.BD_LIFT_LOSS_RATE;
                if (part.GetComponent<ModuleLiftingSurface>() != null)
                {
                    ModuleLiftingSurface wing;
                    wing = part.GetComponent<ModuleLiftingSurface>();
                    //2x4m wing board = 2 Lift, 0.25 Lift/m2. 20mm round = 20*20=400/20000= 0.02 Lift reduced per hit, 100 rounds to reduce lift to 0. mind you, it only takes ~15 rounds to destroy the wing...
                    if (wing.deflectionLiftCoeff > 0)
                    {
                        wing.deflectionLiftCoeff -= liftDam;
                        wing.deflectionLiftCoeff = Mathf.Clamp(wing.deflectionLiftCoeff, 0.01f, Mathf.Infinity);
                    }
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD DEBUG] " + part.name + "took lift damage: " + liftDam + ", current lift: " + wing.deflectionLiftCoeff);
                }
                if (part.GetComponent<ModuleControlSurface>() != null && part.GetDamagePercentatge() > 0.125f)
                {
                    ModuleControlSurface aileron;
                    aileron = part.GetComponent<ModuleControlSurface>();
                    if (aileron.deflectionLiftCoeff > 0)
                    {
                        aileron.deflectionLiftCoeff -= liftDam;
                        aileron.deflectionLiftCoeff = Mathf.Clamp(aileron.deflectionLiftCoeff, 0.01f, Mathf.Infinity);
                    }
                    if (BDArmorySettings.BD_CTRL_SRF)
                    {
                        int Diceroll = (int)UnityEngine.Random.Range(0f, 100f);
                        if (explosivedamage)
                        {
                            HEBonus = 1.2f;
                        }
                        if (Diceroll <= (BDArmorySettings.BD_DAMAGE_CHANCE * HEBonus))
                        {
                            aileron.actuatorSpeed = 0;
                            aileron.authorityLimiter = 0;
                            aileron.ctrlSurfaceRange = 0;
                        }
                    }
                }
            }
            //Subsystems
            if (BDArmorySettings.BD_SUBSYSTEMS)
            {
                double Diceroll = UnityEngine.Random.Range(0, 100);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Subsystem DiceRoll: " + Diceroll + "; needs: " + damageChance);
                if (Diceroll <= (damageChance) && part.GetDamagePercentatge() < 0.95f)
                {
                    if (part.GetComponent<ModuleReactionWheel>() != null) //should have this be separate dice rolls, else a part with more than one of these will lose them all
                    {
                        ModuleReactionWheel SAS; //could have torque reduced per hit
                        SAS = part.GetComponent<ModuleReactionWheel>();
                        part.RemoveModule(SAS);
                    }
                    if (part.GetComponent<ModuleRadar>() != null)
                    {
                        ModuleRadar radar; //would need to mod detection curve to degrade performance on hit
                        radar = part.GetComponent<ModuleRadar>();
                        part.RemoveModule(radar);
                    }
                    if (part.GetComponent<ModuleAlternator>() != null)
                    {
                        ModuleAlternator alt; //damaging alternator is probably just petty. Could reduce output per hit
                        alt = part.GetComponent<ModuleAlternator>();
                        part.RemoveModule(alt);
                    }
                    if (part.GetComponent<ModuleAnimateGeneric>() != null)
                    {
                        ModuleAnimateGeneric anim;
                        anim = part.GetComponent<ModuleAnimateGeneric>(); // could reduce anim speed, open percent per hit
                        part.RemoveModule(anim);
                    }
                    if (part.GetComponent<ModuleDecouple>() != null)
                    {
                        ModuleDecouple stage;
                        stage = part.GetComponent<ModuleDecouple>(); //decouplers decouple
                        stage.Decouple();
                    }
                    if (part.GetComponent<ModuleECMJammer>() != null)
                    {
                        ModuleECMJammer ecm;
                        ecm = part.GetComponent<ModuleECMJammer>(); //could reduce ecm strngth/rcs modifier
                        part.RemoveModule(ecm);
                    }
                    if (part.GetComponent<ModuleGenerator>() != null)
                    {
                        ModuleGenerator gen;
                        gen = part.GetComponent<ModuleGenerator>();
                        part.RemoveModule(gen);
                    }
                    if (part.GetComponent<ModuleResourceConverter>() != null)
                    {
                        ModuleResourceConverter isru;
                        isru = part.GetComponent<ModuleResourceConverter>(); //could reduce efficiency, increase heat per hit
                        part.RemoveModule(isru);
                    }
                    if (part.GetComponent<ModuleResourceConverter>() != null)
                    {
                        ModuleTurret turret;
                        turret = part.GetComponent<ModuleTurret>(); //could reduce traverse speed, range per hit
                        part.RemoveModule(turret);
                    }
                    if (part.GetComponent<ModuleTargetingCamera>() != null)
                    {
                        ModuleTargetingCamera cam;
                        cam = part.GetComponent<ModuleTargetingCamera>(); // gimbal range??
                        part.RemoveModule(cam);
                    }
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD DEBUG] " + part.name + "took subsystem damage");
                }
            }
            //Command parts
            if (BDArmorySettings.BD_COCKPITS)
            {
                if (part.GetComponent<ModuleCommand>() != null)
                {
                    double ControlDiceRoll = UnityEngine.Random.Range(0, 100);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Command DiceRoll: " + ControlDiceRoll);
                    if (ControlDiceRoll <= (BDArmorySettings.BD_DAMAGE_CHANCE * 2))
                    {
                        using (List<Part>.Enumerator craftPart = part.vessel.parts.GetEnumerator())
                        {
                            using (List<BDModulePilotAI>.Enumerator control = part.vessel.FindPartModulesImplementing<BDModulePilotAI>().GetEnumerator())
                                while (control.MoveNext())
                                {
                                    if (control.Current == null) continue;
                                    control.Current.evasionThreshold += 10; //pilot jitteriness increases
                                    control.Current.maxSteer *= 0.9f;
                                    if (control.Current.steerDamping > 0.5f) //damage to controls
                                    {
                                        control.Current.steerDamping -= 0.5f;
                                    }
                                    if (control.Current.dynamicSteerDampingPitchFactor > 0.5f)
                                    {
                                        control.Current.dynamicSteerDampingPitchFactor -= 0.5f;
                                    }
                                    if (control.Current.dynamicSteerDampingRollFactor > 0.5f)
                                    {
                                        control.Current.dynamicSteerDampingRollFactor -= 0.5f;
                                    }
                                    if (control.Current.dynamicSteerDampingYawFactor > 0.5f)
                                    {
                                        control.Current.dynamicSteerDampingYawFactor -= 0.5f;
                                    }
                                }
                            //GuardRange reduction to sim canopy/sensor damage?
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD DEBUG] " + part.name + "took command damage");
                        }
                    }
                }
                if (part.protoModuleCrew.Count > 0 && penetrationFactor > 1 && part.GetDamagePercentatge() < 0.95f)
                {
                    if (BDArmorySettings.BD_PILOT_KILLS)
                    {
                        float PilotTAC = Mathf.Clamp((BDArmorySettings.BD_DAMAGE_CHANCE / part.mass), 0.01f, 100); //larger cockpits = greater volume = less chance any hit will pass through a region of volume containing a pilot
                        float killchance = UnityEngine.Random.Range(0, 100);
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BD Debug]: Pilot TAC: " + PilotTAC + "; dice roll: " + killchance);
                        if (killchance <= PilotTAC) //add penetrationfactor threshold? hp threshold?
                        {
                            ProtoCrewMember crewMember = part.protoModuleCrew.FirstOrDefault(x => x != null);
                            if (crewMember != null)
                            {
                                crewMember.UnregisterExperienceTraits(part);
                                //crewMember.outDueToG = true; //implement temp KO to simulate wounding?
                                crewMember.Die();
                                if (part.isKerbalEVA())
                                {
                                    part.Die();
                                }
                                else
                                {
                                    part.RemoveCrewmember(crewMember); // sadly, I wasn't able to get the K.I.A. portrait working
                                }
                                //Vessel.CrewWasModified(part.vessel);
                                //Debug.Log(crewMember.name + " was killed by damage to cabin!");
                                if (HighLogic.CurrentGame.Parameters.Difficulty.MissingCrewsRespawn)
                                {
                                    crewMember.StartRespawnPeriod();
                                }
                                //ScreenMessages.PostScreenMessage(crewMember.name + " killed by damage to " + part.vessel.name + part.partName + ".", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                                ScreenMessages.PostScreenMessage("Cockpit snipe! " + crewMember.name + " killed!", 5.0f, ScreenMessageStyle.UPPER_LEFT);
                            }
                        }
                    }
                }

            }
        }
    }
}
