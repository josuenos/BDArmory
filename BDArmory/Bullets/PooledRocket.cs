using System;
using System.Collections.Generic;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.Bullets
{
    public class PooledRocket : MonoBehaviour
    {
        public RocketInfo rocket; //get tracers, expFX urls moved to BulletInfo
                                  //Seeker/homing rocket code (Image-Recognition tracking?)

        public Transform spawnTransform;
        public Vessel sourceVessel;
        public string sourceVesselName;

        public string rocketName;
        public float rocketMass;
        public float caliber;
        public float thrust;
        private Vector3 thrustVector;
        public float thrustTime;
        public bool shaped;
        public float maxAirDetonationRange;
        public bool flak;
        public bool concussion;
        public bool gravitic;
        public bool EMP;
        public bool choker;
        public float massMod = 0;
        public float impulse = 0;
        public float detonationRange;
        public float tntMass;
        bool explosive = true;
        public float bulletDmgMult = 1;
        public float blastRadius = 0;
        public float randomThrustDeviation = 0.05f;
        public float massScalar = 0.012f;
        public string explModelPath;
        public string explSoundPath;

        float startTime;
        float stayTime = 0.04f;
        float lifeTime = 10;

        Vector3 prevPosition;
        Vector3 currPosition;
        Vector3 startPosition;
        Ray RocketRay;
        private float impactVelocity;

        public bool hasPenetrated = false;
        public bool hasDetonated = false;
        public int penTicker = 0;

        private float distanceFromStart = 0;

        //bool isThrusting = true;

        Rigidbody rb;
        public Rigidbody parentRB;

        KSPParticleEmitter[] pEmitters;

        float randThrustSeed;

        public AudioSource audioSource;

        HashSet<Vessel> craftHit = new HashSet<Vessel>();

        void OnEnable()
        {
            BDArmorySetup.numberOfParticleEmitters++;

            rb = gameObject.AddOrGetComponent<Rigidbody>();

            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

            using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    if (FlightGlobals.getStaticPressure(transform.position) == 0 && pe.Current.useWorldSpace)
                    {
                        pe.Current.emit = false;
                    }
                    else if (pe.Current.useWorldSpace)
                    {
                        BDAGaplessParticleEmitter gpe = pe.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                        gpe.rb = rb;
                        gpe.emit = true;
                    }
                    else
                    {
                        pe.Current.emit = true;
                        EffectBehaviour.AddParticleEmitter(pe.Current);
                    }
                }

            prevPosition = transform.position;
            currPosition = transform.position;
            startPosition = transform.position;
            startTime = Time.time;

            massScalar = 0.012f / rocketMass;

            rb.mass = rocketMass;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            if (!FlightGlobals.RefFrameIsRotating) rb.useGravity = false;

            rb.useGravity = false;

            randThrustSeed = UnityEngine.Random.Range(0f, 100f);
            thrustVector = new Vector3(0, 0, thrust);

            SetupAudio();

            if (this.sourceVessel)
            {
                var aName = this.sourceVessel.GetName();
                if (BDACompetitionMode.Instance && BDACompetitionMode.Instance.Scores.ContainsKey(aName))
                    ++BDACompetitionMode.Instance.Scores[aName].shotsFired;
                sourceVesselName = sourceVessel.GetName(); // Set the source vessel name as the vessel might have changed its name or died by the time the bullet hits.
            }
            else
            {
                sourceVesselName = null;
            }
            if (tntMass <= 0)
            {
                explosive = false;
            }
        }

        void onDisable()
        {
            BDArmorySetup.OnVolumeChange -= UpdateVolume;
            BDArmorySetup.numberOfParticleEmitters--;
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
            sourceVesselName = null;
        }

        void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            //floating origin and velocity offloading corrections
            if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
            {
                transform.position -= FloatingOrigin.OffsetNonKrakensbane;
                prevPosition -= FloatingOrigin.OffsetNonKrakensbane;
                startPosition -= FloatingOrigin.OffsetNonKrakensbane;
            }
            distanceFromStart = Vector3.Distance(transform.position, startPosition);

            if (Time.time - startTime < stayTime && transform.parent != null)
            {
                transform.rotation = transform.parent.rotation;
                transform.position = spawnTransform.position;
                //+(transform.parent.rigidbody.velocity*Time.fixedDeltaTime);
            }
            else
            {
                if (transform.parent != null && parentRB)
                {
                    transform.parent = null;
                    rb.isKinematic = false;
                    rb.velocity = parentRB.velocity + Krakensbane.GetFrameVelocityV3f();
                }
            }

            if (rb && !rb.isKinematic)
            {
                //physics
                if (FlightGlobals.RefFrameIsRotating)
                {
                    rb.velocity += FlightGlobals.getGeeForceAtPosition(transform.position) * Time.fixedDeltaTime;
                }

                //guidance and attitude stabilisation scales to atmospheric density.
                float atmosMultiplier =
                    Mathf.Clamp01(2.5f *
                                  (float)
                                  FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
                                      FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody));

                //model transform. always points prograde
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(rb.velocity + Krakensbane.GetFrameVelocity(), transform.up),
                    atmosMultiplier * (0.5f * (Time.time - startTime)) * 50 * Time.fixedDeltaTime);


                if (Time.time - startTime < thrustTime && Time.time - startTime > stayTime)
                {
                    thrustVector.x = randomThrustDeviation * (1 - (Mathf.PerlinNoise(4 * Time.time, randThrustSeed) * 2)) / massScalar;//this needs to scale w/ rocket mass, or light projectiles will be 
                    thrustVector.y = randomThrustDeviation * (1 - (Mathf.PerlinNoise(randThrustSeed, 4 * Time.time) * 2)) / massScalar;//far more affected than heavier ones
                    rb.AddRelativeForce(thrustVector);
                }//0.012/rocketmass - use .012 as baseline, it's the mass of hte hydra, which the randomTurstdeviation was originally calibrated for
            }

            if (Time.time - startTime > thrustTime)
            {
                foreach (var pe in pEmitters)
                    if (pe != null)
                        pe.emit = false;
            }
            if (Time.time - startTime > 0.1f + stayTime)
            {
                hasPenetrated = true;
                hasDetonated = false;
                penTicker = 0;

                currPosition = transform.position;
                float dist = (currPosition - prevPosition).magnitude;
                RocketRay = new Ray(prevPosition, currPosition - prevPosition);
                var hits = Physics.RaycastAll(RocketRay, dist, 9076737);
                if (hits.Length > 0)
                {
                    var orderedHits = hits.OrderBy(x => x.distance);

                    using (var hitsEnu = orderedHits.GetEnumerator())
                    {
                        while (hitsEnu.MoveNext())
                        {
                            if (!hasPenetrated || hasDetonated) break;

                            RaycastHit hit = hitsEnu.Current;
                            Part hitPart = null;
                            KerbalEVA hitEVA = null;

                            try
                            {
                                hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                                hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                            }
                            catch (NullReferenceException e)
                            {
                                Debug.LogWarning("[BDArmory.BDArmory]:NullReferenceException for Kinetic Hit: " + e.Message);
                                return;
                            }

                            if (hitEVA != null)
                            {
                                hitPart = hitEVA.part;
                                // relative velocity, separate from the below statement, because the hitpart might be assigned only above
                                if (hitPart.rb != null)
                                    impactVelocity = (rb.velocity - (hitPart.rb.velocity + Krakensbane.GetFrameVelocityV3f())).magnitude;
                                else
                                    impactVelocity = rb.velocity.magnitude;
                                ProjectileUtils.ApplyDamage(hitPart, hit, 1, 1, caliber, rocketMass, impactVelocity, bulletDmgMult, distanceFromStart, explosive, false, sourceVessel, rocketName);
                                Detonate(hit.point, false);
                                return;
                            }

                            if (hitPart != null && hitPart.vessel == sourceVessel) continue;  //avoid autohit;

                            Vector3 impactVector = rb.velocity;
                            if (hitPart != null && hitPart.rb != null)
                                // using relative velocity vector instead of just rocket velocity
                                // since KSP vessels can easily be moving faster than rockets
                                impactVector = rb.velocity - (hitPart.rb.velocity + Krakensbane.GetFrameVelocityV3f());

                            float hitAngle = Vector3.Angle(impactVector, -hit.normal);

                            if (ProjectileUtils.CheckGroundHit(hitPart, hit, caliber))
                            {
                                ProjectileUtils.CheckBuildingHit(hit, rocketMass, rb.velocity, bulletDmgMult);
                                Detonate(hit.point, false);
                                return;
                            }

                            impactVelocity = impactVector.magnitude;
                            if (gravitic)
                            {
                                var ME = hitPart.FindModuleImplementing<ModuleMassAdjust>();
                                if (ME == null)
                                {
                                    ME = (ModuleMassAdjust)hitPart.AddModule("ModuleMassAdjust");
                                }
                                ME.massMod += massMod;
                                ME.duration += BDArmorySettings.WEAPON_FX_DURATION;
                            }
                            if (concussion)
                            {
                                hitPart.rb.AddForceAtPosition(impactVector.normalized * impulse, hit.point, ForceMode.Acceleration);
                                Detonate(hit.point, false);
                                hasDetonated = true;
                                return; //impulse rounds shouldn't penetrate/do damage
                            }
                            float anglemultiplier = (float)Math.Cos(Math.PI * hitAngle / 180.0);

                            float thickness = ProjectileUtils.CalculateThickness(hitPart, anglemultiplier);
                            float penetration = ProjectileUtils.CalculatePenetration(caliber, rocketMass, impactVelocity);
                            float penetrationFactor = ProjectileUtils.CalculateArmorPenetration(hitPart, anglemultiplier, hit, penetration, thickness, caliber);
                            if (penetration > thickness)
                            {
                                rb.velocity = rb.velocity * (float)Math.Sqrt(thickness / penetration);
                                if (penTicker > 0) rb.velocity *= 0.55f;
                            }

                            if (penetrationFactor > 1)
                            {
                                hasPenetrated = true;
                                ProjectileUtils.ApplyDamage(hitPart, hit, 1, penetrationFactor, caliber, rocketMass, impactVelocity, bulletDmgMult, distanceFromStart, explosive, false, sourceVessel, rocketName);
                                penTicker += 1;
                                ProjectileUtils.CheckPartForExplosion(hitPart);

                                if (explosive)
                                {
                                    transform.position += (rb.velocity * Time.fixedDeltaTime) / 3;

                                    Detonate(transform.position, false);
                                    hasDetonated = true;
                                }
                            }
                            else // stopped by armor
                            {
                                if (hitPart.rb != null && hitPart.rb.mass > 0)
                                {
                                    float forceAverageMagnitude = impactVelocity * impactVelocity *
                                                          (1f / hit.distance) * rocketMass;

                                    float accelerationMagnitude =
                                        forceAverageMagnitude / (hitPart.vessel.GetTotalMass() * 1000);

                                    hitPart.rb.AddForceAtPosition(impactVector.normalized * accelerationMagnitude, hit.point, ForceMode.Acceleration);

                                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                        Debug.Log("[BDArmory.PooledRocket]: Force Applied " + Math.Round(accelerationMagnitude, 2) + "| Vessel mass in kgs=" + hitPart.vessel.GetTotalMass() * 1000 + "| rocket effective mass =" + rocketMass);
                                }

                                hasPenetrated = false;
                                ProjectileUtils.ApplyDamage(hitPart, hit, 1, penetrationFactor, caliber, rocketMass, impactVelocity, bulletDmgMult, distanceFromStart, explosive, false, sourceVessel, rocketName);
                                Detonate(hit.point, false);
                                hasDetonated = true;
                            }

                            if (penTicker >= 2)
                            {
                                Detonate(hit.point, false);
                                return;
                            }

                            if (rb.velocity.magnitude <= 100 && hasPenetrated && (Time.time - startTime > thrustTime))
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                {
                                    Debug.Log("[BDArmory.PooledRocket]: Rocket ballistic velocity too low, stopping");
                                }
                                Detonate(hit.point, false);
                                return;
                            }
                            if (!hasPenetrated || hasDetonated) break;
                        }
                    }
                }
            }
            else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
            {
                Detonate(currPosition, false);
            }
            prevPosition = currPosition;

            if (Time.time - startTime > lifeTime) // life's 10s, quite a long time for faster rockets
            {
                Detonate(transform.position, true);
            }
            if (distanceFromStart >= maxAirDetonationRange)//rockets are performance intensive, lets cull those that have flown too far away
            {
                Detonate(transform.position, false);
            }
            if (ProximityAirDetonation(distanceFromStart))
            {
                Detonate(transform.position, false);
            }
        }

        private bool ProximityAirDetonation(float distanceFromStart)
        {
            bool detonate = false;

            if (distanceFromStart <= blastRadius) return false;

            if (flak)
            {
                using (var hitsEnu = Physics.OverlapSphere(transform.position, detonationRange, 557057).AsEnumerable().GetEnumerator())
                {
                    while (hitsEnu.MoveNext())
                    {
                        if (hitsEnu.Current == null) continue;
                        try
                        {
                            Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                            if (partHit != null && partHit.vessel != sourceVessel)
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory.PooledRocket]: rocket proximity sphere hit | Distance overlap = " + detonationRange + "| Part name = " + partHit.name);
                                return detonate = true;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[BDArmory.PooledRocket]: Exception thrown in ProximityAirDetonation: " + e.Message + "\n" + e.StackTrace);
                        }
                    }
                }
            }
            return detonate;
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDArmorySetup.GameIsPaused || (Time.time - startTime > thrustTime))
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
                else
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                }
            }
        }

        void Detonate(Vector3 pos, bool missed)
        {
            if (!missed)
            {
                if (tntMass > 0)
                {
                    Vector3 direction = default(Vector3);
                    if (shaped)
                    {
                        direction = (pos + rb.velocity * Time.deltaTime).normalized;
                    }
                    if (concussion || EMP || choker)
                    {
                        using (var hitsEnu = Physics.OverlapSphere(transform.position, 25, 557057).AsEnumerable().GetEnumerator())
                        {
                            craftHit.Clear();
                            while (hitsEnu.MoveNext())
                            {
                                if (hitsEnu.Current == null) continue;
                                if (hitsEnu.Current.gameObject == FlightGlobals.currentMainBody.gameObject) continue; // Ignore terrain hits.
                                Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                                if (craftHit.Contains(partHit.vessel)) continue; // Don't hit the same craft multiple times.
                                craftHit.Add(partHit.vessel);

                                float Distance = Vector3.Distance(partHit.transform.position, this.transform.position);
                                if (partHit != null)
                                {
                                    if (concussion && partHit.mass > 0)
                                    {
                                        partHit.rb.AddForceAtPosition((partHit.transform.position - this.transform.position).normalized * impulse, partHit.transform.position, ForceMode.Acceleration);
                                    }
                                    if (EMP)
                                    {
                                        var MDEC = partHit.vessel.rootPart.FindModuleImplementing<ModuleDrainEC>();
                                        if (MDEC == null)
                                        {
                                            MDEC = (ModuleDrainEC)partHit.vessel.rootPart.AddModule("ModuleDrainEC");
                                        }
                                        MDEC.incomingDamage += ((25 - Distance) * 5); //this way craft at edge of blast might only get disabled instead of bricked
                                        MDEC.softEMP = false; //can bypass EMP damage cap                                            
                                    }
                                    if (choker)
                                    {
                                        var ash = partHit.vessel.rootPart.FindModuleImplementing<ModuleDrainIntakes>();
                                        if (ash == null)
                                        {
                                            ash = (ModuleDrainIntakes)partHit.vessel.rootPart.AddModule("ModuleDrainIntakes");
                                        }
                                        ash.drainDuration += BDArmorySettings.WEAPON_FX_DURATION * (1 - (Distance / 25)); //reduce intake knockout time based on distance from epicenter                                        
                                    }
                                }
                            }
                        }
                        ExplosionFx.CreateExplosion(pos, tntMass, explModelPath, explSoundPath, ExplosionSourceType.Bullet, caliber, null, sourceVesselName, null, direction, true);
                    }
                    else
                    {
                        ExplosionFx.CreateExplosion(pos, tntMass, explModelPath, explSoundPath, ExplosionSourceType.Bullet, caliber, null, sourceVesselName, null, direction);
                    }
                    if (gravitic)
                    {
                        using (var hitsEnu = Physics.OverlapSphere(transform.position, blastRadius, 557057).AsEnumerable().GetEnumerator())
                        {
                            while (hitsEnu.MoveNext())
                            {
                                if (hitsEnu.Current == null) continue;

                                Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
                                if (partHit != null && partHit.mass > 0)
                                {
                                    float distance = Vector3.Distance(transform.position, partHit.transform.position);
                                    var ME = partHit.vessel.rootPart.FindModuleImplementing<ModuleMassAdjust>();
                                    if (ME == null)
                                    {
                                        ME = (ModuleMassAdjust)partHit.vessel.rootPart.AddModule("ModuleMassAdjust");
                                    }
                                    ME.massMod += (massMod * (1 - (distance / blastRadius))); //this way craft at edge of blast might only get disabled instead of bricked
                                    ME.duration += (BDArmorySettings.WEAPON_FX_DURATION * (1 - (distance / blastRadius))); //can bypass EMP damage cap
                                }
                            }
                        }
                    }
                }
            } // needs to be Explosiontype Bullet since missile only returns Module MissileLauncher
            gameObject.SetActive(false);
        }

        void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.minDistance = 1;
            audioSource.maxDistance = 2000;
            audioSource.dopplerLevel = 0.5f;
            audioSource.volume = 0.9f * BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            audioSource.pitch = 1f;
            audioSource.priority = 255;
            audioSource.spatialBlend = 1;
            audioSource.clip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/rocketLoop");

            UpdateVolume();
            BDArmorySetup.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }
    }
}
