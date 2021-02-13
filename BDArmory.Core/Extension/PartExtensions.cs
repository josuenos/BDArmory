﻿using System;
using System.Collections.Generic;
using BDArmory.Core.Services;
using BDArmory.Core.Utils;
using UniLinq;
using UnityEngine;

namespace BDArmory.Core.Extension
{
    public enum ExplosionSourceType { Other, Missile, Bullet };
    public static class PartExtensions
    {
        public static void AddDamage(this Part p, float damage)
        {
            if (BDArmorySettings.PAINTBALL_MODE) return; // Don't add damage when paintball mode is enabled

            //////////////////////////////////////////////////////////
            // Basic Add Hitpoints for compatibility (only used by lasers & fires)
            //////////////////////////////////////////////////////////

            if (p.GetComponent<KerbalEVA>() != null)
            {
                ApplyHitPoints(p.GetComponent<KerbalEVA>(), damage);
            }
            else
            {
                Dependencies.Get<DamageService>().AddDamageToPart_svc(p, damage);
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory]: Standard Hitpoints Applied : " + damage);
            }
        }

        public static float AddExplosiveDamage(this Part p,
                                               float explosiveDamage,
                                               float caliber,
                                               ExplosionSourceType sourceType)
        {
            if (BDArmorySettings.PAINTBALL_MODE) return 0f; // Don't add damage when paintball mode is enabled

            float damage_ = 0f;
            //////////////////////////////////////////////////////////
            // Explosive Hitpoints
            //////////////////////////////////////////////////////////

            switch (sourceType)
            {
                case ExplosionSourceType.Missile:
                    damage_ = (BDArmorySettings.DMG_MULTIPLIER / 100) * BDArmorySettings.EXP_DMG_MOD_MISSILE * explosiveDamage;
                    break;
                default:
                    damage_ = (BDArmorySettings.DMG_MULTIPLIER / 100) * BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW * explosiveDamage;
                    break;
            }

            var damage_before = damage_;
            //////////////////////////////////////////////////////////
            //   Armor Reduction factors
            //////////////////////////////////////////////////////////

            if (p.HasArmor())
            {
                float armorMass_ = p.GetArmorThickness();
                float damageReduction = DamageReduction(armorMass_, damage_, sourceType, caliber);

                damage_ = damageReduction;
            }

            //////////////////////////////////////////////////////////
            //   Apply Hitpoints
            //////////////////////////////////////////////////////////

            if (p.GetComponent<KerbalEVA>() != null)
            {
                ApplyHitPoints(p.GetComponent<KerbalEVA>(), (float)damage_);
            }
            else
            {
                ApplyHitPoints(p, damage_);
            }
            return damage_;
        }

        public static float AddBallisticDamage(this Part p,
                                               float mass,
                                               float caliber,
                                               float multiplier,
                                               float penetrationfactor,
                                               float bulletDmgMult,
                                               float impactVelocity)
        {
            if (BDArmorySettings.PAINTBALL_MODE) return 0f; // Don't add damage when paintball mode is enabled

            //////////////////////////////////////////////////////////
            // Basic Kinetic Formula
            //////////////////////////////////////////////////////////
            //Hitpoints mult for scaling in settings
            //1e-4 constant for adjusting MegaJoules for gameplay

            float damage_ = ((0.5f * (mass * Mathf.Pow(impactVelocity, 2)))
                            * (BDArmorySettings.DMG_MULTIPLIER / 100) * bulletDmgMult
                            * 1e-4f * BDArmorySettings.BALLISTIC_DMG_FACTOR);

            var damage_before = damage_;
            //////////////////////////////////////////////////////////
            //   Armor Reduction factors
            //////////////////////////////////////////////////////////

            if (p.HasArmor())
            {
                float armorMass_ = p.GetArmorThickness();
                float damageReduction = DamageReduction(armorMass_, damage_, ExplosionSourceType.Bullet, caliber, penetrationfactor);

                damage_ = damageReduction;
            }
            //////////////////////////////////////////////////////////
            //   Apply Hitpoints
            //////////////////////////////////////////////////////////

            if (p.GetComponent<KerbalEVA>() != null)
            {
                ApplyHitPoints(p.GetComponent<KerbalEVA>(), (float)damage_);
            }
            else
            {
                ApplyHitPoints(p, damage_, caliber, mass, mass, impactVelocity, penetrationfactor);
            }
            return damage_;
        }

