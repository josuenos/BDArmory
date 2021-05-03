using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.Control;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Modules
{
    // A class to manage the safety of kerbals in BDA.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalSafetyManager : MonoBehaviour
    {
        #region Definitions
        static public KerbalSafetyManager Instance; // static instance for dealing with global stuff.

        public Dictionary<ProtoCrewMember, KerbalSafety> kerbals; // The kerbals being managed.
        List<KerbalEVA> evaKerbalsToMonitor;
        bool isEnabled = false;
        #endregion

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            kerbals = new Dictionary<ProtoCrewMember, KerbalSafety>();
        }

        public void Start()
        {
            Debug.Log("[BDArmory.KerbalSafety]: Safety manager started" + (BDArmorySettings.KERBAL_SAFETY > 0 ? " and enabled." : ", but currently disabled."));
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
            evaKerbalsToMonitor = new List<KerbalEVA>();
            if (BDArmorySettings.KERBAL_SAFETY > 0)
                CheckAllVesselsForKerbals();
        }

        public void OnDestroy()
        {
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
        }

        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            {
                DisableKerbalSafety();
                foreach (var ks in kerbals.Values)
                    ks.recovered = true;
            }
        }

        public void EnableKerbalSafety()
        {
            if (isEnabled) return;
            isEnabled = true;
            Debug.Log("[BDArmory.KerbalSafety]: Enabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.AddHandlers();
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                switch (BDArmorySettings.RUNWAY_PROJECT_ROUND)
                {
                    case 32: // Nuclear engines
                    case 33: // Rapid deployment II
                        GameEvents.onVesselSOIChanged.Add(EatenByTheKraken);
                        break;
                }
            }
            CheckAllVesselsForKerbals(); // Check for new vessels that were added while we weren't active.
        }

        public void DisableKerbalSafety()
        {
            if (!isEnabled) return;
            isEnabled = false;
            Debug.Log("[BDArmory.KerbalSafety]: Disabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.RemoveHandlers();
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                switch (BDArmorySettings.RUNWAY_PROJECT_ROUND)
                {
                    case 32: // Nuclear engines round
                    case 33: // Rapid deployment II
                        GameEvents.onVesselSOIChanged.Remove(EatenByTheKraken);
                        break;
                }
            }
        }

        public void CheckAllVesselsForKerbals()
        {
            newKerbalsAwaitingCheck.Clear();
            evaKerbalsToMonitor.Clear();
            foreach (var vessel in FlightGlobals.Vessels)
                CheckVesselForKerbals(vessel);
        }

        public void CheckVesselForKerbals(Vessel vessel, bool quiet = false)
        {
            if (BDArmorySettings.KERBAL_SAFETY == 0) return;
            if (vessel == null) return;
            foreach (var part in vessel.parts)
            {
                if (part == null) continue;
                foreach (var crew in part.protoModuleCrew)
                {
                    if (crew == null) continue;
                    if (kerbals.ContainsKey(crew)) continue; // Already managed.
                    var ks = part.gameObject.AddComponent<KerbalSafety>();
                    StartCoroutine(ks.Configure(crew, part, quiet && false)); // FIXME remove false when working here
                }
            }
        }

        HashSet<KerbalEVA> newKerbalsAwaitingCheck = new HashSet<KerbalEVA>();
        public void ManageNewlyEjectedKerbal(KerbalEVA kerbal, Vector3 velocity)
        {
            if (newKerbalsAwaitingCheck.Contains(kerbal)) return;
            newKerbalsAwaitingCheck.Add(kerbal);
            StartCoroutine(ManageNewlyEjectedKerbalCoroutine(kerbal));
            StartCoroutine(ManuallyMoveKerbalEVACoroutine(kerbal, velocity, 2f));
        }

        IEnumerator ManageNewlyEjectedKerbalCoroutine(KerbalEVA kerbal)
        {
            var kerbalName = kerbal.vessel.vesselName;
            while (kerbal != null && !kerbal.Ready)
            {
                yield return new WaitForFixedUpdate();
            }
            if (kerbal != null && kerbal.vessel != null)
            {
                CheckVesselForKerbals(kerbal.vessel, true);
                newKerbalsAwaitingCheck.Remove(kerbal);
            }
            else
            {
                Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " disappeared before we could start managing them.");
            }
        }

        /// <summary>
        /// The flight integrator doesn't seem to update the EVA kerbal's position or velocity for about 0.95s of real-time for some unknown reason (this seems fairly constant regardless of time-control or FPS).
        /// </summary>
        /// <param name="kerbal">The kerbal on EVA.</param>
        /// <param name="realTime">The amount of real-time to manually update for.</param>
        IEnumerator ManuallyMoveKerbalEVACoroutine(KerbalEVA kerbal, Vector3 velocity, float realTime = 1f)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.KerbalSafety]: Manually setting position of " + kerbal.vessel.vesselName + " for " + realTime + "s of real-time.");
            if (!evaKerbalsToMonitor.Contains(kerbal)) evaKerbalsToMonitor.Add(kerbal);
            var gee = (Vector3)FlightGlobals.getGeeForceAtPosition(kerbal.transform.position);
            var verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
            float verticalSpeedAdjustment = 0f;
            var position = kerbal.vessel.GetWorldPos3D();
            if (kerbal.vessel.radarAltitude + verticalSpeed * Time.fixedDeltaTime < 2f) // Crashed into terrain, explode upwards.
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS) verticalSpeedAdjustment = 3f * (float)gee.magnitude - verticalSpeed;
                velocity = Vector3.ProjectOnPlane(velocity, -gee.normalized) - 3f * (gee + UnityEngine.Random.onUnitSphere * 0.3f * gee.magnitude);
                position += (2f - (float)kerbal.vessel.radarAltitude) * -gee.normalized;
                kerbal.vessel.SetPosition(position); // Put the kerbal back at just above gound level.
                kerbal.vessel.Landed = false;
            }
            else
            {
                velocity += 1.5f * -(gee + UnityEngine.Random.onUnitSphere * 0.3f * gee.magnitude);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) verticalSpeedAdjustment = 1.5f * (float)gee.magnitude;
            }
            verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
            kerbal.vessel.SetRotation(UnityEngine.Random.rotation);
            kerbal.vessel.rootPart.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1, 2));
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.KerbalSafety]: Setting " + kerbal.vessel.vesselName + "'s position to " + position.ToString("0.00") + " (" + kerbal.vessel.GetWorldPos3D().ToString("0.00") + ", altitude: " + kerbal.vessel.radarAltitude.ToString("0.00") + ") and velocity to " + velocity.magnitude.ToString("0.00") + " (" + verticalSpeed.ToString("0.00") + "m/s vertically, adjusted by " + verticalSpeedAdjustment.ToString("0.00") + "m/s)");
            var startTime = Time.realtimeSinceStartup;
            kerbal.vessel.rootPart.SetDetectCollisions(false);
            while (kerbal != null && kerbal.isActiveAndEnabled && kerbal.vessel != null && kerbal.vessel.isActiveAndEnabled && Time.realtimeSinceStartup - startTime < realTime)
            {
                // Note: 0.968f gives a reduction in speed to ~20% over 1s.
                if (verticalSpeed < 0f && kerbal.vessel.radarAltitude + verticalSpeed * (realTime - (Time.realtimeSinceStartup - startTime)) < 100f)
                {
                    velocity = velocity * 0.968f + gee * verticalSpeed / 10f * Time.fixedDeltaTime;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) verticalSpeedAdjustment = Vector3.Dot(-gee.normalized, gee * verticalSpeed / 10f * Time.fixedDeltaTime);
                }
                else
                {
                    velocity = velocity * 0.968f + gee * Time.fixedDeltaTime;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) verticalSpeedAdjustment = Vector3.Dot(-gee.normalized, gee * Time.fixedDeltaTime);
                }
                verticalSpeed = Vector3.Dot(-gee.normalized, velocity);
                position += velocity * Time.fixedDeltaTime - FloatingOrigin.Offset;
                kerbal.vessel.IgnoreGForces(1);
                kerbal.vessel.IgnoreSpeed(1);
                kerbal.vessel.SetPosition(position);
                kerbal.vessel.SetWorldVelocity(velocity + Krakensbane.GetLastCorrection());
                yield return new WaitForFixedUpdate();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.KerbalSafety]: Setting " + kerbal.vessel.vesselName + "'s position to " + position.ToString("0.00") + " (" + kerbal.vessel.GetWorldPos3D().ToString("0.00") + ", altitude: " + kerbal.vessel.radarAltitude.ToString("0.00") + ") and velocity to " + velocity.magnitude.ToString("0.00") + " (" + kerbal.vessel.Velocity().magnitude.ToString("0.00") + ", " + verticalSpeed.ToString("0.00") + "m/s vertically, adjusted by " + verticalSpeedAdjustment.ToString("0.00") + "m/s)." + " (offset: " + !FloatingOrigin.Offset.IsZero() + ", frameVel: " + !Krakensbane.GetFrameVelocity().IsZero() + ")" + " " + Krakensbane.GetFrameVelocityV3f().ToString("0.0") + ", corr: " + Krakensbane.GetLastCorrection().ToString("0.0"));
            }
            if (kerbal != null && kerbal.vessel != null)
            {
                kerbal.vessel.rootPart.SetDetectCollisions(true);
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS) for (int count = 0; kerbal != null && kerbal.isActiveAndEnabled && kerbal.vessel != null && kerbal.vessel.isActiveAndEnabled && count < 10; ++count)
                {
                    yield return new WaitForFixedUpdate();
                    Debug.Log("[BDArmory.KerbalSafety]: Tracking " + kerbal.vessel.vesselName + "'s position to " + kerbal.vessel.GetWorldPos3D().ToString("0.00") + " (altitude: " + kerbal.vessel.radarAltitude.ToString("0.00") + ") and velocity to " + kerbal.vessel.Velocity().magnitude.ToString("0.00") + " (" + kerbal.vessel.verticalSpeed.ToString("0.00") + "m/s vertically." + " (offset: " + !FloatingOrigin.Offset.IsZero() + ", frameVel: " + !Krakensbane.GetFrameVelocity().IsZero() + ")" + " " + Krakensbane.GetFrameVelocityV3f().ToString("0.0") + ", corr: " + Krakensbane.GetLastCorrection().ToString("0.0"));
                }
        }

        /// <summary>
        /// Register all the crew members as recovered, then recover the vessel.
        /// </summary>
        /// <param name="vessel">The vessel to recover.</param>
        public void RecoverVesselNow(Vessel vessel)
        {
            foreach (var part in vessel.parts.ToList())
            {
                foreach (var crew in part.protoModuleCrew.ToList())
                {
                    if (kerbals.ContainsKey(crew))
                    {
                        kerbals[crew].recovered = true;
                        Debug.Log("[BDArmory.KerbalSafety]: Recovering " + kerbals[crew].kerbalName + ".");
                        kerbals[crew].RemoveHandlers();
                    }
                }
            }
            ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        }

        void EatenByTheKraken(GameEvents.HostedFromToAction<Vessel, CelestialBody> fromTo)
        {
            if (!BDACompetitionMode.Instance.competitionIsActive) return;
            if (evaKerbalsToMonitor.Where(k => k != null).Select(k => k.vessel).Contains(fromTo.host))
            {
                var message = fromTo.host.vesselName + " got eaten by the Kraken!";
                Debug.Log("[BDArmory.KerbalSafety]: " + message);
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                fromTo.host.gameObject.SetActive(false);
                evaKerbalsToMonitor.Remove(evaKerbalsToMonitor.Find(k => k.vessel == fromTo.host));
                fromTo.host.Die();
                LoadedVesselSwitcher.Instance.TriggerSwitchVessel(0);
            }
            else
            {
                if (fromTo.host != null)
                {
                    Debug.Log("[BDArmory.KerbalSafety]: " + fromTo.host + " got eaten by the Kraken!");
                    fromTo.host.gameObject.SetActive(false);
                    fromTo.host.Die();
                }
            }
        }
    }

    public class KerbalSafety : MonoBehaviour
    {
        #region Definitions
        public string kerbalName; // The name of the kerbal/crew member.
        public KerbalEVA kerbalEVA; // For kerbals that have ejected or are sitting in command seats.
        public ProtoCrewMember crew; // For kerbals that are in cockpits.
        public Part part; // The part the proto crew member is in.
        public KerbalSeat seat; // The seat the kerbalEVA is in (if they're in one).
        public ModuleEvaChute chute; // The chute of the crew member.
        // public BDModulePilotAI ai; // The pilot AI.
        public bool recovering = false; // Whether they're scheduled for recovery or not.
        public bool recovered = false; // Whether they've been recovered or not.
        public bool deployingChute = false; // Whether they're scheduled for deploying their chute or not.
        public bool ejected = false; // Whether the kerbal has ejected or not.
        public bool leavingSeat = false; // Whether the kerbal is about to leave their seat.
        private string message;
        #endregion

        #region Field definitions
        // [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EjectOnImpendingDoom", // Eject if doomed
        //     groupName = "pilotAI_Ejection", groupDisplayName = "#LOC_BDArmory_PilotAI_Ejection", groupStartCollapsed = true),
        //     UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.02f, scene = UI_Scene.All)]
        // public float ejectOnImpendingDoom = 0.2f; // Time to impact at which to eject.
        #endregion

        /// <summary>
        /// Begin managing a crew member in a part.
        /// </summary>
        /// <param name="crew">The proto crew member.</param>
        /// <param name="part">The part.</param>
        public IEnumerator Configure(ProtoCrewMember crew, Part part, bool quiet = false)
        {
            if (crew == null)
            {
                Debug.LogError("[BDArmory.KerbalSafety]: Cannot manage null crew.");
                Destroy(this);
                yield break;
            }
            if (part == null)
            {
                Debug.LogError("[BDArmory.KerbalSafety]: Crew cannot exist outside of a part.");
                Destroy(this);
                yield break;
            }
            while (!part.vessel.loaded) yield return new WaitForFixedUpdate(); // Wait for the vessel to be loaded. (Avoids kerbals not being registered in seats.)
            kerbalName = crew.displayName;
            this.crew = crew;
            switch (BDArmorySettings.KERBAL_SAFETY_INVENTORY)
            {
                case 1:
                    this.crew.ResetInventory(true); // Reset the inventory to the default of a chute and a jetpack.
                    break;
                case 2:
                    this.crew.ResetInventory(false); // Reset the inventory to just a chute.
                    break;
            }
            this.part = part;
            if (part.IsKerbalEVA())
            {
                this.kerbalEVA = part.GetComponent<KerbalEVA>();
                if (kerbalEVA.IsSeated())
                {
                    var seats = part.vessel.FindPartModulesImplementing<KerbalSeat>();
                    bool found = false;
                    foreach (var s in seats)
                    {
                        if (s.Occupant == part)
                        {
                            seat = s;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Debug.LogError("[BDArmory.KerbalSafety]: Failed to find the kerbal seat that " + kerbalName + " occupies.");
                        yield break;
                    }
                }
                else // Free-falling EVA kerbal.
                {
                    ejected = true;
                    StartCoroutine(DelayedChuteDeployment());
                    StartCoroutine(RecoverWhenPossible());
                }
                ConfigureKerbalEVA(kerbalEVA);
            }
            AddHandlers();
            KerbalSafetyManager.Instance.kerbals.Add(crew, this);
            if (!quiet)
                Debug.Log("[BDArmory.KerbalSafety]: Managing the safety of " + kerbalName + (ejected ? " on EVA" : " in " + part.vessel.vesselName) + ".");
            OnVesselModified(part.vessel); // Immediately check the vessel.
        }

        private void ConfigureKerbalEVA(KerbalEVA kerbalEVA)
        {
            chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
            if (chute != null)
                chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
            {
                DisableConstructionMode(kerbalEVA);
                if (BDArmorySettings.KERBAL_SAFETY_INVENTORY == 2)
                    RemoveJetpack(kerbalEVA);
            }
        }
        private void DisableConstructionMode(KerbalEVA kerbalEVA)
        {
            if (kerbalEVA.InConstructionMode)
                kerbalEVA.InConstructionMode = false;
        }

        private void RemoveJetpack(KerbalEVA kerbalEVA)
        {
            var inventory = kerbalEVA.ModuleInventoryPartReference;
            if (inventory.ContainsPart("evaJetpack"))
            {
                inventory.RemoveNPartsFromInventory("evaJetpack", 1, false);
            }
            kerbalEVA.part.UpdateMass();
        }

        public void OnDestroy()
        {
            StopAllCoroutines();
            if (BDArmorySettings.KERBAL_SAFETY > 0 && !recovered)
            {
                Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " is MIA. Ejected: " + ejected + ", deployed chute: " + deployingChute);
            }
            if (crew != null)
            {
                if (KerbalSafetyManager.Instance.kerbals.ContainsKey(crew))
                {
                    KerbalSafetyManager.Instance.kerbals.Remove(crew); // Stop managing this kerbal.
                }
            }
            RemoveHandlers();
        }

        /// <summary>
        /// Add various event handlers. 
        /// </summary>
        public void AddHandlers()
        {
            if (kerbalEVA)
            {
                if (seat && seat.part)
                    seat.part.OnJustAboutToDie += Eject;
            }
            else
            {
                if (part)
                    part.OnJustAboutToDie += Eject;
            }
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
            GameEvents.onVesselCreate.Add(OnVesselModified);
        }

        /// <summary>
        /// Remove the event handlers. 
        /// </summary>
        public void RemoveHandlers()
        {
            if (part) part.OnJustAboutToDie -= Eject;
            if (seat && seat.part) seat.part.OnJustAboutToDie -= Eject;
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            GameEvents.onVesselCreate.Remove(OnVesselModified);
        }

        // FIXME to be part of an update loop (maybe)
        // void EjectOnImpendingDoom()
        // {
        //     if (!ejected && ejectOnImpendingDoom * (float)vessel.srfSpeed > ai.terrainAlertDistance)
        //     {
        //         KerbalSafety.Instance.Eject(vessel, this); // Abandon ship!
        //         ai.avoidingTerrain = false;
        //     }
        // }

        #region Ejection
        /// <summary>
        /// Eject from a vessel. 
        /// </summary>
        public void Eject()
        {
            if (ejected) return; // We've already ejected.
            if (part == null || part.vessel == null) return; // The vessel is gone, don't try to do anything.
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.KerbalSafety]: Ejection triggered for " + kerbalName + " in " + part);
            if (kerbalEVA != null)
            {
                if (kerbalEVA.isActiveAndEnabled) // Otherwise, they've been killed already and are being cleaned up by KSP.
                {
                    if (seat != null && kerbalEVA.IsSeated()) // Leave the seat.
                    {
                        Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " is leaving their seat on " + seat.part.vessel.vesselName + ".");
                        seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                    }
                    else
                    {
                        Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " has already left their seat.");
                    }
                    StartCoroutine(DelayedChuteDeployment());
                    StartCoroutine(RecoverWhenPossible());
                }
            }
            else if (crew != null && part.protoModuleCrew.Contains(crew) && !FlightEVA.hatchInsideFairing(part)) // Eject from a cockpit.
            {
                if (BDArmorySettings.KERBAL_SAFETY < 2) return;
                if (!ProcessEjection(part)) // All exits were blocked by something.
                {
                    if (!EjectFromOtherPart()) // Look for other airlocks to spawn from.
                    {
                        message = kerbalName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. R.I.P.";
                        BDACompetitionMode.Instance.competitionStatus.Add(message);
                        Debug.Log("[BDArmory.KerbalSafety]: " + message);
                    }
                }
            }
            else
            {
                Debug.LogError("[BDArmory.KerbalSafety]: Ejection called without a kerbal present.");
            }
            ejected = true;
        }

        private bool EjectFromOtherPart()
        {
            Part fromPart = part;
            foreach (var toPart in part.vessel.parts)
            {
                if (toPart == part) continue;
                if (toPart.CrewCapacity > 0 && !FlightEVA.hatchInsideFairing(toPart) && !FlightEVA.HatchIsObstructed(toPart, toPart.airlock))
                {
                    var crewTransfer = CrewTransfer.Create(fromPart, crew, OnDialogDismiss);
                    if (crewTransfer.validParts.Contains(toPart))
                    {
                        Debug.Log("[BDArmory.KerbalSafety]: Transferring " + kerbalName + " from " + fromPart + " to " + toPart + " then ejecting.");
                        crewTransfer.MoveCrewTo(toPart);
                        if (ProcessEjection(toPart))
                            return true;
                        fromPart = toPart;
                    }
                }
            }
            return false;
        }

        private void OnDialogDismiss(PartItemTransfer.DismissAction arg1, Part arg2)
        {
            Debug.Log(arg1);
            Debug.Log(arg2);
        }

        private bool ProcessEjection(Part fromPart)
        {
            kerbalEVA = FlightEVA.fetch.spawnEVA(crew, fromPart, fromPart.airlock, true);
            if (kerbalEVA != null && kerbalEVA.vessel != null)
            {
                CameraManager.Instance.SetCameraFlight();
                if (crew != null && crew.KerbalRef != null)
                {
                    crew.KerbalRef.state = Kerbal.States.BAILED_OUT;
                    fromPart.vessel.RemoveCrew(crew);
                }
                Debug.Log("[BDArmory.KerbalSafety]: " + crew.displayName + " ejected from " + fromPart.vessel.vesselName + " at " + fromPart.vessel.radarAltitude.ToString("0.00") + "m with velocity " + fromPart.vessel.srf_velocity.magnitude.ToString("0.00") + "m/s (vertical: " + fromPart.vessel.verticalSpeed + ")");
                kerbalEVA.autoGrabLadderOnStart = false; // Don't grab the vessel.
                kerbalEVA.StartNonCollidePeriod(5f, 1f, fromPart, fromPart.airlock);
                KerbalSafetyManager.Instance.ManageNewlyEjectedKerbal(kerbalEVA, fromPart.vessel.GetSrfVelocity());
                recovered = true;
                OnDestroy();
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Check various conditions when this vessel gets modified.
        /// </summary>
        /// <param name="vessel">The vessel that was modified.</param>
        public void OnVesselModified(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;
            if (part.vessel == vessel) // It's this vessel.
            {
                if (kerbalEVA != null)
                {
                    if (kerbalEVA.isActiveAndEnabled)
                    {
                        if (vessel.parts.Count == 1) // It's a falling kerbal.
                        {
                            if (!ejected)
                            {
                                ejected = true;
                                StartCoroutine(DelayedChuteDeployment());
                                StartCoroutine(RecoverWhenPossible());
                            }
                        }
                        else // It's a kerbal in a seat.
                        {
                            // Check if the kerbal needs to leave his seat.
                            ejected = false;
                            if (vessel.parts.Count == 2) // Just a kerbal in a seat.
                            {
                                StartCoroutine(DelayedLeaveSeat());
                            }
                            else { } // FIXME What else?
                        }
                    }
                    else
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " was not active (probably dead and being cleaned up by KSP already).");
                        OnDestroy();
                    }
                }
                else // It's a crew.
                {
                    // FIXME Check if the crew needs to eject.
                    ejected = false; // Reset ejected flag as failure to eject may have changed due to the vessel modification.
                }
            }
        }

        /// <summary>
        /// Parachute deployment.
        /// </summary>
        /// <param name="delay">Delay before deploying the chute</param>
        IEnumerator DelayedChuteDeployment(float delay = 1f)
        {
            if (deployingChute)
            {
                yield break;
            }
            deployingChute = true; // Indicate that we're deploying our chute.
            ejected = true; // Also indicate that we've ejected.
            yield return new WaitForSeconds(delay);
            if (kerbalEVA == null) yield break;
            kerbalEVA.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
            if (chute != null && !kerbalEVA.IsSeated() && !kerbalEVA.vessel.LandedOrSplashed) // Check that the kerbal hasn't regained their seat or already landed.
            {
                Debug.Log("[BDArmory.KerbalSafety]: " + kerbalName + " is falling, deploying halo parachute at " + kerbalEVA.vessel.radarAltitude + "m.");
                if (chute.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    chute.deploymentState = ModuleParachute.deploymentStates.STOWED; // Reset the deployment state.
                chute.deployAltitude = 30f;
                chute.Deploy();
            }
            if (kerbalEVA.vessel.LandedOrSplashed)
                Debug.Log("[BDArmory.KerbalSafety]: " + kerbalEVA.vessel.vesselName + " has already landed, not deploying chute.");
            if (FlightGlobals.ActiveVessel == kerbalEVA.vessel)
                LoadedVesselSwitcher.Instance.TriggerSwitchVessel(1f);
        }

        /// <summary>
        /// Leave seat after a short delay.
        /// </summary>
        /// <param name="delay">Delay before leaving seat.</param>
        IEnumerator DelayedLeaveSeat(float delay = 3f)
        {
            if (leavingSeat)
            {
                yield break;
            }
            leavingSeat = true;
            yield return new WaitForSeconds(delay);
            if (seat != null)
            {
                Debug.Log("[BDArmory.KerbalSafety]: Found " + kerbalName + " in a combat chair just falling, ejecting.");
                seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                ejected = true;
                StartCoroutine(DelayedChuteDeployment());
                StartCoroutine(RecoverWhenPossible());
            }
        }

        /// <summary>
        /// Recover the kerbal when possible (has landed and isn't the active vessel).
        /// </summary>
        /// <param name="asap">Don't wait until the kerbal has landed.</param>
        public IEnumerator RecoverWhenPossible(bool asap = false)
        {
            if (asap)
            {
                if (KerbalSafetyManager.Instance.kerbals.ContainsKey(crew))
                    KerbalSafetyManager.Instance.kerbals.Remove(crew); // Stop managing this kerbal.
            }
            if (recovering)
            {
                yield break;
            }
            recovering = true;
            if (!asap)
            {
                yield return new WaitUntil(() => kerbalEVA == null || kerbalEVA.vessel.LandedOrSplashed);
                yield return new WaitForSeconds(5); // Give it around 5s after landing, then recover the kerbal
            }
            yield return new WaitUntil(() => kerbalEVA == null || FlightGlobals.ActiveVessel != kerbalEVA.vessel);
            if (kerbalEVA == null)
            {
                Debug.LogError("[BDArmory.KerbalSafety]: " + kerbalName + " on EVA is MIA.");
                yield break;
            }
            Debug.Log("[BDArmory.KerbalSafety]: Recovering " + kerbalName + ".");
            recovered = true;
            ShipConstruction.RecoverVesselFromFlight(kerbalEVA.vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        }
        #endregion
    }
}