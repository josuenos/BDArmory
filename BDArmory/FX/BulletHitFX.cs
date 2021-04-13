using System;
using System.Collections.Generic;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Modules;
using UniLinq;
using UnityEngine;

namespace BDArmory.FX
{
    class Decal : MonoBehaviour
    {
        Part parentPart;
        public static ObjectPool CreateDecalPool(string modelPath)
        {
            var template = GameDatabase.Instance.GetModel(modelPath);
            var decal = template.AddComponent<Decal>();
            template.SetActive(false);
            return ObjectPool.CreateObjectPool(template, BDArmorySettings.MAX_NUM_BULLET_DECALS, false, true, 0, true);
        }

        public void AttachAt(Part hitPart, RaycastHit hit, Vector3 offset)
        {
            parentPart = hitPart;
            transform.SetParent(hitPart.transform);
            transform.position = hit.point + offset;
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
            parentPart.OnJustAboutToDie += OnParentDestroy;
            parentPart.OnJustAboutToBeDestroyed += OnParentDestroy;
            gameObject.SetActive(true);
        }

        public void OnParentDestroy()
        {
            if (parentPart)
            {
                parentPart.OnJustAboutToDie -= OnParentDestroy;
                parentPart.OnJustAboutToBeDestroyed -= OnParentDestroy;
                parentPart = null;
                transform.parent = null;
                gameObject.SetActive(false);
            }
        }

        public void OnDestroy()
        {
            OnParentDestroy(); // Make sure it's disabled and book-keeping is done.
        }
    }

    public class BulletHitFX : MonoBehaviour
    {
        KSPParticleEmitter[] pEmitters;
        AudioSource audioSource;
        AudioClip hitSound;
        float startTime;
        public bool ricochet;
        public float caliber;

        public static ObjectPool decalPool_small;
        public static ObjectPool decalPool_large;
        public static ObjectPool decalPool_paint1;
        public static ObjectPool decalPool_paint2;
        public static ObjectPool decalPool_paint3;
        public static ObjectPool bulletHitFXPool;
        public static ObjectPool penetrationFXPool;
        public static ObjectPool leakFXPool;
        public static ObjectPool FireFXPool;
        public static Dictionary<Vessel, List<float>> PartsOnFire = new Dictionary<Vessel, List<float>>();

        public static int MaxFiresPerVessel = 3;
        public static float FireLifeTimeInSeconds = 5f;

        private bool disabled = false;

        public static void SetupShellPool()
        {
            if (!BDArmorySettings.PAINTBALL_MODE)
            {
                if (decalPool_large == null)
                    decalPool_large = Decal.CreateDecalPool("BDArmory/Models/bulletDecal/BulletDecal2");

                if (decalPool_small == null)
                    decalPool_small = Decal.CreateDecalPool("BDArmory/Models/bulletDecal/BulletDecal1");
            }
            else
            {
                if (decalPool_paint1 == null)
                    decalPool_paint1 = Decal.CreateDecalPool("BDArmory/Models/bulletDecal/BulletDecal3");

                if (decalPool_paint2 == null)
                    decalPool_paint2 = Decal.CreateDecalPool("BDArmory/Models/bulletDecal/BulletDecal4");

                if (decalPool_paint3 == null)
                    decalPool_paint3 = Decal.CreateDecalPool("BDArmory/Models/bulletDecal/BulletDecal5");
            }
        }

        public static void AdjustDecalPoolSizes(int size)
        {
            if (decalPool_large != null) decalPool_large.AdjustSize(size);
            if (decalPool_small != null) decalPool_small.AdjustSize(size);
            if (decalPool_paint1 != null) decalPool_paint1.AdjustSize(size);
            if (decalPool_paint2 != null) decalPool_paint2.AdjustSize(size);
            if (decalPool_paint3 != null) decalPool_paint3.AdjustSize(size);
        }