        /// <summary>
        /// Ballistic Hitpoint Damage
        /// </summary>
        public static void ApplyHitPoints(Part p, float damage_, float caliber, float mass, float multiplier, float impactVelocity, float penetrationfactor)
        {
            //////////////////////////////////////////////////////////
            // Apply HitPoints Ballistic
            //////////////////////////////////////////////////////////
            Dependencies.Get<DamageService>().AddDamageToPart_svc(p, damage_);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: mass: " + mass + " caliber: " + caliber + " multiplier: " + multiplier + " velocity: " + impactVelocity + " penetrationfactor: " + penetrationfactor);
                Debug.Log("[BDArmory]: Ballistic Hitpoints Applied : " + damage_);
            }
        }

        /// <summary>
        /// Explosive Hitpoint Damage
        /// </summary>
        public static void ApplyHitPoints(Part p, float damage)
        {
            //////////////////////////////////////////////////////////
            // Apply Hitpoints / Explosive
            //////////////////////////////////////////////////////////

            Dependencies.Get<DamageService>().AddDamageToPart_svc(p, damage);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: Explosive Hitpoints Applied to " + p.name + ": " + damage);
        }

        /// <summary>
        /// Kerbal Hitpoint Damage
        /// </summary>
        public static void ApplyHitPoints(KerbalEVA kerbal, float damage)
        {
            //////////////////////////////////////////////////////////
            // Apply Hitpoints / Kerbal
            //////////////////////////////////////////////////////////

            Dependencies.Get<DamageService>().AddDamageToKerbal_svc(kerbal, damage);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: Hitpoints Applied to " + kerbal.name + ": " + damage);
        }

        public static void AddForceToPart(Rigidbody rb, Vector3 force, Vector3 position, ForceMode mode)
        {
            //////////////////////////////////////////////////////////
            // Add The force to part
            //////////////////////////////////////////////////////////

            rb.AddForceAtPosition(force, position, mode);
            Debug.Log("[BDArmory]: Force Applied : " + force.magnitude);
        }

        public static void Destroy(this Part p)
        {
            Dependencies.Get<DamageService>().SetDamageToPart_svc(p, -1);
        }

        public static bool HasArmor(this Part p)
        {
            return p.GetArmorThickness() > 15f;
        }

        public static bool GetFireFX(this Part p)
        {
            return Dependencies.Get<DamageService>().HasFireFX_svc(p);
        }

        public static float GetFireFXTimeOut(this Part p)
        {
            return Dependencies.Get<DamageService>().GetFireFXTimeOut(p);
        }

        public static float Damage(this Part p)
        {
            return Dependencies.Get<DamageService>().GetPartDamage_svc(p);
        }

        public static float MaxDamage(this Part p)
        {
            return Dependencies.Get<DamageService>().GetMaxPartDamage_svc(p);
        }

        public static void ReduceArmor(this Part p, double massToReduce)
        {
            if (!p.HasArmor()) return;
            massToReduce = Math.Max(0.10, Math.Round(massToReduce, 2));
            Dependencies.Get<DamageService>().ReduceArmor_svc(p, (float)massToReduce);

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: Armor Removed : " + massToReduce);
            }
        }

        public static float GetArmorThickness(this Part p)
        {
            if (p == null) return 0f;
            return Dependencies.Get<DamageService>().GetPartArmor_svc(p);
        }

        public static float GetArmorPercentage(this Part p)
        {
            if (p == null) return 0;
            float armor_ = Dependencies.Get<DamageService>().GetPartArmor_svc(p);
            float maxArmor_ = Dependencies.Get<DamageService>().GetMaxArmor_svc(p);

            return armor_ / maxArmor_;
        }

        public static float GetDamagePercentage(this Part p)
        {
            if (p == null) return 0;

            float damage_ = p.Damage();
            float maxDamage_ = p.MaxDamage();

            return damage_ / maxDamage_;
        }

        public static void RefreshAssociatedWindows(this Part part)
        {
            //Thanks FlowerChild
            //refreshes part action window

            //IEnumerator<UIPartActionWindow> window = UnityEngine.Object.FindObjectsOfType(typeof(UIPartActionWindow)).Cast<UIPartActionWindow>().GetEnumerator();
            //while (window.MoveNext())
            //{
            //    if (window.Current == null) continue;
            //    if (window.Current.part == part)
            //    {
            //        window.Current.displayDirty = true;
            //    }
            //}
            //window.Dispose();

            MonoUtilities.RefreshContextWindows(part);
        }

        public static bool IsMissile(this Part part)
        {
            return part.Modules.Contains("MissileBase") || part.Modules.Contains("MissileLauncher") ||
                   part.Modules.Contains("BDModularGuidance");
        }

        public static float GetArea(this Part part, bool isprefab = false, Part prefab = null)
        {
            var size = part.GetSize();
            float sfcAreaCalc = 2f * (size.x * size.y) + 2f * (size.y * size.z) + 2f * (size.x * size.z);

            return sfcAreaCalc;
        }

        public static float GetAverageBoundSize(this Part part)
        {
            var size = part.GetSize();

            return (size.x + size.y + size.z) / 3f;
        }

        public static float GetVolume(this Part part)
        {
            var size = part.GetSize();
            var volume = size.x * size.y * size.z;
            return volume;
        }

        public static Vector3 GetSize(this Part part)
        {
            var size = part.GetComponentInChildren<MeshFilter>().mesh.bounds.size;

            // if (part.name.Contains("B9.Aero.Wing.Procedural")) // Covered by SuicidalInsanity's patch.
            // {
            //     size = size * 0.1f;
            // }

            float scaleMultiplier = 1f;
            if (part.Modules.Contains("TweakScale"))
            {
                var tweakScaleModule = part.Modules["TweakScale"];
                scaleMultiplier = tweakScaleModule.Fields["currentScale"].GetValue<float>(tweakScaleModule) /
                                  tweakScaleModule.Fields["defaultScale"].GetValue<float>(tweakScaleModule);
            }

            return size * scaleMultiplier;
        }

        public static bool IsAero(this Part part)
        {
            return part.Modules.Contains("ModuleControlSurface") ||
                   part.Modules.Contains("ModuleLiftingSurface");
        }

        public static string GetExplodeMode(this Part part)
        {
            return Dependencies.Get<DamageService>().GetExplodeMode_svc(part);
        }

        public static bool IgnoreDecal(this Part part)
        {
            if (
                part.Modules.Contains("FSplanePropellerSpinner") ||
                part.Modules.Contains("ModuleWheelBase") ||
                part.Modules.Contains("KSPWheelBase") ||
                part.gameObject.GetComponentUpwards<KerbalEVA>() ||
                part.Modules.Contains("ModuleDCKShields") ||
                part.Modules.Contains("ModuleShieldGenerator")
                )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool HasFuel(this Part part)
        {
            bool hasFuel = false;
            using (IEnumerator<PartResource> resources = part.Resources.GetEnumerator())
                while (resources.MoveNext())
                {
                    if (resources.Current == null) continue;
                    switch (resources.Current.resourceName)
                    {
                        case "LiquidFuel":
                            if (resources.Current.amount > 1d) hasFuel = true;
                            break;
                        case "Oxidizer":
                            if (resources.Current.amount > 1d) hasFuel = true;
                            break;
                        case "Monopropellant":
                            if (resources.Current.amount > 1d) hasFuel = true;
                            break;
                    }
                }
            return hasFuel;
        }

        public static float DamageReduction(float armor, float damage, ExplosionSourceType sourceType, float caliber = 0, float penetrationfactor = 0)
        {
            float _damageReduction;

            switch (sourceType)
            {
                case ExplosionSourceType.Missile:
                    if (BDAMath.Between(armor, 100f, 200f))
                    {
                        damage *= 0.95f;
                    }
                    else if (BDAMath.Between(armor, 200f, 400f))
                    {
                        damage *= 0.875f;
                    }
                    else if (BDAMath.Between(armor, 400f, 500f))
                    {
                        damage *= 0.80f;
                    }
                    break;
                default:
                    if (!(penetrationfactor >= 1f))
                    {
                        //if (BDAMath.Between(armor, 100f, 200f))
                        //{
                        //    damage *= 0.300f;
                        //}
                        //else if (BDAMath.Between(armor, 200f, 400f))
                        //{
                        //    damage *= 0.250f;
                        //}
                        //else if (BDAMath.Between(armor, 400f, 500f))
                        //{
                        //    damage *= 0.200f;
                        //}

                        //y=(98.34817*x)/(97.85935+x)

                        _damageReduction = (113 * armor) / (154 + armor);

                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: Damage Before Reduction : " + damage / 100);
                            Debug.Log("[BDArmory]: Damage Reduction : " + _damageReduction / 100);
                            Debug.Log("[BDArmory]: Damage After Armor : " + (damage *= (_damageReduction / 100f)));
                        }

                        damage *= (_damageReduction / 100f);
                    }
                    break;
            }

            return damage;
        }
        public static bool isBattery(this Part part)
        {
            bool hasEC = false;
            using (IEnumerator<PartResource> resources = part.Resources.GetEnumerator())
                while (resources.MoveNext())
                {
                    if (resources.Current == null) continue;
                    switch (resources.Current.resourceName)
                    {
                        case "ElectricCharge":
                            if (resources.Current.amount > 1d) hasEC = true; //discount trace EC in alternators
                            break;
                    }
                }
            return hasEC;
        }
        public static Vector3 GetBoundsSize(Part part)
        {
            return PartGeometryUtil.MergeBounds(part.GetRendererBounds(), part.transform).size;
        }

        /// <summary>
        /// KSP version dependent query of whether the part is a kerbal on EVA.
        /// </summary>
        /// <param name="part">Part to check.</param>
        /// <returns>true if the part is a kerbal on EVA.</returns>
        public static bool IsKerbalEVA(this Part part)
        {
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
            {
                return part.IsKerbalEVA_1_11();
            }
            else
            {
                return part.IsKerbalEVA_1_10();
            }
        }

        private static bool IsKerbalEVA_1_11(this Part part) // KSP has issues on older versions if this call is in the parent function.
        {
            return part.isKerbalEVA();
        }

        private static bool IsKerbalEVA_1_10(this Part part)
        {
            return part.FindModuleImplementing<KerbalEVA>() != null;
        }
    }
}
