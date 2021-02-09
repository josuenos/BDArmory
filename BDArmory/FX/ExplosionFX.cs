using System;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Bullets;
using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.FX
{
    public class ExplosionFx : MonoBehaviour
    {
        public static Dictionary<string, ObjectPool> explosionFXPools = new Dictionary<string, ObjectPool>();
        public KSPParticleEmitter[] pEmitters { get; set; }
        public Light LightFx { get; set; }
        public float StartTime { get; set; }
        public AudioClip ExSound { get; set; }
        public AudioSource audioSource { get; set; }
        private float MaxTime { get; set; }
        public float Range { get; set; }
        public float Caliber { get; set; }
        public ExplosionSourceType ExplosionSource { get; set; }
        public string SourceVesselName { get; set; }
        public float Power { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public Part ExplosivePart { get; set; }
        public bool isFX { get; set; }

        public float TimeIndex => Time.time - StartTime;

        private bool disabled = true;

        public Queue<BlastHitEvent> ExplosionEvents = new Queue<BlastHitEvent>();

        public static List<Part> IgnoreParts = new List<Part>();

        public static List<DestructibleBuilding> IgnoreBuildings = new List<DestructibleBuilding>();

        internal static readonly float ExplosionVelocity = 343f;

        private float particlesMaxEnergy;

        private void OnEnable()
        {
            StartTime = Time.time;
            disabled = false;
            MaxTime = Mathf.Sqrt((Range / ExplosionVelocity) * 3f) * 2f; // Scale MaxTime to get a reasonable visualisation of the explosion.
            if (!isFX)
            {
                CalculateBlastEvents();
            }
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    if (pe.maxEnergy > particlesMaxEnergy)
                        particlesMaxEnergy = pe.maxEnergy;
                    pe.emit = true;
                    var emission = pe.ps.emission;
                    emission.enabled = true;
                    EffectBehaviour.AddParticleEmitter(pe);
                }

            LightFx = gameObject.GetComponent<Light>();
            LightFx.range = Range * 3f;

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]:Explosion started tntMass: {" + Power + "}  BlastRadius: {" + Range + "} StartTime: {" + StartTime + "}, Duration: {" + MaxTime + "}");
            }
        }

        void OnDisable()
        {
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
            ExplosivePart = null; // Clear the Part reference.
        }

        private void CalculateBlastEvents()
        {
            var temporalEventList = new List<BlastHitEvent>();

            temporalEventList.AddRange(ProcessingBlastSphere());

            //Let's convert this temporal list on a ordered queue
            using (var enuEvents = temporalEventList.OrderBy(e => e.TimeToImpact).GetEnumerator())
            {
                while (enuEvents.MoveNext())
                {
                    if (enuEvents.Current == null) continue;

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: Enqueueing Blast Event");
                    }

                    ExplosionEvents.Enqueue(enuEvents.Current);
                }
            }
        }

        private List<BlastHitEvent> ProcessingBlastSphere()
        {
            List<BlastHitEvent> result = new List<BlastHitEvent>();
            List<Part> partsAdded = new List<Part>();
            List<DestructibleBuilding> buildingAdded = new List<DestructibleBuilding>();
            Dictionary<string, int> vesselsHitByMissiles = new Dictionary<string, int>();

            using (var hitCollidersEnu = Physics.OverlapSphere(Position, Range, 9076737).AsEnumerable().GetEnumerator())
            {
                while (hitCollidersEnu.MoveNext())
                {
                    if (hitCollidersEnu.Current == null) continue;

                    Part partHit = hitCollidersEnu.Current.GetComponentInParent<Part>();

                    if (partHit != null && partHit.mass > 0 && !partsAdded.Contains(partHit))
                    {
                        string sourceVesselName = null;
                        if (BDACompetitionMode.Instance)
                        {
                            switch (ExplosionSource)
                            {
                                case ExplosionSourceType.Missile:
                                    var explosivePart = ExplosivePart ? ExplosivePart.FindModuleImplementing<BDExplosivePart>() : null;
                                    sourceVesselName = explosivePart ? explosivePart.sourcevessel.GetName() : SourceVesselName;
                                    break;
                                case ExplosionSourceType.Bullet:
                                    sourceVesselName = SourceVesselName;
                                    break;
                                default:
                                    break;
                            }
                        }
                        var damaged = ProcessPartEvent(partHit, sourceVesselName, result, partsAdded);
                        // If the explosion derives from a missile explosion, count the parts damaged for missile hit scores.
                        if (damaged && ExplosionSource == ExplosionSourceType.Missile && BDACompetitionMode.Instance)
                        {
                            if (sourceVesselName != null && BDACompetitionMode.Instance.Scores.ContainsKey(sourceVesselName)) // Check that the source vessel is in the competition.
                            {
                                var damagedVesselName = partHit.vessel?.GetName();
                                if (damagedVesselName != null && damagedVesselName != sourceVesselName && BDACompetitionMode.Instance.Scores.ContainsKey(damagedVesselName)) // Check that the damaged vessel is in the competition and isn't the source vessel.
                                {
                                    if (BDACompetitionMode.Instance.Scores[damagedVesselName].missilePartDamageCounts.ContainsKey(sourceVesselName))
                                        ++BDACompetitionMode.Instance.Scores[damagedVesselName].missilePartDamageCounts[sourceVesselName];
                                    else
                                        BDACompetitionMode.Instance.Scores[damagedVesselName].missilePartDamageCounts[sourceVesselName] = 1;
                                    if (!BDACompetitionMode.Instance.Scores[damagedVesselName].everyoneWhoHitMeWithMissiles.Contains(sourceVesselName))
                                        BDACompetitionMode.Instance.Scores[damagedVesselName].everyoneWhoHitMeWithMissiles.Add(sourceVesselName);
                                    ++BDACompetitionMode.Instance.Scores[sourceVesselName].totalDamagedPartsDueToMissiles;
                                    BDACompetitionMode.Instance.Scores[damagedVesselName].lastMissileHitTime = Planetarium.GetUniversalTime();
                                    BDACompetitionMode.Instance.Scores[damagedVesselName].lastPersonWhoHitMeWithAMissile = sourceVesselName;
                                    if (vesselsHitByMissiles.ContainsKey(damagedVesselName))
                                        ++vesselsHitByMissiles[damagedVesselName];
                                    else
                                        vesselsHitByMissiles[damagedVesselName] = 1;
                                    if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                        BDAScoreService.Instance.TrackMissileParts(sourceVesselName, damagedVesselName, 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        DestructibleBuilding building = hitCollidersEnu.Current.GetComponentInParent<DestructibleBuilding>();

                        if (building != null && !buildingAdded.Contains(building))
                        {
                            ProcessBuildingEvent(building, result, buildingAdded);
                        }
                    }
                }
            }
            if (vesselsHitByMissiles.Count > 0)
            {
                string message = "";
                foreach (var vesselName in vesselsHitByMissiles.Keys)
                    message += (message == "" ? "" : " and ") + vesselName + " had " + vesselsHitByMissiles[vesselName];
                message += " parts damaged due to missile strike.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                // Note: damage hasn't actually been applied to the parts yet, just assigned as events, so we can't know if they survived.
            }
            return result;
        }

        private void ProcessBuildingEvent(DestructibleBuilding building, List<BlastHitEvent> eventList, List<DestructibleBuilding> bulidingAdded)
        {
            Ray ray = new Ray(Position, building.transform.position - Position);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, Range, 557057))
            {
                //TODO: Maybe we are not hitting building because we are hitting explosive parts.

                DestructibleBuilding destructibleBuilding = rayHit.collider.GetComponentInParent<DestructibleBuilding>();

                // Is not a direct hit, because we are hitting a different part
                if (destructibleBuilding != null && destructibleBuilding.Equals(building))
                {
                    var distance = Vector3.Distance(Position, rayHit.point);
                    eventList.Add(new BuildingBlastHitEvent() { Distance = Vector3.Distance(Position, rayHit.point), Building = building, TimeToImpact = distance / ExplosionVelocity });
                    bulidingAdded.Add(building);
                }
            }
        }

        private bool ProcessPartEvent(Part part, string sourceVesselName, List<BlastHitEvent> eventList, List<Part> partsAdded)
        {
            RaycastHit hit;
            float distance = 0;
            if (IsInLineOfSight(part, ExplosivePart, out hit, out distance))
            {
                if (IsAngleAllowed(Direction, hit))
                {
                    //Adding damage hit
                    eventList.Add(new PartBlastHitEvent()
                    {
                        Distance = distance,
                        Part = part,
                        TimeToImpact = distance / ExplosionVelocity,
                        HitPoint = hit.point,
                        Hit = hit,
                        SourceVesselName = sourceVesselName
                    });
                    partsAdded.Add(part);
                    return true;
                }
            }
            return false;
        }

        private bool IsAngleAllowed(Vector3 direction, RaycastHit hit)
        {
            if (ExplosionSource == ExplosionSourceType.Missile || direction == default(Vector3))
            {
                return true;
            }

            return Vector3.Angle(direction, (hit.point - Position).normalized) < 100f;
        }

        /// <summary>
        /// This method will calculate if there is valid line of sight between the explosion origin and the specific Part
        /// In order to avoid collisions with the same missile part, It will not take into account those parts beloging to same vessel that contains the explosive part
        /// </summary>
        /// <param name="part"></param>
        /// <param name="explosivePart"></param>
        /// <param name="hit"> out property with the actual hit</param>
        /// <returns></returns>
        private bool IsInLineOfSight(Part part, Part explosivePart, out RaycastHit hit, out float distance)
        {
            Ray partRay = new Ray(Position, part.transform.position - Position);

            var hits = Physics.RaycastAll(partRay, Range, 9076737).AsEnumerable();
            using (var hitsEnu = hits.OrderBy(x => x.distance).GetEnumerator())
            {
                while (hitsEnu.MoveNext())
                {
                    Part partHit = hitsEnu.Current.collider.GetComponentInParent<Part>();
                    if (partHit == null) continue;
                    hit = hitsEnu.Current;
                    distance = Vector3.Distance(Position, hit.point);
                    if (partHit == part)
                    {
                        return true;
                    }
                    if (partHit != part)
                    {
                        // ignoring collisions against the explosive
                        if (explosivePart != null && partHit.vessel == explosivePart.vessel)
                        {
                            continue;
                        }
                        // if there are parts in between but we still inside the critical sphere of damage.
                        if (distance <= 0.1f * Range)
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            hit = new RaycastHit();
            distance = 0;
            return false;
        }

        public void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (LightFx != null) LightFx.intensity -= 12 * Time.deltaTime;

            if (!disabled && TimeIndex > 0.3f && pEmitters != null) // 0.3s seems to be enough to always show the explosion, but 0.2s isn't for some reason.
            {
                foreach (var pe in pEmitters)
                {
                    if (pe == null) continue;
                    pe.emit = false;
                }
                disabled = true;
            }
        }

        public void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            //floating origin and velocity offloading corrections
            if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
            {
                transform.position -= FloatingOrigin.OffsetNonKrakensbane;
            }
            if (!isFX)
            {
                while (ExplosionEvents.Count > 0 && ExplosionEvents.Peek().TimeToImpact <= TimeIndex)
                {
                    BlastHitEvent eventToExecute = ExplosionEvents.Dequeue();

                    var partBlastHitEvent = eventToExecute as PartBlastHitEvent;
                    if (partBlastHitEvent != null)
                    {
                        ExecutePartBlastEvent(partBlastHitEvent);
                    }
                    else
                    {
                        ExecuteBuildingBlastEvent((BuildingBlastHitEvent)eventToExecute);
                    }
                }
            }

            if (disabled && ExplosionEvents.Count == 0 && TimeIndex > MaxTime)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]:Explosion Finished");
                }

                gameObject.SetActive(false);
                return;
            }
        }

        private void ExecuteBuildingBlastEvent(BuildingBlastHitEvent eventToExecute)
        {
            //TODO: Review if the damage is sensible after so many changes
            //buildings
            DestructibleBuilding building = eventToExecute.Building;
            building.damageDecay = 600f;

            if (building)
            {
                var distanceFactor = Mathf.Clamp01((Range - eventToExecute.Distance) / Range);
                float damageToBuilding = (BDArmorySettings.DMG_MULTIPLIER / 100) * BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW * Power * distanceFactor;

                damageToBuilding *= 2f;

                building.AddDamage(damageToBuilding);

                if (building.Damage > building.impactMomentumThreshold)
                {
                    building.Demolish();
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: Explosion hit destructible building! Hitpoints Applied: " + Mathf.Round(damageToBuilding) +
                             ", Building Damage : " + Mathf.Round(building.Damage) +
                             " Building Threshold : " + building.impactMomentumThreshold);
                }
            }
        }

        private void ExecutePartBlastEvent(PartBlastHitEvent eventToExecute)
        {
            if (eventToExecute.Part == null || eventToExecute.Part.Rigidbody == null || eventToExecute.Part.vessel == null || eventToExecute.Part.partInfo == null) return;

            try
            {
                Part part = eventToExecute.Part;
                Rigidbody rb = part.Rigidbody;
                var realDistance = eventToExecute.Distance;

                if (!eventToExecute.IsNegativePressure)
                {
                    BlastInfo blastInfo =
                        BlastPhysicsUtils.CalculatePartBlastEffects(part, realDistance,
                            part.vessel.totalMass * 1000f, Power, Range);

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log(
                            "[BDArmory]: Executing blast event Part: {" + part.name + "}, " +
                            " VelocityChange: {" + blastInfo.VelocityChange + "}," +
                            " Distance: {" + realDistance + "}," +
                            " TotalPressure: {" + blastInfo.TotalPressure + "}," +
                            " Damage: {" + blastInfo.Damage + "}," +
                            " EffectiveArea: {" + blastInfo.EffectivePartArea + "}," +
                            " Positive Phase duration: {" + blastInfo.PositivePhaseDuration + "}," +
                            " Vessel mass: {" + Math.Round(part.vessel.totalMass * 1000f) + "}," +
                            " TimeIndex: {" + TimeIndex + "}," +
                            " TimePlanned: {" + eventToExecute.TimeToImpact + "}," +
                            " NegativePressure: {" + eventToExecute.IsNegativePressure + "}");
                    }

                    // Add Reverse Negative Event
                    ExplosionEvents.Enqueue(new PartBlastHitEvent()
                    {
                        Distance = Range - realDistance,
                        Part = part,
                        TimeToImpact = 2 * (Range / ExplosionVelocity) + (Range - realDistance) / ExplosionVelocity,
                        IsNegativePressure = true,
                        NegativeForce = blastInfo.VelocityChange * 0.25f
                    });

                    AddForceAtPosition(rb,
                        (eventToExecute.HitPoint + part.rb.velocity * TimeIndex - Position).normalized *
                        blastInfo.VelocityChange *
                        BDArmorySettings.EXP_IMP_MOD,
                        eventToExecute.HitPoint + part.rb.velocity * TimeIndex);

                    var damage = part.AddExplosiveDamage(blastInfo.Damage, Caliber, ExplosionSource);
                    if (BDArmorySettings.BATTLEDAMAGE)
                    {
                        Misc.BattleDamageHandler.CheckDamageFX(part, 50, 0.5f, true, SourceVesselName, eventToExecute.Hit);
                    }

                    // Update scoring structures
                    switch (ExplosionSource)
                    {
                        case ExplosionSourceType.Bullet:
                        case ExplosionSourceType.Missile:
                            var aName = eventToExecute.SourceVesselName; // Attacker
                            var tName = part.vessel.GetName(); // Target
                            if (aName != tName && BDACompetitionMode.Instance.Scores.ContainsKey(tName) && BDACompetitionMode.Instance.Scores.ContainsKey(aName))
                            {
                                var tData = BDACompetitionMode.Instance.Scores[tName];
                                // Track damage
                                switch (ExplosionSource)
                                {
                                    case ExplosionSourceType.Bullet:
                                        if (tData.damageFromBullets.ContainsKey(aName))
                                            tData.damageFromBullets[aName] += damage;
                                        else
                                            tData.damageFromBullets.Add(aName, damage);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            BDAScoreService.Instance.TrackDamage(aName, tName, damage);
                                        break;
                                    case ExplosionSourceType.Missile:
                                        if (tData.damageFromMissiles.ContainsKey(aName))
                                            tData.damageFromMissiles[aName] += damage;
                                        else
                                            tData.damageFromMissiles.Add(aName, damage);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            BDAScoreService.Instance.TrackMissileDamage(aName, tName, damage);
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log(
                            "[BDArmory]: Executing blast event Part: {" + part.name + "}, " +
                            " VelocityChange: {" + eventToExecute.NegativeForce + "}," +
                            " Distance: {" + realDistance + "}," +
                            " Vessel mass: {" + Math.Round(part.vessel.totalMass * 1000f) + "}," +
                            " TimeIndex: {" + TimeIndex + "}," +
                            " TimePlanned: {" + eventToExecute.TimeToImpact + "}," +
                            " NegativePressure: {" + eventToExecute.IsNegativePressure + "}");
                    }
                    AddForceAtPosition(rb, (Position - part.transform.position).normalized * eventToExecute.NegativeForce * BDArmorySettings.EXP_IMP_MOD * 0.25f, part.transform.position);
                }
            }
            catch
            {
                // ignored due to depending on previous event an object could be disposed
            }
        }

        // We use an ObjectPool for the ExplosionFx instances as they leak KSPParticleEmitters otherwise.
        static void CreateObjectPool(string explModelPath, string soundPath)
        {
            var key = explModelPath + soundPath;
            if (!explosionFXPools.ContainsKey(key) || explosionFXPools[key] == null)
            {
                var explosionFXTemplate = GameDatabase.Instance.GetModel(explModelPath);
                if (explosionFXTemplate == null)
                {
                    Debug.LogError("[ExplosionFX]: " + explModelPath + " was not found, using the default explosion instead. Please fix your model.");
                    explosionFXTemplate = GameDatabase.Instance.GetModel(ModuleWeapon.defaultExplModelPath);
                }
                var soundClip = GameDatabase.Instance.GetAudioClip(soundPath);
                if (soundClip == null)
                {
                    Debug.LogError("[ExplosionFX]: " + soundPath + " was not found, using the default sound instead. Please fix your model.");
                    soundClip = GameDatabase.Instance.GetAudioClip(ModuleWeapon.defaultExplSoundPath);
                }
                var eFx = explosionFXTemplate.AddComponent<ExplosionFx>();
                eFx.ExSound = soundClip;
                eFx.audioSource = explosionFXTemplate.AddComponent<AudioSource>();
                eFx.audioSource.minDistance = 200;
                eFx.audioSource.maxDistance = 5500;
                eFx.audioSource.spatialBlend = 1;
                eFx.LightFx = explosionFXTemplate.AddComponent<Light>();
                eFx.LightFx.color = Misc.Misc.ParseColor255("255,238,184,255");
                eFx.LightFx.intensity = 8;
                eFx.LightFx.shadows = LightShadows.None;
                explosionFXTemplate.SetActive(false);
                explosionFXPools[key] = ObjectPool.CreateObjectPool(explosionFXTemplate, 10, true, true, 0f, false);
            }
        }

        public static void CreateExplosion(Vector3 position, float tntMassEquivalent, string explModelPath, string soundPath, ExplosionSourceType explosionSourceType, float caliber = 0, Part explosivePart = null, string sourceVesselName = null, Vector3 direction = default(Vector3), bool isfx = false)
        {
            CreateObjectPool(explModelPath, soundPath);

            Quaternion rotation;
            if (direction == default(Vector3))
            {
                rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            }
            else
            {
                rotation = Quaternion.LookRotation(direction);
            }

            GameObject newExplosion = explosionFXPools[explModelPath + soundPath].GetPooledObject();
            newExplosion.transform.SetPositionAndRotation(position, rotation);
            ExplosionFx eFx = newExplosion.GetComponent<ExplosionFx>();
            eFx.Range = BlastPhysicsUtils.CalculateBlastRange(tntMassEquivalent);
            eFx.Position = position;
            eFx.Power = tntMassEquivalent;
            eFx.ExplosionSource = explosionSourceType;
            eFx.SourceVesselName = sourceVesselName != null ? sourceVesselName : explosionSourceType == ExplosionSourceType.Missile ? (explosivePart != null && explosivePart.vessel != null ? explosivePart.vessel.GetName() : null) : null; // Use the sourceVesselName if specified, otherwise get the sourceVesselName from the missile if it is one.
            eFx.Caliber = caliber;
            eFx.ExplosivePart = explosivePart;
            eFx.Direction = direction;
            eFx.isFX = isfx;
            eFx.pEmitters = newExplosion.GetComponentsInChildren<KSPParticleEmitter>();
            eFx.audioSource = newExplosion.GetComponent<AudioSource>();
            if (tntMassEquivalent <= 5)
            {
                eFx.audioSource.minDistance = 4f;
                eFx.audioSource.maxDistance = 3000;
                eFx.audioSource.priority = 9999;
            }
            newExplosion.SetActive(true);
        }

        public static void AddForceAtPosition(Rigidbody rb, Vector3 force, Vector3 position)
        {
            //////////////////////////////////////////////////////////
            // Add The force to part
            //////////////////////////////////////////////////////////
            if (rb == null) return;
            rb.AddForceAtPosition(force, position, ForceMode.VelocityChange);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: Force Applied | Explosive : " + Math.Round(force.magnitude, 2));
            }
        }
    }

    public abstract class BlastHitEvent
    {
        public float Distance { get; set; }
        public float TimeToImpact { get; set; }
        public bool IsNegativePressure { get; set; }
    }

    internal class PartBlastHitEvent : BlastHitEvent
    {
        public Part Part { get; set; }
        public Vector3 HitPoint { get; set; }
        public RaycastHit Hit { get; set; }
        public float NegativeForce { get; set; }
        public string SourceVesselName { get; set; }
    }

    internal class BuildingBlastHitEvent : BlastHitEvent
    {
        public DestructibleBuilding Building { get; set; }
    }
}
