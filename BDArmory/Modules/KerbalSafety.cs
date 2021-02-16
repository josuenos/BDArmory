using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Core.Extension;
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
            Debug.Log("[KerbalSafety]: Safety manager started" + (BDArmorySettings.KERBAL_SAFETY ? " and enabled." : ", but currently disabled."));
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
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
            Debug.Log("[KerbalSafety]: Enabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.AddHandlers();
            CheckAllVesselsForKerbals(); // Check for new vessels that were added while we weren't active.
        }

        public void DisableKerbalSafety()
        {
            Debug.Log("[KerbalSafety]: Disabling kerbal safety.");
            foreach (var ks in kerbals.Values)
                ks.RemoveHandlers();
        }

        public void CheckAllVesselsForKerbals()
        {
            foreach (var vessel in FlightGlobals.Vessels)
                CheckVesselForKerbals(vessel);
        }

        public void CheckVesselForKerbals(Vessel vessel)
        {
            if (!BDArmorySettings.KERBAL_SAFETY) return;
            if (vessel == null) return;
            foreach (var part in vessel.parts)
            {
                if (part == null) continue;
                foreach (var crew in part.protoModuleCrew)
                {
                    if (crew == null) continue;
                    if (kerbals.ContainsKey(crew)) continue; // Already managed.
                    var ks = part.gameObject.AddComponent<KerbalSafety>();
                    StartCoroutine(ks.Configure(crew, part));
                }
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
                        Debug.Log("[KerbalSafety]: Recovering " + kerbals[crew].kerbalName + ".");
                        kerbals[crew].RemoveHandlers();
                    }
                }
            }
            ShipConstruction.RecoverVesselFromFlight(vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
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
        public IEnumerator Configure(ProtoCrewMember crew, Part part)
        {
            if (crew == null)
            {
                Debug.LogError("[KerbalSafety]: Cannot manage null crew.");
                Destroy(this);
                yield break;
            }
            if (part == null)
            {
                Debug.LogError("[KerbalSafety]: Crew cannot exist outside of a part.");
                Destroy(this);
                yield break;
            }
            while (!part.vessel.loaded) yield return new WaitForFixedUpdate(); // Wait for the vessel to be loaded. (Avoids kerbals not being registered in seats.)
            kerbalName = crew.displayName;
            this.crew = crew;
            this.crew.ResetInventory(); // Reset the inventory to a chute and a jetpack.
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
                        Debug.LogError("[KerbalSafety]: Failed to find the kerbal seat that " + kerbalName + " occupies.");
                        yield break;
                    }
                }
                else // Free-falling EVA kerbal.
                {
                    ejected = true;
                }
                chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
                if (chute != null)
                    chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
            }
            AddHandlers();
            KerbalSafetyManager.Instance.kerbals.Add(crew, this);
            Debug.Log("[KerbalSafety]: Managing the safety of " + kerbalName + " in " + part.vessel.vesselName + ".");
        }

        public void OnDestroy()
        {
            StopAllCoroutines();
            if (BDArmorySettings.KERBAL_SAFETY && !recovered)
            {
                Debug.Log("[KerbalSafety]: " + kerbalName + " is MIA.");
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
                    seat.part.OnJustAboutToBeDestroyed += Eject;
            }
            else
            {
                if (part)
                    part.OnJustAboutToBeDestroyed += Eject;
            }
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
            GameEvents.onVesselCreate.Add(OnVesselModified);
        }

        /// <summary>
        /// Remove the event handlers. 
        /// </summary>
        public void RemoveHandlers()
        {
            if (part) part.OnJustAboutToBeDestroyed -= Eject;
            if (seat && seat.part) seat.part.OnJustAboutToBeDestroyed -= Eject;
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
            if (kerbalEVA != null)
            {
                if (kerbalEVA.isActiveAndEnabled) // Otherwise, they've been killed already and are being cleaned up by KSP.
                {
                    if (seat != null && kerbalEVA.IsSeated()) // Leave the seat.
                    {
                        Debug.Log("[KerbalSafety]: " + kerbalName + " is leaving their seat on " + seat.part.vessel.vesselName + ".");
                        seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate));
                    }
                    else
                    {
                        Debug.Log("[KerbalSafety]: " + kerbalName + " has already left their seat.");
                    }
                    StartCoroutine(PostEjection());
                    StartCoroutine(DelayedChuteDeployment());
                    StartCoroutine(RecoverWhenPossible());
                }
            }
            else if (crew != null) // Eject from a cockpit.
            {
                Debug.Log("[KerbalSafety]: DEBUG skipping ejection for " + kerbalName + " in cockpit " + part + " for now."); return;
                // kerbalEVA = FlightEVA.fetch.spawnEVA(kerbalPCM, part, part.airlock, true);
                // if (kerbalEVA)
                // {
                //     Debug.Log("[KerbalSafety]: " + kerbalPCM.displayName + " ejected from " + part.vessel.vesselName + " at " + part.vessel.radarAltitude.ToString("0.0") + "m.");
                //     kerbalEVA.autoGrabLadderOnStart = false; // Don't grab the vessel.
                //     chute = kerbalEVA.vessel.FindPartModuleImplementing<ModuleEvaChute>();
                //     if (chute != null)
                //         chute.deploymentState = ModuleEvaChute.deploymentStates.STOWED; // Make sure the chute is stowed.
                //     // DeactivatePilot();
                //     StartCoroutine(PostEjection());
                // }
                // else
                // {
                //     // All exits were blocked by something. FIXME Try adjusting the fromAirlock Transform in spawnEVA.
                //     // BDACompetitionMode.Instance.competitionStatus.Add(kerbalPCM.displayName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. RIP.");
                //     Debug.Log("[KerbalSafety]: " + kerbalPCM.displayName + " failed to eject from " + part.vessel.vesselName + ", all exits were blocked. RIP.");
                //     part.vessel.RemoveCrew(kerbalPCM); // Save their ghost.
                //     // StartCoroutine(ExplodeStuffThenEject());
                //     // ExplodeStuffThenEject();
                // }
            }
            else
            {
                Debug.LogError("[KerbalSafety]: Ejection called without a kerbal present.");
            }
            ejected = true;
        }

        /// <summary>
        /// Adjustments to kerbal velocities to avoid hitting the ground before the parachute can deploy.
        /// </summary>
        public IEnumerator PostEjection()
        {
            yield return new WaitForFixedUpdate(); // Wait 2 fixed updates so that vessel position and velocity get valid values.
            yield return new WaitForFixedUpdate();
            if (kerbalEVA == null || kerbalEVA.vessel == null) { Debug.Log("[KerbalSafety]: " + kerbalName + " is MIA after leaving their vessel. RIP."); yield break; }
            if (kerbalEVA.OnALadder) // Force the kerbal to let go.
            {
                var fsm = kerbalEVA.fsm;
                var letGoEvent = fsm.CurrentState.StateEvents.SingleOrDefault(e => e.name == "Ladder Let Go");
                if (letGoEvent == null) Debug.LogError("[KerbalSafety]: Did not find let go event");
                else
                {
                    Debug.Log("DEBUG running letGoEvent");
                    fsm.RunEvent(letGoEvent);
                }
            }
            if (kerbalEVA.vessel.LandedOrSplashed) { Debug.Log("[KerbalSafety]: " + kerbalName + " has already landed, not compensating for low altitude."); yield break; }
            var currentAltitude = Misc.Misc.GetRadarAltitudeAtPos(kerbalEVA.vessel.transform.position, false);
            var expectedAltitudeIn5s = Misc.Misc.GetRadarAltitudeAtPos(kerbalEVA.vessel.PredictPosition(5f), false);
            var gravity = FlightGlobals.getGeeForceAtPosition(kerbalEVA.vessel.transform.position);
            var upDirection = -gravity.normalized;
            var gee = (float)gravity.magnitude;
            if (currentAltitude < 2) kerbalEVA.vessel.transform.position += (2f - currentAltitude) * upDirection;
            kerbalEVA.vessel.srf_velocity += Mathf.Sqrt(Mathf.Max(0f, 200f - expectedAltitudeIn5s) * gee) * upDirection;
            Debug.Log("[KerbalSafety]: Adding " + (Mathf.Sqrt(Mathf.Max(0f, 200f - expectedAltitudeIn5s) * gee)).ToString("0.0") + "m/s to " + kerbalName + "'s vertical velocity giving " + Vector3.Dot(kerbalEVA.vessel.srf_velocity, upDirection).ToString("0.0") + "m/s at altitude " + kerbalEVA.vessel.radarAltitude.ToString("0.0"));
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
                            ejected = true;
                            StartCoroutine(DelayedChuteDeployment());
                            StartCoroutine(RecoverWhenPossible());
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
                        Debug.Log("[KerbalSafety]: " + kerbalName + " was not active (probably dead and being cleaned up by KSP already).");
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
                Debug.Log("[KerbalSafety]: " + kerbalName + " is falling, deploying halo parachute.");
                if (chute.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED)
                    chute.deploymentState = ModuleParachute.deploymentStates.STOWED; // Reset the deployment state.
                chute.deployAltitude = 30f;
                chute.Deploy();
                // var crew = kerbal.GetComponent<ProtoCrewMember>();
                // if (crew != null && KerbalSafetyManager.Instance.kerbals.ContainsKey(crew.displayName))
                //     StartCoroutine(KerbalSafetyManager.Instance.kerbals[crew.displayName].PostEjection());
            }
            if (kerbalEVA.vessel.LandedOrSplashed)
                Debug.Log("[KerbalSafety]: " + kerbalEVA.vessel.vesselName + " has already landed, not deploying chute.");
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
                Debug.Log("[KerbalSafety]: Found a kerbal in a combat chair just falling, ejecting.");
                seat.LeaveSeat(new KSPActionParam(KSPActionGroup.Abort, KSPActionType.Activate)); // This will trigger a new OnVesselModified for the now falling kerbal.
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
                Debug.LogError("[KerbalSafety]: " + kerbalName + " on EVA is MIA.");
                yield break;
            }
            Debug.Log("[KerbalSafety]: Recovering " + kerbalName + ".");
            recovered = true;
            ShipConstruction.RecoverVesselFromFlight(kerbalEVA.vessel.protoVessel, HighLogic.CurrentGame.flightState, true);
        }
        #endregion
    }
}