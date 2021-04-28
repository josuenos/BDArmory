
using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.Core.Utils;
using BDArmory.FX;
using System;
using System.Text;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace BDArmory.Modules
{
    class ModuleCASE : PartModule, IPartMassModifier, IPartCostModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => CASEmass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => CASEcost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        private double ammoMass;
        private double ammoQuantity;
        private double ammoExplosionYield = 0;

        private string explModelPath = "BDArmory/Models/explosion/explosion";
        private string explSoundPath = "BDArmory/Sounds/explode1";

        private string limitEdexploModelPath = "BDArmory/Models/explosion/30mmExplosion";
        private string shuntExploModelPath = "BDArmory/Models/explosion/CASEexplosion";

        public string SourceVessel;
        public bool hasDetonated;
        private float blastRadius;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.explosionPotential = 1.0f;
                part.force_activate();
            }
        }
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_AddedMass")]//CASE mass

        public float CASEmass = 0f;

        private float CASEcost = 0f;
        // private float origCost = 0;
        private float origMass = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_CASE"),//Cellular Ammo Storage Equipment Tier
        UI_FloatRange(minValue = 0f, maxValue = 2f, stepIncrement = 1f, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float CASELevel = 0; //tier of ammo storage. 0 = nothing, ammosplosion; 1 = base, ammosplosion contained(barely), 2 = blast safely shunted outside, minimal damage to surrounding parts

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                var internalmag = part.FindModuleImplementing<ModuleWeapon>();
                if (internalmag != null)
                {
                    Fields["CASELevel"].guiActiveEditor = false;
                    Fields["CASEmass"].guiActiveEditor = false;
                }
                else
                {
                    UI_FloatRange ATrangeEditor = (UI_FloatRange)Fields["CASELevel"].uiControlEditor;
                    ATrangeEditor.onFieldChanged = CASESetup;
                    origMass = part.mass;
                    //origScale = part.rescaleFactor;
                    CASESetup(null, null);
                }
            }
        }
        void CASESetup(BaseField field, object obj)
        {
            CASEmass = ((origMass / 2) * CASELevel);
            //part.mass = CASEmass;
            CASEcost = (CASELevel * 1000);
            //part.transform.localScale = (Vector3.one * (origScale + (CASELevel/10)));
            //Debug.Log("[BDArmory.ModuleCASE] part.mass = " + part.mass + "; CASElevel = " + CASELevel + "; CASEMass = " + CASEmass + "; Scale = " + part.transform.localScale);
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            if (part.partInfo != null)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    CASESetup(null, null);
                }
                else
                {
                    if (part.vessel != null)
                    {
                        var CASEString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleCASE", "CASELevel");
                        if (!string.IsNullOrEmpty(CASEString))
                        {
                            try
                            {
                                CASELevel = float.Parse(CASEString);
                                CASESetup(null, null);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleCASE]: Exception parsing CASELevel: " + e.Message);
                            }
                        }
                        else
                            CASELevel = 0f;
                    }
                    else // Don't.
                    {
                        enabled = false;
                    }
                }
            }
        }

        private List<PartResource> GetResources()
        {
            List<PartResource> resources = new List<PartResource>();

            foreach (PartResource resource in part.Resources)
            {
                if (!resources.Contains(resource)) { resources.Add(resource); }
            }
            return resources;
        }
        private void CalculateBlast()
        {
            foreach (PartResource resource in GetResources())
            {
                var resources = part.Resources.ToList();
                using (IEnumerator<PartResource> ammo = resources.GetEnumerator())
                    while (ammo.MoveNext())
                    {
                        if (ammo.Current == null) continue;
                        if (ammo.Current.resourceName == resource.resourceName)
                        {
                            ammoMass = ammo.Current.info.density;
                            ammoQuantity = ammo.Current.amount;
                            ammoExplosionYield += (((ammoMass * 1000) * ammoQuantity) / 6);
                        }
                    }
            }
            blastRadius = BlastPhysicsUtils.CalculateBlastRange(ammoExplosionYield);
        }
        public float GetBlastRadius()
        {
            CalculateBlast();
            return blastRadius;
        }
        public void DetonateIfPossible()
        {
            if (!hasDetonated)
            {
                hasDetonated = true; // Set hasDetonated here to avoid recursive calls due to ammo boxes exploding each other.
                var vesselName = vessel != null ? vessel.vesselName : null;
                Vector3 direction = default(Vector3);
                GetBlastRadius();
                if (CASELevel == 0) //a considerable quantity of explosives and propellants just detonated inside your ship
                {
                    ExplosionFx.CreateExplosion(part.transform.position, (float)ammoExplosionYield, explModelPath, explSoundPath, ExplosionSourceType.Missile, 0, part, vesselName, null, direction, false, part.mass*1000);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE 0 explosion, tntMassEquivilent: " + ammoExplosionYield);
                }
                else if (CASELevel == 1) // the blast is reduced. Damage is severe, but (potentially) survivable
                {
                    ExplosionFx.CreateExplosion(part.transform.position, ((float)ammoExplosionYield / 2), limitEdexploModelPath, explSoundPath, ExplosionSourceType.Missile, 0, part, vesselName, null, direction, true);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE I explosion, tntMassEquivilent: " + ammoExplosionYield + ", part: " + part + ", vessel: " + vesselName);
                    using (var blastHits = Physics.OverlapSphere(part.transform.position, blastRadius / 2, 9076737).AsEnumerable().GetEnumerator())
                    {
                        while (blastHits.MoveNext())
                        {
                            if (blastHits.Current == null) continue;
                            try
                            {
                                Part partHit = blastHits.Current.GetComponentInParent<Part>();
                                if (partHit == null || partHit == part) continue;
                                if (partHit.mass > 0)
                                {
                                    Rigidbody rb = partHit.Rigidbody;
                                    if (rb == null) continue;
                                    Vector3 distToG0 = part.transform.position - partHit.transform.position;

                                    Ray LoSRay = new Ray(part.transform.position, partHit.transform.position - part.transform.position);
                                    RaycastHit hit;
                                    if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737))
                                    {
                                        if (hit.collider.gameObject != FlightGlobals.currentMainBody.gameObject)
                                        {
                                            KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                            Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                                            if (p == partHit)
                                            {
                                                ApplyDamage(p, hit);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleCASE]: Exception in AmmoExplosion Hit: " + e.Message + "\n" + e.StackTrace);
                            }
                        }
                    }
                }
                else //if (CASELevel == 2) //blast contained, shunted out side of hull, minimal damage
                {
                    ExplosionFx.CreateExplosion(part.transform.position, (float)ammoExplosionYield, shuntExploModelPath, explSoundPath, ExplosionSourceType.Missile, 0, part, vesselName, null, direction, true);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE II explosion, tntMassEquivilent: " + ammoExplosionYield);
                    Ray BlastRay = new Ray(part.transform.position, part.transform.up);
                    var hits = Physics.RaycastAll(BlastRay, blastRadius, 9076737);
                    if (hits.Length > 0)
                    {
                        var orderedHits = hits.OrderBy(x => x.distance);

                        using (var hitsEnu = orderedHits.GetEnumerator())
                        {
                            while (hitsEnu.MoveNext())
                            {
                                RaycastHit hit = hitsEnu.Current;
                                Part hitPart = null;
                                KerbalEVA hitEVA = null;

                                if (hit.collider.gameObject != FlightGlobals.currentMainBody.gameObject)
                                {
                                    try
                                    {
                                        hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                                        hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                    }
                                    catch (NullReferenceException e)
                                    {
                                        Debug.LogError("[BDArmory.ModuleCASE]: NullReferenceException for AmmoExplosion Hit: " + e.Message + "\n" + e.StackTrace);
                                        continue;
                                    }

                                    if (hitPart == null || hitPart == part) continue;

                                    if (hitEVA != null)
                                    {
                                        hitPart = hitEVA.part;
                                        if (hitPart.rb != null)
                                            ApplyDamage(hitPart, hit);
                                        break;
                                    }
                                    ApplyDamage(hitPart, hit);
                                }
                            }
                        }
                    }
                }
                if (part.vessel != null) // Already in the process of being destroyed.
                    part.Destroy();
            }
        }
        private void ApplyDamage(Part hitPart, RaycastHit hit)
        {
            //hitting a vessel Part
            //No struts, they cause weird bugs :) -BahamutoD
            if (hitPart == null) return;
            if (hitPart.partInfo.name.Contains("Strut")) return;
            float explDamage = 0;
            if (BDArmorySettings.BULLET_HITS)
            {
                BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, false, 200, 3);
            }
            if (CASELevel == 2)
            {
                explDamage = 100;
                hitPart.AddDamage(explDamage);
                float armorToReduce = hitPart.GetArmorThickness() * 0.25f;
                hitPart.ReduceArmor(armorToReduce);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE]" + hitPart.name + "damaged, armor reduced by " + armorToReduce);
            }
            else //CASE I
            {
                explDamage = (hitPart.Modules.GetModule<HitpointTracker>().GetMaxHitpoints() * 0.9f);
                explDamage = Mathf.Clamp(explDamage, 0, 600);
                hitPart.AddDamage(explDamage);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE]" + hitPart.name + "damaged for " + (hitPart.MaxDamage() * 0.9f));
                if (BDArmorySettings.BATTLEDAMAGE)
                {
                    Misc.BattleDamageHandler.CheckDamageFX(hitPart, 200, 3, true, SourceVessel, hit);
                }
            }
            {
                var aName = SourceVessel;
                var tName = part.vessel.GetName();

                if (aName != null && tName != null && aName != tName && BDACompetitionMode.Instance.Scores.ContainsKey(aName) && BDACompetitionMode.Instance.Scores.ContainsKey(tName))
                {
                    if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                    {
                        BDAScoreService.Instance.TrackDamage(aName, tName, explDamage);
                    }
                    var aData = BDACompetitionMode.Instance.Scores[aName];
                    aData.Score += 1;

                    if (part.vessel.GetName() == "Pinata")
                    {
                        aData.PinataHits++;
                    }

                    var tData = BDACompetitionMode.Instance.Scores[tName];
                    tData.lastPersonWhoHitMe = aName;
                    tData.lastHitTime = Planetarium.GetUniversalTime();
                    tData.everyoneWhoHitMe.Add(aName);
                    // Track hits
                    if (tData.hitCounts.ContainsKey(aName))
                        ++tData.hitCounts[aName];
                    else
                        tData.hitCounts.Add(aName, 1);
                    // Track damage
                    if (tData.damageFromBullets.ContainsKey(aName))
                        tData.damageFromBullets[aName] += explDamage;
                    else
                        tData.damageFromBullets.Add(aName, explDamage);

                }
            }
        }
        void OnDestroy()
        {
            if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_AMMOBINS && BDArmorySettings.BD_VOLATILE_AMMO && HighLogic.LoadedSceneIsFlight && !VesselSpawner.Instance.vesselsSpawning)
            {
                DetonateIfPossible();
            }
        }
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            var internalmag = part.FindModuleImplementing<ModuleWeapon>();
            if (internalmag != null)
            {
                output.AppendLine($" Has Intrinsic C.A.S.E. Type {CASELevel}");
            }
            else
            {
                output.AppendLine($"Can add Cellular Ammo Storage Equipment to reduce ammo explosion damage");
            }

            output.AppendLine("");

            return output.ToString();
        }
    }
}

