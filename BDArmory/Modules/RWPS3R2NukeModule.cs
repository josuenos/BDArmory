using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.FX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    class RWPS3R2NukeModule : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "WARNING: Reactor Safeties:", guiActiveEditor = true), UI_Label(affectSymCounterparts = UI_Scene.All, scene = UI_Scene.All)]//Weapon Name
        public string status = "OFFLINE";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Coolant Remaining", guiActiveEditor = false), UI_Label(scene = UI_Scene.All)]
        public double fuelleft;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField]
        public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPField(isPersistant = true)]
        public float thermalRadius = 750;

        [KSPField(isPersistant = true)]
        public float yield = 0.05f;
        float yieldCubeRoot;

        [KSPField(isPersistant = true)]
        public float fluence = 0.05f;

        [KSPField(isPersistant = true)]
        public float tntEquivilent = 500;

        [KSPField(isPersistant = true)]
        public float ADTimer = 20;

        private int FuelID;
        private bool hasDetonated = false;
        private bool goingCritical = false;
        private float lastValidAtmDensity = 0f;
        public string Sourcevessel;
        HashSet<Part> partsHit = new HashSet<Part>();

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                FuelID = PartResourceLibrary.Instance.GetDefinition("LiquidFuel").id;
                vessel.GetConnectedResourceTotals(FuelID, out double fuelCurrent, out double fuelMax);
                fuelleft = fuelCurrent;
                Sourcevessel = part.vessel.GetName();
                var engine = part.FindModuleImplementing<ModuleEngines>();
                if (engine != null)
                {
                    engine.allowShutdown = false;
                }
                yieldCubeRoot = Mathf.Pow(yield, 1f / 3f);
                part.force_activate();
                part.OnJustAboutToBeDestroyed += Detonate;
                GameEvents.onVesselPartCountChanged.Add(CheckAttached);
                GameEvents.onVesselCreate.Add(CheckAttached);
            }
            base.OnStart(state);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDACompetitionMode.Instance.competitionIsActive) //only begin checking engine state after comp start
                {
                    if (vessel.atmDensity > 0)
                        lastValidAtmDensity = (float)vessel.atmDensity;
                    vessel.GetConnectedResourceTotals(FuelID, out double fuelCurrent, out double fuelMax);
                    fuelleft = fuelCurrent;
                    if (fuelleft <= 0)
                    {
                        if (!hasDetonated && !goingCritical)
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: nerva on " + Sourcevessel + " is out of fuel.");
                            StartCoroutine(DelayedDetonation(0.5f)); //bingo fuel, detonate
                        }
                    }
                    var engine = part.FindModuleImplementing<ModuleEngines>();
                    if (engine != null)
                    {
                        if (!engine.isEnabled || !engine.EngineIgnited)
                        {
                            if (!hasDetonated)
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: nerva on " + Sourcevessel + " is Off, detonating");
                                Detonate(); //nuke engine off after comp start, detonate.
                            }
                        }
                        if (engine.thrustPercentage < 100)
                        {
                            if (part.Modules.GetModule<HitpointTracker>().Hitpoints == part.Modules.GetModule<HitpointTracker>().GetMaxHitpoints())
                            {
                                if (!hasDetonated)
                                {
                                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: nerva on " + Sourcevessel + " is manually thrust limited, detonating");
                                    Detonate(); //nuke engine off after comp start, detonate.
                                }
                            }
                        }
                    }
                }
            }
        }

        void CheckAttached(Vessel v)
        {
            if (v == vessel && !hasDetonated && !goingCritical)
            {
                if (v.FindPartModuleImplementing<MissileFire>() == null)
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Nuclear engine on " + Sourcevessel + " has become detached.");
                    goingCritical = true;
                    StartCoroutine(DelayedDetonation(0.5f));
                }
            }
        }

        IEnumerator DelayedDetonation(float delay)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Nuclear engine on " + Sourcevessel + " going critical in " + delay.ToString("0.0") + "s.");
            goingCritical = true;
            yield return new WaitForSeconds(delay);
            if (!hasDetonated && part != null) Detonate();
        }

        public void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(CheckAttached);
            GameEvents.onVesselCreate.Remove(CheckAttached);
        }

        void Detonate() //borrowed from Stockalike Project Orion
        {
            if (hasDetonated || FlightGlobals.currentMainBody == null || VesselSpawner.Instance.vesselsSpawning) // Don't trigger on scene changes or during spawning.
            {
                return;
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Running Detonate() on nerva in vessel " + Sourcevessel);
            //affect any nearby parts/vessels that aren't the source vessel

            Dictionary<string, int> vesselsHitByMissiles = new Dictionary<string, int>();

            using (var blastHits = Physics.OverlapSphere(part.transform.position, thermalRadius, 9076737).AsEnumerable().GetEnumerator())
            {
                partsHit.Clear();
                while (blastHits.MoveNext())
                {
                    if (blastHits.Current == null) continue;
                    if (blastHits.Current.gameObject == FlightGlobals.currentMainBody.gameObject) continue; // Ignore terrain hits.
                    Part partHit = blastHits.Current.GetComponentInParent<Part>();
                    if (partsHit.Contains(partHit)) continue; // Don't hit the same part multiple times.
                    partsHit.Add(partHit);
                    if (partHit != null && partHit.mass > 0)
                    {
                        var distToG0 = Math.Max((part.transform.position - partHit.transform.position).magnitude, 1f);
                        float radiativeArea = !double.IsNaN(partHit.radiativeArea) ? (float)partHit.radiativeArea : partHit.GetArea();
                        if (BDArmorySettings.DRAW_DEBUG_LABELS && double.IsNaN(partHit.radiativeArea))
                        {
                            Debug.Log("[BDArmory.NukeTest]: radiative area of part " + partHit + " was NaN, using approximate area " + radiativeArea + " instead.");
                        }
                        //if (partHit.vessel != this.vessel)
                        if (partHit != part)
                        {
                            partHit.skinTemperature += fluence * 3370000000 / (4 * Math.PI * Math.Pow(distToG0, 2.0)) * radiativeArea / 2; // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
                        } // everything gets heated via atmosphere

                        Ray LoSRay = new Ray(part.transform.position, partHit.transform.position - part.transform.position);
                        RaycastHit hit;
                        if (Physics.Raycast(LoSRay, out hit, distToG0, 9076737)) // only add impulse to parts with line of sight to detonation
                        {
                            KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                            Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                            float blastDamage = 100;
                            if (p == partHit)
                            {
                                //if (p.vessel != this.vessel)
                                if (p != part)
                                {
                                    // Forces
                                    if (p.rb != null && p.rb.mass > 0) // Don't apply forces to physicsless parts.
                                    {
                                        var blastImpulse = Mathf.Pow(3.01f * 1100f / distToG0, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot * radiativeArea / 3f;
                                        // Math.Pow(Math.Pow(Math.Pow(9.54e-3 * 2200.0 / distToG0, 1.95), 4.0) + Math.Pow(Math.Pow(3.01 * 1100.0 / distToG0, 1.25), 4.0), 0.25) * 6.894 * vessel.atmDensity * Math.Pow(yield, 1.0 / 3.0) * partHit.radiativeArea / 3.0; //assuming a 0.05 kT yield
                                        if (float.IsNaN(blastImpulse))
                                        {
                                            Debug.LogWarning("[BDArmory.NukeTest]: blast impulse is NaN. distToG0: " + distToG0 + ", vessel: " + vessel + ", atmDensity: " + lastValidAtmDensity + ", yield^(1/3): " + yieldCubeRoot + ", partHit: " + partHit + ", radiativeArea: " + radiativeArea);
                                        }
                                        else
                                        {
                                            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: Applying " + blastImpulse.ToString("0.0") + " impulse to " + p + " of mass " + p.mass + " at distance " + distToG0 + "m");
                                            p.rb.AddForceAtPosition((partHit.transform.position - part.transform.position).normalized * (float)blastImpulse, partHit.transform.position, ForceMode.Impulse);
                                        }
                                    }

                                    // Damage
                                    blastDamage = ((float)((yield * 3370000000) / (4f * Mathf.PI * distToG0 * distToG0) * (radiativeArea / 2f)));
                                    if (float.IsNaN(blastDamage))
                                    {
                                        Debug.LogWarning("[BDArmory.NukeTest]: blast damage is NaN. distToG0: " + distToG0 + ", yield: " + yield + ", part: " + partHit + ", radiativeArea: " + radiativeArea);
                                        continue;
                                    }
                                    p.AddExplosiveDamage(blastDamage, 100, ExplosionSourceType.Missile);

                                    // Scoring
                                    var aName = Sourcevessel; // Attacker
                                    var tName = p.vessel.GetName(); // Target
                                    if (tName != null && aName != tName && BDACompetitionMode.Instance.Scores.ContainsKey(tName) && BDACompetitionMode.Instance.Scores.ContainsKey(aName))
                                    {
                                        // Part hit counts
                                        if (BDACompetitionMode.Instance.Scores[tName].missilePartDamageCounts.ContainsKey(aName))
                                            ++BDACompetitionMode.Instance.Scores[tName].missilePartDamageCounts[aName];
                                        else
                                            BDACompetitionMode.Instance.Scores[tName].missilePartDamageCounts[aName] = 1;
                                        if (!BDACompetitionMode.Instance.Scores[tName].everyoneWhoHitMeWithMissiles.Contains(aName))
                                            BDACompetitionMode.Instance.Scores[tName].everyoneWhoHitMeWithMissiles.Add(aName);
                                        ++BDACompetitionMode.Instance.Scores[aName].totalDamagedPartsDueToMissiles;
                                        BDACompetitionMode.Instance.Scores[tName].lastMissileHitTime = Planetarium.GetUniversalTime();
                                        BDACompetitionMode.Instance.Scores[tName].lastPersonWhoHitMeWithAMissile = aName;
                                        if (vesselsHitByMissiles.ContainsKey(tName))
                                            ++vesselsHitByMissiles[tName];
                                        else
                                            vesselsHitByMissiles[tName] = 1;
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            BDAScoreService.Instance.TrackMissileParts(aName, tName, 1);

                                        // Part damage scoring
                                        var tData = BDACompetitionMode.Instance.Scores[tName];
                                        if (tData.damageFromMissiles.ContainsKey(aName))
                                            tData.damageFromMissiles[aName] += blastDamage;
                                        else
                                            tData.damageFromMissiles.Add(aName, blastDamage);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            BDAScoreService.Instance.TrackMissileDamage(aName, tName, blastDamage);
                                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.NukeTest]: " + aName + " did " + blastDamage + " blast damage to " + tName + " at " + distToG0.ToString("0.000") + "m");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {

                        DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

                        if (building != null)
                        {
                            var distToEpicenter = Mathf.Max((part.transform.position - building.transform.position).magnitude, 1f);
                            var blastImpulse = Mathf.Pow(3.01f * 1100f / distToEpicenter, 1.25f) * 6.894f * lastValidAtmDensity * yieldCubeRoot;
                            // blastImpulse = (((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToEpicenter)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToEpicenter)), 1.25)), 4.0)), 0.25)) * 6.894) * (vessel.atmDensity)) * Math.Pow(yield, (1.0 / 3.0))));
                            if (!double.IsNaN(blastImpulse) && blastImpulse > 140) //140kPa, level at which reinforced concrete structures are destroyed
                            {
                                building.Demolish();
                            }
                        }
                    }
                }
            }
            if (vesselsHitByMissiles.Count > 0)
            {
                string message = "";
                foreach (var vesselName in vesselsHitByMissiles.Keys)
                    message += (message == "" ? "" : " and ") + vesselName + " had " + vesselsHitByMissiles[vesselName];
                message += " parts damaged (Blast Wave) by " + Sourcevessel + "'s exploding engine core.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDArmory.NukeTest]: " + message);
            }
            ExplosionFx.CreateExplosion(part.transform.position, 1, explModelPath, explSoundPath, ExplosionSourceType.Missile, 0, null, Sourcevessel, "Reactor Containment Failure");
            hasDetonated = true;
            if (part.vessel != null) // Already in the process of being destroyed.
                part.Destroy();
        }
    }
}