        // We use an ObjectPool for the BulletHitFX and PenFX instances as they leak KSPParticleEmitters otherwise.
        public static void SetupBulletHitFXPool()
        {
            if (bulletHitFXPool == null)
            {
                var bulletHitFXTemplate = GameDatabase.Instance.GetModel("BDArmory/Models/bulletHit/bulletHit");
                var bFX = bulletHitFXTemplate.AddComponent<BulletHitFX>();
                bFX.audioSource = bulletHitFXTemplate.AddComponent<AudioSource>();
                bFX.audioSource.minDistance = 1;
                bFX.audioSource.maxDistance = 50;
                bFX.audioSource.spatialBlend = 1;
                bulletHitFXTemplate.SetActive(false);
                bulletHitFXPool = ObjectPool.CreateObjectPool(bulletHitFXTemplate, 10, true, true, 10f * Time.deltaTime, false);
            }
            if (penetrationFXPool == null)
            {
                var penetrationFXTemplate = GameDatabase.Instance.GetModel("BDArmory/FX/PenFX");
                var bFX = penetrationFXTemplate.AddComponent<BulletHitFX>();
                bFX.audioSource = penetrationFXTemplate.AddComponent<AudioSource>();
                bFX.audioSource.minDistance = 1;
                bFX.audioSource.maxDistance = 50;
                bFX.audioSource.spatialBlend = 1;
                penetrationFXTemplate.SetActive(false);
                penetrationFXPool = ObjectPool.CreateObjectPool(penetrationFXTemplate, 10, true, true, 10f * Time.deltaTime, false);
            }
        }

        public static void SpawnDecal(RaycastHit hit, Part hitPart, float caliber, float penetrationfactor)
        {
            if (!BDArmorySettings.BULLET_DECALS) return;
            ObjectPool decalPool_;
            if (!BDArmorySettings.PAINTBALL_MODE)
            {
                if (caliber >= 90f)
                {
                    decalPool_ = decalPool_large;
                }
                else
                {
                    decalPool_ = decalPool_small;
                }
            }
            else
            {
                int i;
                i = UnityEngine.Random.Range(1, 4);
                if (i < 1.66)
                {
                    decalPool_ = decalPool_paint1;
                }
                else if (i > 2.33)
                {
                    decalPool_ = decalPool_paint2;
                }
                else
                {
                    decalPool_ = decalPool_paint3;
                }
            }

            //front hit
            var decalFront = decalPool_.GetPooledObject();
            if (decalFront != null && hitPart != null)
            {
                var decal = decalFront.GetComponentInChildren<Decal>();
                decal.AttachAt(hitPart, hit, new Vector3(0.25f, 0f, 0f));
            }
            //back hole if fully penetrated
            if (penetrationfactor >= 1)
            {
                var decalBack = decalPool_.GetPooledObject();
                if (decalBack != null && hitPart != null)
                {
                    var decal = decalBack.GetComponentInChildren<Decal>();
                    decal.AttachAt(hitPart, hit, new Vector3(-0.25f, 0f, 0f));
                }
            }
        }

        private static bool CanFlamesBeAttached(Part hitPart)
        {
            if (!BDArmorySettings.FIRE_FX_IN_FLIGHT && !hitPart.vessel.LandedOrSplashed || !hitPart.HasFuel())
                return false;

            if (hitPart.vessel.LandedOrSplashed)
            {
                MaxFiresPerVessel = BDArmorySettings.MAX_FIRES_PER_VESSEL;
                FireLifeTimeInSeconds = BDArmorySettings.FIRELIFETIME_IN_SECONDS;
            }

            if (PartsOnFire.ContainsKey(hitPart.vessel) && PartsOnFire[hitPart.vessel].Count >= MaxFiresPerVessel)
            {
                var firesOnVessel = PartsOnFire[hitPart.vessel];

                firesOnVessel.Where(x => (Time.time - x) > FireLifeTimeInSeconds).Select(x => firesOnVessel.Remove(x));
                return false;
            }

            if (!PartsOnFire.ContainsKey(hitPart.vessel))
            {
                List<float> firesList = new List<float> { Time.time };

                PartsOnFire.Add(hitPart.vessel, firesList);
            }
            else
            {
                PartsOnFire[hitPart.vessel].Add(Time.time);
            }

            return true;
        }

