using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BDArmory.Control;
using BDArmory.Extensions;
using BDArmory.GameModes;
using BDArmory.Settings;
using BDArmory.Utils;
using BDArmory.UI;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Status for spawning.
    /// External libraries should look for and use these.
    /// </summary>
    public static class VesselSpawnerStatus
    {
        public static bool vesselsSpawning  // Flag for when vessels are being spawned and other things should wait for them to finish being spawned.
        {
            get { return _vesselsSpawning; }
            set
            {
                _vesselsSpawning = value
                    || (CircularSpawning.Instance != null && CircularSpawning.Instance.vesselsSpawning)
                    || (SingleVesselSpawning.Instance != null && SingleVesselSpawning.Instance.vesselsSpawning);
            } // Add in other relevant conditions whenever new classes derived from VesselSpawnerBase are added.
        }
        static bool _vesselsSpawning = false;
        public static bool vesselSpawnSuccess // Flag for whether vessel spawning was successful or not across all derived VesselSpawner classes.
        {
            get { return _vesselSpawnSuccess; }
            set
            {
                _vesselSpawnSuccess = value
                    && (CircularSpawning.Instance == null || CircularSpawning.Instance.vesselSpawnSuccess)
                    && (SingleVesselSpawning.Instance == null || SingleVesselSpawning.Instance.vesselSpawnSuccess);
            } // Add in other relevant conditions whenever new classes derived from VesselSpawnerBase are added.
        }
        static bool _vesselSpawnSuccess = true;
        public static SpawnFailureReason spawnFailureReason = SpawnFailureReason.None;
        public static bool inhibitCameraTools => vesselsSpawning; // Flag for CameraTools (currently just checks for vessels being spawned).
    }

    /// Base class for VesselSpawner classes so that it can work with spawn strategies.
    public abstract class VesselSpawnerBase : MonoBehaviour
    {
        protected static string AutoSpawnPath;
        protected static readonly string AutoSpawnFolder = "AutoSpawn";
        public bool vesselsSpawning { get { return _vesselsSpawning; } set { _vesselsSpawning = value; VesselSpawnerStatus.vesselsSpawning = value; } }
        bool _vesselsSpawning = false;
        public bool vesselSpawnSuccess { get { return _vesselSpawnSuccess; } set { _vesselSpawnSuccess = value; VesselSpawnerStatus.vesselSpawnSuccess = value; } }
        bool _vesselSpawnSuccess = true;
        public SpawnFailureReason spawnFailureReason { get { return VesselSpawnerStatus.spawnFailureReason; } set { VesselSpawnerStatus.spawnFailureReason = value; } }
        protected static readonly WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        protected virtual void Awake()
        {
            AutoSpawnPath = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, AutoSpawnFolder));
        }

        protected void LogMessageFrom(string derivedClassName, string message, bool toScreen, bool toLog)
        {
            if (toScreen) BDACompetitionMode.Instance.competitionStatus.Add(message);
            if (toLog) Debug.Log($"[BDArmory.{derivedClassName}]: " + message);
        }
        void LogMessage(string message, bool toScreen = true, bool toLog = true) => LogMessageFrom("VesselSpawnerBase", message, toScreen, toLog);

        #region SpawnStrategy kludges
        public abstract IEnumerator Spawn(SpawnConfig spawnConfig); // FIXME This is essentially a kludge to get the VesselSpawner class to be functional with the way that the SpawnStrategy interface is defined.
        #endregion

        // ======================================================
        // Vessel Spawning Functions and Coroutines
        // Check for "spawnFailureReason != SpawnFailureReason.None" after calling any of these coroutines to determine success/failure.
        // The message will have already been displayed/logged, but you'll need to set "vesselsSpawning = false" and "yield break" on failure.

        #region Pre-spawn
        public virtual void PreSpawnInitialisation(SpawnConfig spawnConfig)
        {
            //Reset gravity
            if (BDArmorySettings.GRAVITY_HACKS)
            {
                PhysicsGlobals.GraviticForceMultiplier = 1d;
                VehiclePhysics.Gravity.Refresh();
            }

            // If we're on another planetary body, first switch to the proper one.
            if (spawnConfig.worldIndex != FlightGlobals.currentMainBody.flightGlobalsIndex)
            { SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, 20); }

            if (spawnConfig.killEverythingFirst)
            {
                BDACompetitionMode.Instance.LogResults("due to spawning", "auto-dump-from-spawning"); // Log results first.
                BDACompetitionMode.Instance.StopCompetition(); // Stop any running competition.
                BDACompetitionMode.Instance.ResetCompetitionStuff(); // Reset competition scores.
            }
        }
        #endregion

        #region Early-spawn
        // Common group-spawning variables. For individual craft, use local versions instead to avoid conflicts.
        protected double terrainAltitude { get; set; }
        protected Vector3d spawnPoint { get; set; }

        /// <summary>
        /// Acquire the spawn point, killing off other vessels or check for the default 100km PRE range.
        /// </summary>
        /// <param name="spawnConfig">The spawn configuration</param>
        /// <param name="viewDistance">The viewing distance if killing everything off and relocating the camera.</param>
        /// <param name="spawnAirborne">Whether the craft are to be air-spawned or not (also only if relocating the camera).</param>
        /// <returns></returns>
        protected IEnumerator AcquireSpawnPoint(SpawnConfig spawnConfig, float viewDistance, bool spawnAirborne)
        {
            if (spawnConfig.killEverythingFirst) // If we're killing everything, relocate the camera and floating origin to the spawn point and wait for the terrain. Note: this sets the variables in the "else" branch.
            {
                yield return SpawnUtils.RemoveAllVessels();
                yield return WaitForTerrain(spawnConfig, viewDistance, spawnAirborne);
            }
            else // Otherwise, just try spawning at the specified location.
            {
                // Get the spawning point in world position coordinates.
                terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
                if ((spawnPoint - FloatingOrigin.fetch.offset).magnitude > 100e3)
                { LogMessage("WARNING The spawn point is " + ((spawnPoint - FloatingOrigin.fetch.offset).magnitude / 1000).ToString("G4") + "km away. Expect vessels to be killed immediately.", true, false); }
            }
        }

        protected IEnumerator WaitForTerrain(SpawnConfig spawnConfig, float viewDistance, bool spawnAirborne)
        {
            // Update the floating origin offset, so that the vessels spawn within range of the physics.
            SpawnUtils.ShowSpawnPoint(spawnConfig.worldIndex, spawnConfig.latitude, spawnConfig.longitude, spawnConfig.altitude, viewDistance, true);
            // Re-acquire the spawning point after the floating origin shift.
            terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(spawnConfig.latitude, spawnConfig.longitude);
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(spawnConfig.latitude, spawnConfig.longitude, terrainAltitude + spawnConfig.altitude);
            FloatingOrigin.SetOffset(spawnPoint); // This adjusts local coordinates, such that spawnPoint is (0,0,0), which should hopefully help with collider detection.

            if (terrainAltitude > 0) // Not over the ocean or on a surfaceless body.
            {
                // Wait for the terrain to load in before continuing.
                Ray ray;
                RaycastHit hit;
                var radialUnitVector = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
                var testPosition = spawnPoint + 1000f * radialUnitVector;
                var terrainDistance = 1000f + (float)spawnConfig.altitude;
                var lastTerrainDistance = terrainDistance;
                var distanceToCoMainBody = (testPosition - FlightGlobals.currentMainBody.transform.position).magnitude;
                ray = new Ray(testPosition, -radialUnitVector);
                LogMessage("Waiting up to 10s for terrain to settle.", true, BDArmorySettings.DEBUG_SPAWNING);
                var startTime = Planetarium.GetUniversalTime();
                double lastStableTimeStart = startTime;
                double stableTime = 0;
                do
                {
                    lastTerrainDistance = terrainDistance;
                    yield return waitForFixedUpdate;
                    terrainDistance = Physics.Raycast(ray, out hit, (float)distanceToCoMainBody, (int)LayerMasks.Scenery) ? hit.distance : -1f; // Oceans shouldn't be more than 10km deep...
                    if (terrainDistance < 0f) // Raycast is failing to find terrain.
                    {
                        if (Planetarium.GetUniversalTime() - startTime < 1) continue; // Give the terrain renderer a chance to spawn the terrain.
                        else break;
                    }
                    if (Mathf.Abs(lastTerrainDistance - terrainDistance) > 0.1f)
                        lastStableTimeStart = Planetarium.GetUniversalTime(); // Reset the stable time tracker.
                    stableTime = Planetarium.GetUniversalTime() - lastStableTimeStart;
                } while (Planetarium.GetUniversalTime() - startTime < 10 && stableTime < 1f);
                if (terrainDistance < 0)
                {
                    if (!spawnAirborne)
                    {
                        LogMessage("Failed to find terrain at the spawning point! Try increasing the spawn altitude.");
                        spawnFailureReason = SpawnFailureReason.NoTerrain;
                        yield break;
                    }
                    else
                    {
                        if (BDArmorySettings.DEBUG_SPAWNING) LogMessage("Failed to find terrain at the spawning point!");
                    }
                }
                else
                {
                    spawnPoint = hit.point + (float)spawnConfig.altitude * hit.normal;
                }
            }
        }
        #endregion

        #region Spawning
        public int vesselsSpawningCount = 0;
        protected string latestSpawnedVesselName = "";
        protected Dictionary<string, Vessel> spawnedVessels = new Dictionary<string, Vessel>(); // Vessel name => vessel instance.
        protected Dictionary<string, string> spawnedVesselURLs = new Dictionary<string, string>(); // Vessel name => URL.
        protected Dictionary<string, int> spawnedVesselsTeamIndex = new Dictionary<string, int>(); // Vessel name => team index
        protected Dictionary<string, int> spawnedVesselPartCounts = new Dictionary<string, int>(); // Vessel name => part count.
        protected Dictionary<string, Vector3> finalSpawnPositions = new Dictionary<string, Vector3>(); // Vessel name => final spawn position (for later reuse).
        protected Dictionary<string, Quaternion> finalSpawnRotations = new Dictionary<string, Quaternion>(); // Vessel name => final spawn rotation (for later reuse).
        protected void ResetInternals()
        {
            // Clear our internal collections and counters.
            vesselsSpawningCount = 0;
            spawnedVessels.Clear();
            spawnedVesselURLs.Clear();
            spawnedVesselsTeamIndex.Clear();
            spawnedVesselPartCounts.Clear();
            finalSpawnPositions.Clear();
            finalSpawnRotations.Clear();
        }

        protected IEnumerator SpawnVessels(List<VesselSpawnConfig> vesselSpawnConfigs)
        {
            ResetInternals();
            // Perform the actual spawning concurrently.
            LogMessage("Spawning vessels...", false);
            List<Coroutine> spawningVessels = new List<Coroutine>();
            foreach (var vesselSpawnConfig in vesselSpawnConfigs)
                spawningVessels.Add(StartCoroutine(SpawnSingleVessel(vesselSpawnConfig)));
            yield return new WaitWhile(() => vesselsSpawningCount > 0 && spawnFailureReason == SpawnFailureReason.None);
            if (spawnFailureReason == SpawnFailureReason.None && spawnedVessels.Count == 0)
            {
                spawnFailureReason = SpawnFailureReason.VesselFailedToSpawn;
                LogMessage("No vessels were spawned!");
            }
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                foreach (var cr in spawningVessels) StopCoroutine(cr);
            }
        }

        protected IEnumerator SpawnSingleVessel(VesselSpawnConfig vesselSpawnConfig)
        {
            ++vesselsSpawningCount;

            Vessel vessel;
            Vector3d craftGeoCoords;
            EditorFacility editorFacility = EditorFacility.SPH;
            var radialUnitVector = (vesselSpawnConfig.position - FlightGlobals.currentMainBody.transform.position).normalized;
            vesselSpawnConfig.position += 1000f * radialUnitVector; // Adjust the spawn point upwards by 1000m.
            FlightGlobals.currentMainBody.GetLatLonAlt(vesselSpawnConfig.position, out craftGeoCoords.x, out craftGeoCoords.y, out craftGeoCoords.z); // Convert spawn point (+1000m) to geo-coords for the actual spawning function.
            try
            {
                // Spawn the craft with zero pitch, roll and yaw as the final rotation depends on the root transform, which takes some time to be populated.
                vessel = VesselSpawner.SpawnVesselFromCraftFile(vesselSpawnConfig.craftURL, craftGeoCoords, 0f, 0f, 0f, out editorFacility); // SPAWN
            }
            catch { vessel = null; }
            if (vessel == null)
            {
                var craftName = Path.GetFileNameWithoutExtension(vesselSpawnConfig.craftURL);
                LogMessage("Failed to spawn craft " + craftName);
                yield break; // Note: this doesn't cancel spawning.
            }
            vessel.Landed = false; // Tell KSP that it's not landed so KSP doesn't mess with its position.
            vessel.ResumeStaging(); // Trigger staging to resume to get staging icons to work properly.
            if (vesselSpawnConfig.reuseURLVesselName && spawnedVesselURLs.ContainsValue(vesselSpawnConfig.craftURL))
            {
                vessel.vesselName = spawnedVesselURLs.Where(kvp => kvp.Value == vesselSpawnConfig.craftURL).Select(kvp => kvp.Key).First();
            }
            else
            {
                if (spawnedVesselURLs.ContainsKey(vessel.vesselName))
                {
                    var count = 1;
                    var potentialName = vessel.vesselName + "_" + count;
                    while (spawnedVesselURLs.ContainsKey(potentialName))
                        potentialName = vessel.vesselName + "_" + (++count);
                    vessel.vesselName = potentialName;
                }
                spawnedVesselURLs.Add(vessel.vesselName, vesselSpawnConfig.craftURL);
            }
            var vesselName = vessel.vesselName;
            latestSpawnedVesselName = vesselName;
            spawnedVesselsTeamIndex[vesselName] = vesselSpawnConfig.teamIndex; // For specific team assignments.
            var heightFromTerrain = vessel.GetHeightFromTerrain() - 35f; // The SpawnVesselFromCraftFile routine adds 35m for some reason.

            // Wait until the vessel's part list gets updated.
            do
            {
                yield return waitForFixedUpdate;
                if (vessel == null)
                {
                    LogMessage(vesselName + " disappeared during spawning!");
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
            } while (vessel.parts.Count == 0);
            spawnedVesselPartCounts[vesselName] = SpawnUtils.PartCount(vessel); // Get the part-count without EVA kerbals.

            // Wait another update so that the reference transforms get updated.
            yield return waitForFixedUpdate;
            var startTime = Time.time;
            // Sometimes if a vessel camera switch occurs, the craft appears unloaded for a couple of frames. This avoids NREs for control surfaces triggered by the change in reference transform.
            while (vessel != null && (vessel.ReferenceTransform == null || vessel.rootPart == null || vessel.rootPart.GetReferenceTransform() == null) && (Time.time - startTime < 1f)) yield return waitForFixedUpdate;
            if (vessel == null || vessel.rootPart == null)
            {
                LogMessage((vessel == null) ? (vesselName + " disappeared during spawning!") : (vesselName + " had no root part during spawning!"));
                spawnFailureReason = SpawnFailureReason.VesselLostParts;
                yield break;
            }
            vessel.SetReferenceTransform(vessel.rootPart); // Set the reference transform to the root part's transform. This includes setting the control point orientation.

            // Now rotate the vessel and put it at the right altitude.
            var ray = new Ray(vesselSpawnConfig.position, -radialUnitVector);
            RaycastHit hit;
            var distanceToCoMainBody = (ray.origin - FlightGlobals.currentMainBody.transform.position).magnitude;
            float distance;
            Vector3 localSurfaceNormal = -ray.direction;
            var localTerrainAltitude = BodyUtils.GetTerrainAltitudeAtPos(ray.origin);
            if (localTerrainAltitude > 0 && Physics.Raycast(ray, out hit, distanceToCoMainBody, (int)LayerMasks.Scenery))
            {
                distance = hit.distance;
                localSurfaceNormal = hit.normal;
            }
            else
            {
                distance = BodyUtils.GetRadarAltitudeAtPos(ray.origin);
                localSurfaceNormal = radialUnitVector;
                if (BDArmorySettings.DEBUG_SPAWNING && localTerrainAltitude > 0) LogMessage("Failed to find terrain for spawn adjustments", false);
            }
            // Rotation
            vessel.SetRotation(Quaternion.FromToRotation(editorFacility == EditorFacility.SPH ? -vessel.ReferenceTransform.forward : vessel.ReferenceTransform.up, localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the terrain normal (or radial unit vector).
            vessel.SetRotation(Quaternion.AngleAxis(Vector3.SignedAngle(editorFacility == EditorFacility.SPH ? vessel.ReferenceTransform.up : -vessel.ReferenceTransform.forward, vesselSpawnConfig.direction, localSurfaceNormal), localSurfaceNormal) * vessel.transform.rotation); // Re-orient the vessel to the right direction.
            if (vesselSpawnConfig.airborne && !BDArmorySettings.SF_GRAVITY)
            { vessel.SetRotation(Quaternion.AngleAxis(-vesselSpawnConfig.pitch, vessel.ReferenceTransform.right) * vessel.transform.rotation); }
            // Position
            if (FlightGlobals.currentMainBody.hasSolidSurface)
            { vesselSpawnConfig.position += radialUnitVector * (vesselSpawnConfig.altitude + heightFromTerrain - distance); }
            else
            { vesselSpawnConfig.position -= 1000f * radialUnitVector; }
            if (vessel.mainBody.ocean) // Check for being under water.
            {
                var distanceUnderWater = -FlightGlobals.getAltitudeAtPos(vesselSpawnConfig.position);
                if (distanceUnderWater >= 0) // Under water.
                {
                    vessel.Splashed = true; // Set the vessel as splashed.
                }
            }
            vessel.SetPosition(vesselSpawnConfig.position);
            finalSpawnPositions[vesselName] = vesselSpawnConfig.position;
            finalSpawnRotations[vesselName] = vessel.transform.rotation;
            vessel.altimeterDisplayState = AltimeterDisplayState.AGL;

            // Game mode adjustments.
            if (BDArmorySettings.SPACE_HACKS)
            {
                var SF = vessel.rootPart.FindModuleImplementing<ModuleSpaceFriction>();
                if (SF == null)
                {
                    SF = (ModuleSpaceFriction)vessel.rootPart.AddModule("ModuleSpaceFriction");
                }
            }
            if (BDArmorySettings.MUTATOR_MODE)
            {
                var MM = vessel.rootPart.FindModuleImplementing<BDAMutator>();
                if (MM == null)
                {
                    MM = (BDAMutator)vessel.rootPart.AddModule("BDAMutator");
                }
            }
            if (BDArmorySettings.HACK_INTAKES)
            {
                SpawnUtils.HackIntakes(vessel, true);
            }

            LogMessage("Vessel " + vesselName + " spawned!", false);
            spawnedVessels[vesselName] = vessel;
            --vesselsSpawningCount;
        }
        #endregion

        #region Post-spawning
        #region Multi-vessel post-spawn functions
        /// <summary>
        /// Perform the main sequence of post-spawn checks and functions for a group of vessels.
        /// </summary>
        /// <param name="spawnConfig"></param>
        /// <param name="spawnAirborne"></param>
        /// <returns></returns>
        protected IEnumerator PostSpawnMainSequence(SpawnConfig spawnConfig, bool spawnAirborne)
        {
            yield return CheckVesselValidity(spawnedVessels);
            if (spawnFailureReason != SpawnFailureReason.None) yield break;

            yield return WaitForWeaponManagers(spawnedVessels, spawnedVesselPartCounts, spawnConfig.numberOfTeams != 1 && spawnConfig.numberOfTeams != -1);
            if (spawnFailureReason != SpawnFailureReason.None) yield break;

            // Reset craft positions and rotations as sometimes KSP packs and unpacks vessels between frames and resets things! (Possibly due to kerbals in command seats?)
            ResetFinalSpawnPositionsAndRotations(spawnedVessels, finalSpawnPositions, finalSpawnRotations);

            // Lower vessels to the ground or activate them in the air.
            if (spawnConfig.altitude >= 0 && !spawnAirborne)
            {
                foreach (var vessel in spawnedVessels.Values) // Turn on the brakes.
                {
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false); // Disable them first to make sure they trigger on toggling.
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                    if (BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED == 0) PlaceSpawnedVessel(vessel);
                }
                if (BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED > 0)
                    yield return LowerVesselsToSurface(spawnedVessels, spawnedVesselPartCounts, spawnConfig.easeInSpeed, spawnConfig.altitude);
            }
            else AirborneActivation(spawnedVessels);
            if (spawnFailureReason != SpawnFailureReason.None) yield break;

            // One last check for renamed vessels (since we're not entirely sure when this occurs).
            SpawnUtils.CheckForRenamedVessels(spawnedVessels);

            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                // Check AI/WM counts and placement for RWP.
                foreach (var vesselName in spawnedVessels.Keys)
                {
                    SpawnUtils.CheckAIWMCounts(spawnedVessels[vesselName]);
                    SpawnUtils.CheckAIWMPlacement(spawnedVessels[vesselName]);
                }
            }
        }

        /// <summary>
        /// Check a group of vessels for being valid. Times out after 1s.
        /// </summary>
        /// <param name="vessels"></param>
        /// <returns></returns>
        protected IEnumerator CheckVesselValidity(Dictionary<string, Vessel> vessels)
        {
            var startTime = Time.time;
            Dictionary<string, BDACompetitionMode.InvalidVesselReason> invalidVessels;
            // Check that the spawned vessels are valid craft
            do
            {
                yield return waitForFixedUpdate;
                invalidVessels = vessels.ToDictionary(kvp => kvp.Key, kvp => BDACompetitionMode.Instance.IsValidVessel(kvp.Value)).Where(kvp => kvp.Value != BDACompetitionMode.InvalidVesselReason.None).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            } while (invalidVessels.Count > 0 && Time.time - startTime < 1); // Give it up to 1s for KSP to populate the vessel's AI and WM.
            if (invalidVessels.Count > 0)
            {
                LogMessage("The following vessels are invalid:\n - " + string.Join("\n - ", invalidVessels.Select(t => t.Key + " : " + t.Value)), true, false);
                LogMessage("Invalid vessels: " + string.Join(", ", invalidVessels.Select(t => t.Key + ":" + t.Value)), false, true);
                spawnFailureReason = SpawnFailureReason.InvalidVessel;
            }
        }

        /// <summary>
        /// Wait for weapon managers of a group of vessels to appear in the Vessel Switcher.
        /// </summary>
        /// <param name="vessels"></param>
        /// <param name="vesselPartCounts"></param>
        /// <param name="saveTeams"></param>
        /// <returns></returns>
        protected IEnumerator WaitForWeaponManagers(Dictionary<string, Vessel> vessels, Dictionary<string, int> vesselPartCounts, bool saveTeams)
        {
            var vesselsToCheck = vessels.Keys.ToList();
            var allWeaponManagersAssigned = false;
            var startTime = Time.time;
            do
            {
                yield return waitForFixedUpdate;
                SpawnUtils.CheckForRenamedVessels(vessels);

                // Check that none of the vessels have lost parts.
                if (vessels.Any(kvp => kvp.Value == null || SpawnUtils.PartCount(kvp.Value) != vesselPartCounts[kvp.Key]))
                {
                    var offendingVessels = vessels.Where(kvp => kvp.Value == null || SpawnUtils.PartCount(kvp.Value) != vesselPartCounts[kvp.Key]);
                    LogMessage("Part-count of some vessels changed after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value == null ? "null" : kvp.Value.vesselName + $" ({vesselPartCounts[kvp.Key] - SpawnUtils.PartCount(kvp.Value)})")));
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }

                // Wait for all the weapon managers to be added to LoadedVesselSwitcher.
                LoadedVesselSwitcher.Instance.UpdateList();
                var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                foreach (var vesselName in vesselsToCheck.ToList())
                {
                    var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessels[vesselName]);
                    if (weaponManager != null && weaponManagers.Contains(weaponManager)) // The weapon manager has been added, let's go!
                    { vesselsToCheck.Remove(vesselName); }
                }
                if (vesselsToCheck.Count == 0)
                    allWeaponManagersAssigned = true;

                if (allWeaponManagersAssigned)
                {
                    if (saveTeams) // Already assigned.
                        SpawnUtils.SaveTeams();
                    yield break; // Success!
                }
            } while (Time.time - startTime < 10); // Give it up to 10s for the weapon managers to get added to the LoadedVesselSwitcher's list.
            LogMessage("Timed out waiting for weapon managers to appear in the Vessel Switcher.", true, false);
            spawnFailureReason = SpawnFailureReason.TimedOut;
        }

        protected void ResetFinalSpawnPositionsAndRotations(Dictionary<string, Vessel> vessels, Dictionary<string, Vector3> positions, Dictionary<string, Quaternion> rotations)
        {
            // Reset craft positions and rotations as sometimes KSP packs and unpacks vessels between frames and resets things!
            foreach (var vesselName in vessels.Keys)
            {
                if (vessels[vesselName] == null) continue;
                vessels[vesselName].SetPosition(finalSpawnPositions[vesselName]);
                vessels[vesselName].SetRotation(finalSpawnRotations[vesselName]);
            }
        }

        protected IEnumerator LowerVesselsToSurface(Dictionary<string, Vessel> vessels, Dictionary<string, int> partCounts, float easeInSpeed, double altitude)
        {
            var radialUnitVectors = vessels.ToDictionary(v => v.Key, v => (v.Value.transform.position - FlightGlobals.currentMainBody.transform.position).normalized);
            // Prevent the vessels from falling too fast and check if their velocities in the surface normal direction is below a threshold.
            var vesselsHaveLanded = vessels.Keys.ToDictionary(v => v, v => (int)0); // 1=started moving, 2=landed.
            var landingStartTime = Time.time;
            do
            {
                yield return waitForFixedUpdate;
                foreach (var vesselName in vessels.Keys)
                {
                    var vessel = vessels[vesselName];
                    if (vessel.LandedOrSplashed && BodyUtils.GetRadarAltitudeAtPos(vessel.transform.position) <= 0) // Wait for the vessel to settle a bit in the water. The 15s buffer should be more than sufficient.
                    {
                        vesselsHaveLanded[vesselName] = 2;
                    }
                    if (vesselsHaveLanded[vesselName] == 0 && Vector3.Dot(vessel.srf_velocity, radialUnitVectors[vesselName]) < 0) // Check that vessel has started moving.
                        vesselsHaveLanded[vesselName] = 1;
                    if (vesselsHaveLanded[vesselName] == 1 && Vector3.Dot(vessel.srf_velocity, radialUnitVectors[vesselName]) >= 0) // Check if the vessel has landed.
                    {
                        vesselsHaveLanded[vesselName] = 2;
                        if (BodyUtils.GetRadarAltitudeAtPos(vessel.transform.position) > 0)
                            vessel.Landed = true; // Tell KSP that the vessel is landed.
                        else
                            vessel.Splashed = true; // Tell KSP that the vessel is splashed.
                    }
                    if (vesselsHaveLanded[vesselName] == 1 && vessel.srf_velocity.sqrMagnitude > easeInSpeed) // While the vessel hasn't landed, prevent it from moving too fast.
                        vessel.SetWorldVelocity(0.99 * easeInSpeed * vessel.srf_velocity); // Move at VESSEL_SPAWN_EASE_IN_SPEED m/s at most.
                }

                // Check that none of the vessels have lost parts.
                if (vessels.Any(kvp => SpawnUtils.PartCount(kvp.Value) < partCounts[kvp.Key]))
                {
                    var offendingVessels = vessels.Where(kvp => SpawnUtils.PartCount(kvp.Value) < partCounts[kvp.Key]);
                    LogMessage("Part-count of some vessels changed after spawning: " + string.Join(", ", offendingVessels.Select(kvp => kvp.Value == null ? "null" : kvp.Value.vesselName + $" ({partCounts[kvp.Key] - SpawnUtils.PartCount(kvp.Value)})")));
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }

                if (vesselsHaveLanded.Values.All(v => v == 2)) yield break;
            } while (Time.time - landingStartTime < 15 + altitude / easeInSpeed); // Give the vessels up to (15 + altitude / VESSEL_SPAWN_EASE_IN_SPEED) seconds to land.
            LogMessage("Timed out waiting for the vessels to land.", true, false);
            spawnFailureReason = SpawnFailureReason.TimedOut;
        }

        /// <summary>
        /// Activation sequence for airborne vessels.
        /// </summary>
        /// <param name="vessels"></param>
        protected void AirborneActivation(Dictionary<string, Vessel> vessels)
        {
            foreach (var vessel in vessels.Select(v => v.Value))
            { AirborneActivation(vessel); }
        }
        #endregion

        #region Single vessel post-spawn functions
        /// <summary>
        /// Get the vessel corresponding to the craftURL from the spawnedVesselURLs dictionary.
        /// Note: this is only valid when craftURLs are unique for each spawned vessel (i.e., when vesselSpawnConfig.reuseURLVesselName is true) (like in continuous spawning).
        /// </summary>
        /// <param name="craftURL"></param>
        /// <returns></returns>
        protected Vessel GetSpawnedVesselsName(string craftURL)
        {
            // Find the vesselName for the craft URL.
            var vesselName = spawnedVesselURLs.Where(kvp => kvp.Value == craftURL).Select(kvp => kvp.Key).FirstOrDefault();
            if (string.IsNullOrEmpty(vesselName) || !spawnedVessels.ContainsKey(vesselName))
            {
                spawnFailureReason = SpawnFailureReason.VesselFailedToSpawn;
                if (!string.IsNullOrEmpty(vesselName))
                {
                    foreach (var vessl in FlightGlobals.Vessels) // If the vessel was partially spawned, find and remove it.
                    {
                        if (vessl == null) continue;
                        if (vessl.vesselName == vesselName) RemoveVessel(vessl);
                    }
                    return null;
                }
            }
            return spawnedVessels[vesselName];
        }

        /// <summary>
        /// Perform the main sequence of post-spawn checks and functions for a vessel.
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="spawnAirborne"></param>
        /// <returns></returns>
        protected IEnumerator PostSpawnMainSequence(Vessel vessel, bool spawnAirborne)
        {
            var vesselName = vessel.vesselName;

            yield return CheckVesselValidity(vessel);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                if (vessel != null) RemoveVessel(vessel);
                yield break;
            }

            yield return WaitForWeaponManager(vessel);
            if (spawnFailureReason != SpawnFailureReason.None)
            {
                if (vessel != null) RemoveVessel(vessel);
                yield break;
            }

            // Reset craft positions and rotations as sometimes KSP packs and unpacks vessels between frames and resets things! (Possibly due to kerbals in command seats?)
            vessel.SetPosition(finalSpawnPositions[vesselName]);
            vessel.SetRotation(finalSpawnRotations[vesselName]);

            // Undo any camera adjustment and reset the camera distance. This has an internal check so that it only occurs once.
            SpawnUtils.RevertSpawnLocationCamera(true);
            if (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.state == Vessel.State.DEAD)
            {
                LoadedVesselSwitcher.Instance.ForceSwitchVessel(vessel); // Update the camera.
                FlightCamera.fetch.SetDistance(50);
            }

            // Lower vessel to the ground or activate them in the air.
            if (vessel.radarAltitude >= 0 && !spawnAirborne)
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false); // Disable them first to make sure they trigger on toggling.
                vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                if (BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED == 0) PlaceSpawnedVessel(vessel);
                else
                {
                    yield return LowerVesselsToSurface(new Dictionary<string, Vessel> { { vesselName, vessel } }, new Dictionary<string, int> { { vesselName, SpawnUtils.PartCount(vessel) } }, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, vessel.radarAltitude);
                    if (spawnFailureReason != SpawnFailureReason.None)
                    {
                        if (vessel != null) RemoveVessel(vessel);
                        yield break;
                    }
                }
            }
            else AirborneActivation(vessel);

            // Check for the vessel having been renamed from the VESSELNAMING tag (not sure when this occurs, but it should be before now).
            if (vesselName != vessel.vesselName)
                vessel.vesselName = vesselName;

            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                // Check AI/WM counts and placement for RWP.
                SpawnUtils.CheckAIWMCounts(vessel);
                SpawnUtils.CheckAIWMPlacement(vessel);
            }
        }

        /// <summary>
        /// Check a single vessel for being valid. Times out after 1s.
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        protected IEnumerator CheckVesselValidity(Vessel vessel)
        {
            var startTime = Time.time;
            var validity = BDACompetitionMode.Instance.IsValidVessel(vessel);
            while (validity != BDACompetitionMode.InvalidVesselReason.None && Time.time - startTime < 1)
            {
                yield return waitForFixedUpdate;
                validity = BDACompetitionMode.Instance.IsValidVessel(vessel);
                vessel.SetPosition(finalSpawnPositions[vessel.vesselName]); // Prevent the vessel from falling.
            }
            if (validity != BDACompetitionMode.InvalidVesselReason.None)
            {
                LogMessage($"The vessel {vessel.vesselName} is invalid: {validity}");
                spawnFailureReason = SpawnFailureReason.InvalidVessel;
            }
        }

        /// <summary>
        /// Wait for the weapon manager to appear in the Vessel Switcher (single vessel version).
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        protected IEnumerator WaitForWeaponManager(Vessel vessel)
        {
            yield return waitForFixedUpdate; // Wait at least one update so the vessel parts list is populated.
            var startTime = Time.time;
            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
            var vesselName = vessel.vesselName; // In case it disappears.
            var assigned = weaponManager != null && LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Contains(weaponManager);
            while (!assigned && Time.time - startTime < 10 && vessel != null)
            {
                yield return waitForFixedUpdate;
                if (vessel == null || SpawnUtils.PartCount(vessel) != spawnedVesselPartCounts[vesselName])
                {
                    LogMessage($"Part-count of {vesselName} changed after spawning: {(vessel == null ? spawnedVesselPartCounts[vesselName] : spawnedVesselPartCounts[vesselName] - SpawnUtils.PartCount(vessel))}");
                    spawnFailureReason = SpawnFailureReason.VesselLostParts;
                    yield break;
                }
                if (weaponManager == null) weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
                assigned = weaponManager != null && LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Contains(weaponManager);
                vessel.SetPosition(finalSpawnPositions[vesselName]); // Prevent the vessel from falling.
            }
            if (!assigned)
            {
                LogMessage("Timed out waiting for weapon managers to appear in the Vessel Switcher.", true, false);
                spawnFailureReason = SpawnFailureReason.TimedOut;
            }
        }

        /// <summary>
        /// Activation sequence for an airborne vessel.
        /// 
        /// Checks for the vessel or weapon manager being null or having lost parts should have been done before calling this.
        /// </summary>
        /// <param name="vessel"></param>
        protected void AirborneActivation(Vessel vessel)
        {
            // Activate the vessel with AG10, or failing that, staging.
            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
            vessel.ActionGroups.ToggleGroup(BDACompetitionMode.KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
            weaponManager.AI.ActivatePilot();
            weaponManager.AI.CommandTakeOff();
            if (weaponManager.guardMode)
            {
                if (BDArmorySettings.DEBUG_SPAWNING) LogMessage($"Disabling guardMode on {vessel.vesselName}.", false);
                weaponManager.ToggleGuardMode(); // Disable guard mode (in case someone enabled it on AG10 or in the SPH).
                weaponManager.SetTarget(null);
            }

            if (!BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
            {
                if (BDArmorySettings.DEBUG_SPAWNING) LogMessage(vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.", false);
                SpawnUtils.ActivateAllEngines(vessel);
            }
            else if (BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
            {
                SpawnUtils.ActivateAllEngines(vessel, false);
            }
        }

        /// <summary>
        /// Place a spawned vessel on the ground/water surface in a single step.
        /// </summary>
        /// <param name="vessel"></param>
        protected void PlaceSpawnedVessel(Vessel vessel)
        {
            var down = FlightGlobals.getGeeForceAtPosition(vessel.CoM).normalized;
            if (BodyUtils.GetTerrainAltitudeAtPos(vessel.CoM, true) < 0) // Over water.
            {
                if (BDArmorySettings.DEBUG_SPAWNING) LogMessage($"{vessel.vesselName} is {vessel.altitude:G3}m above water, lowering.", false);
                vessel.SetPosition(vessel.transform.position + vessel.altitude * down);
                return;
            }
            // Over land.
            var radius = vessel.GetRadius();
            var terrainHits = Physics.SphereCastAll(vessel.CoM - radius * down, radius, down, (float)vessel.altitude + radius, (int)LayerMasks.Scenery); // Start "radius" above the CoM so the minimum distance is the altitude of the CoM.
            var minDistance = (float)vessel.altitude;
            if (terrainHits.Length > 0) { minDistance = Mathf.Min(minDistance, radius + terrainHits.Min(h => h.distance)); }
            foreach (var terrainHit in terrainHits)
            {
                if (Physics.BoxCast(terrainHit.point + 0.1f * down, new Vector3(radius, 0.1f, radius), -down, out RaycastHit hit, Quaternion.FromToRotation(Vector3.up, terrainHit.normal), terrainHit.distance + 1f, (int)(LayerMasks.Parts | LayerMasks.EVA | LayerMasks.Wheels)))
                {
                    var hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                    if (BDArmorySettings.DEBUG_SPAWNING) LogMessage($"{vessel.vesselName}: Distance from {terrainHit.collider.name} to {hit.collider.name} ({hitPart.name}): {hit.distance:G3}m", false);
                    minDistance = Mathf.Min(minDistance, hit.distance);
                }
            }
            if (BDArmorySettings.DEBUG_SPAWNING) LogMessage($"{vessel.vesselName} is {minDistance:G3}m above land, lowering.", false);
            vessel.SetPosition(vessel.transform.position + down * (minDistance - 0.1f));
        }

        public void AddToActiveCompetition(Vessel vessel, bool airborne)
        {
            var vesselName = vessel.vesselName;
            // If a competition is active, update the scoring structure.
            if ((BDACompetitionMode.Instance.competitionStarting || BDACompetitionMode.Instance.competitionIsActive) && !BDACompetitionMode.Instance.Scores.Players.Contains(vesselName))
            {
                BDACompetitionMode.Instance.Scores.AddPlayer(vessel);
            }
            if (ContinuousSpawning.Instance.vesselsSpawningContinuously)
            {
                if (!ContinuousSpawning.Instance.continuousSpawningScores.ContainsKey(vesselName))
                    ContinuousSpawning.Instance.continuousSpawningScores.Add(vesselName, new ContinuousSpawning.ContinuousSpawningScores());
                ContinuousSpawning.Instance.continuousSpawningScores[vesselName].vessel = vessel; // Update some values in the scoring structure.
                ContinuousSpawning.Instance.continuousSpawningScores[vesselName].outOfAmmoTime = 0;
            }

            var weaponManager = VesselModuleRegistry.GetModule<MissileFire>(vessel);
            if (BDArmorySettings.TAG_MODE && !string.IsNullOrEmpty(BDACompetitionMode.Instance.Scores.currentlyIT))
            { weaponManager.SetTeam(BDTeam.Get("NO")); }
            else
            {
                // Assign the vessel to an unassigned team.
                var weaponManagers = LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList();
                var currentTeams = weaponManagers.Where(wm => wm != weaponManager).Select(wm => wm.Team).ToHashSet(); // Current teams, excluding us.
                char team = 'A';
                while (currentTeams.Contains(BDTeam.Get(team.ToString())))
                    ++team;
                weaponManager.SetTeam(BDTeam.Get(team.ToString()));
            }

            if (!airborne)
            {
                weaponManager.AI.ActivatePilot();
                weaponManager.AI.CommandTakeOff();
                if (!BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) == 0) // If the vessel didn't activate their engines on AG10, then activate all their engines and hope for the best.
                {
                    if (BDArmorySettings.DEBUG_SPAWNING) LogMessage(vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.", false);
                    SpawnUtils.ActivateAllEngines(vessel);
                }
                else if (BDArmorySettings.NO_ENGINES && SpawnUtils.CountActiveEngines(vessel) > 0) // Vessel had some active engines. Turn them off if possible.
                {
                    SpawnUtils.ActivateAllEngines(vessel, false);
                }
            }
            // Enable guard mode if a competition is active.
            if (BDACompetitionMode.Instance.competitionIsActive && !weaponManager.guardMode) weaponManager.ToggleGuardMode();
            weaponManager.AI.ReleaseCommand();
            weaponManager.ForceScan();

            if (ContinuousSpawning.Instance.vesselsSpawningContinuously)
            {
                // Adjust BDACompetitionMode's scoring structures.
                ContinuousSpawning.Instance.UpdateCompetitionScores(vessel, true);
                ++ContinuousSpawning.Instance.continuousSpawningScores[vesselName].spawnCount;
            }

            // Update the ramming information for the new vessel.
            if (BDACompetitionMode.Instance.rammingInformation != null)
            { BDACompetitionMode.Instance.AddPlayerToRammingInformation(vessel); }
        }
        #endregion

        #region Utils
        /// <summary>
        /// Remove a vessel, removing it from the spawning dictionaries too.
        /// </summary>
        /// <param name="vessel"></param>
        protected void RemoveVessel(Vessel vessel)
        {
            var vesselName = vessel.vesselName;
            if (spawnedVessels.ContainsKey(vesselName)) spawnedVessels.Remove(vesselName);
            if (spawnedVesselURLs.ContainsKey(vesselName)) spawnedVesselURLs.Remove(vesselName);
            if (spawnedVesselsTeamIndex.ContainsKey(vesselName)) spawnedVesselsTeamIndex.Remove(vesselName);
            if (spawnedVesselPartCounts.ContainsKey(vesselName)) spawnedVesselPartCounts.Remove(vesselName);
            if (finalSpawnPositions.ContainsKey(vesselName)) finalSpawnPositions.Remove(vesselName);
            if (finalSpawnRotations.ContainsKey(vesselName)) finalSpawnRotations.Remove(vesselName);
            SpawnUtils.RemoveVessel(vessel);
        }
        #endregion
        #endregion
    }
}