        void OnEnable()
        {
            startTime = Time.time;
            disabled = false;

            foreach (var pe in pEmitters)
            {
                if (pe == null) continue;
                EffectBehaviour.AddParticleEmitter(pe);
            }

            audioSource = gameObject.GetComponent<AudioSource>();
            audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;

            int random = UnityEngine.Random.Range(1, 3);

            if (ricochet)
            {
                if (caliber <= 30)
                {
                    string path = "BDArmory/Sounds/ricochet" + random;
                    hitSound = GameDatabase.Instance.GetAudioClip(path);
                }
                else
                {
                    string path = "BDArmory/Sounds/Artillery_Shot";
                    hitSound = GameDatabase.Instance.GetAudioClip(path);
                }
            }
            else
            {
                if (caliber <= 30)
                {
                    string path = "BDArmory/Sounds/bulletHit" + random;
                    hitSound = GameDatabase.Instance.GetAudioClip(path);
                }
                else
                {
                    string path = "BDArmory/Sounds/Artillery_Shot";
                    hitSound = GameDatabase.Instance.GetAudioClip(path);
                }
            }

            audioSource.PlayOneShot(hitSound);
        }

        void OnDisable()
        {
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
        }

        void Update()
        {
            if (!disabled && Time.time - startTime > Time.deltaTime)
            {
                using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                    while (pe.MoveNext())
                    {
                        if (pe.Current == null) continue;
                        pe.Current.emit = false;
                    }
                disabled = true;
            }
        }

        public static void CreateBulletHit(Part hitPart, Vector3 position, RaycastHit hit, Vector3 normalDirection, bool ricochet, float caliber, float penetrationfactor)
        {
            if (decalPool_large == null || decalPool_small == null)
                SetupShellPool();
            if (BDArmorySettings.PAINTBALL_MODE && decalPool_paint1 == null)
                SetupShellPool();
            if (bulletHitFXPool == null || penetrationFXPool == null)
                SetupBulletHitFXPool();

            if ((hitPart != null) && caliber != 0 && !hitPart.IgnoreDecal())
            {
                SpawnDecal(hit, hitPart, caliber, penetrationfactor); //No bullet decals for laser or ricochet                
            }

            GameObject newExplosion = (caliber <= 30 || BDArmorySettings.PAINTBALL_MODE) ? bulletHitFXPool.GetPooledObject() : penetrationFXPool.GetPooledObject();
            newExplosion.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normalDirection));
            var bulletHitComponent = newExplosion.GetComponent<BulletHitFX>();
            bulletHitComponent.ricochet = ricochet;
            bulletHitComponent.caliber = caliber;
            bulletHitComponent.pEmitters = newExplosion.GetComponentsInChildren<KSPParticleEmitter>();
            newExplosion.SetActive(true);
            foreach (var pe in bulletHitComponent.pEmitters)
            {
                if (pe == null) continue;
                pe.emit = true;

                if (pe.gameObject.name == "sparks")
                {
                    pe.force = (4.49f * FlightGlobals.getGeeForceAtPosition(position));
                }
                else if (pe.gameObject.name == "smoke")
                {
                    pe.force = (1.49f * FlightGlobals.getGeeForceAtPosition(position));
                }
            }
        }

        // FIXME Use an object pool for flames?          

        public static void AttachLeak(RaycastHit hit, Part hitPart, float caliber, bool explosive, string sourcevessel)
        {
            if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_TANKS)
            {
                if (leakFXPool == null)
                    leakFXPool = FuelLeakFX.CreateLeakFXPool("BDArmory/FX/FuelLeakFX/model");
                var fuelLeak = leakFXPool.GetPooledObject();
                var leakFX = fuelLeak.GetComponentInChildren<FuelLeakFX>();
                leakFX.AttachAt(hitPart, hit, new Vector3(0.25f, 0f, 0f));
                leakFX.transform.localScale = Vector3.one * (caliber / 10);

                var leak = hitPart.FindModuleImplementing<ModuleDrainFuel>();
                if (leak != null && !hitPart.isEngine()) //only apply one leak to engines
                {
                    if (BDArmorySettings.BD_FIRES_ENABLED)
                    {
                        int explosiveMod = 10; //10% chance of AP rounds starting fires from sparks/tracers/etc
                        if (explosive)
                        {
                            explosiveMod = 33; //33% chance of starting fires from HE rounds
                            explosive = false;
                        }
                        //if (incindiary)
                        //{
                        //    explosiveMod = 90; //90% chance of starting fires from inc rounds
                        //    explosive = false;
                        //}
                        double Diceroll = UnityEngine.Random.Range(0, 100);
                        if (Diceroll <= explosiveMod)
                        {
                            explosive = true;
                        }
                        //Debug.Log("[FIRE DEBUG] diceroll: " + Diceroll);
                        if (explosive)
                        {
                            int leakcount = 1;
                            foreach (var existingLeakFX in hitPart.GetComponentsInChildren<FuelLeakFX>())
                            {
                                existingLeakFX.lifeTime = 0; //kill leakFX, start fire
                                leakcount++;
                            }
                            leak.drainDuration = 0;
                            AttachFire(hit, hitPart, caliber, sourcevessel, -1, leakcount);
                        }
                    }
                    else
                    {
                        leak.drainDuration += (20 * BDArmorySettings.BD_TANK_LEAK_TIME);
                    }
                    leak.drainRate += ((caliber / 100) * BDArmorySettings.BD_TANK_LEAK_RATE);
                }
                else
                {
                    leak = (ModuleDrainFuel)hitPart.AddModule("ModuleDrainFuel");
                    leak.drainRate = ((caliber / 10) * BDArmorySettings.BD_TANK_LEAK_RATE);
                    leak.drainDuration = (BDArmorySettings.BD_TANK_LEAK_TIME);
                    leakFX.lifeTime = (BDArmorySettings.BD_TANK_LEAK_TIME);

                    if (hitPart.isEngine())
                    {
                        leak.drainDuration = (100 * BDArmorySettings.BD_TANK_LEAK_TIME);
                        leakFX.lifeTime = (100 * BDArmorySettings.BD_TANK_LEAK_TIME);
                    }
                }
                Debug.Log("[BDArmory.BulletHitFX]: BulletHit attaching fuel leak, drainrate: " + leak.drainRate);

                fuelLeak.SetActive(true);
            }
        }
        public static void AttachFire(RaycastHit hit, Part hitPart, float caliber, string sourcevessel, float burntime = -1, int ignitedLeaks = 1)
        {
            if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_FIRES_ENABLED)
            {
                if (FireFXPool == null)
                    FireFXPool = FireFX.CreateFireFXPool("BDArmory/FX/FireFX/model");
                var fire = FireFXPool.GetPooledObject();
                var fireFX = fire.GetComponentInChildren<FireFX>();
                fireFX.AttachAt(hitPart, hit, new Vector3(0.25f, 0f, 0f), sourcevessel);
                fireFX.burnRate = (((caliber / 50) * BDArmorySettings.BD_TANK_LEAK_RATE) * ignitedLeaks);
                //fireFX.transform.localScale = Vector3.one * (caliber/10);

                Debug.Log("[BDArmory.BulletHitFX]: BulletHit fire, burn rate: " + fireFX.burnRate);

                fire.SetActive(true);
            }
        }
        public static void AttachFlames(Vector3 contactPoint, Part hitPart)
        {
            if (!CanFlamesBeAttached(hitPart)) return;

            var modelUrl = "BDArmory/FX/FlameEffect2/model";

            var flameObject = (GameObject)Instantiate(GameDatabase.Instance.GetModel(modelUrl), contactPoint, Quaternion.identity);

            flameObject.SetActive(true);
            flameObject.transform.SetParent(hitPart.transform);
            flameObject.AddComponent<DecalEmitterScript>();

            DecalEmitterScript.shrinkRateFlame = 0.125f;
            DecalEmitterScript.shrinkRateSmoke = 0.125f;

            foreach (var pe in flameObject.GetComponentsInChildren<KSPParticleEmitter>())
            {
                if (!pe.useWorldSpace) continue;
                var gpe = pe.gameObject.AddComponent<DecalGaplessParticleEmitter>();
                gpe.Emit = true;
            }
        }
    }
}
