using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.Competition;
using BDArmory.Radar;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Control
{
    // trivial score keeping structure
    public class ScoringData
    {
        public Vessel vesselRef; // TODO Reuse these fields instead of looking for them each time.
        public MissileFire weaponManagerRef;
        public int Score;
        public int PinataHits;
        public int totalDamagedPartsDueToRamming = 0;
        public int totalDamagedPartsDueToMissiles = 0;
        public string lastPersonWhoHitMe = "";
        public string lastPersonWhoHitMeWithAMissile = "";
        public string lastPersonWhoRammedMe = "";
        public double lastHitTime; // Bullets
        public bool tagIsIt = false; // For tag mode
        public int tagKillsWhileIt = 0; // For tag mode
        public int tagTimesIt = 0; // For tag mode
        public double tagTotalTime = 0; // For tag mode
        public double tagScore = 0; // For tag mode
        public double lastMissileHitTime; // Missiles
        public double lastFiredTime;
        public double lastRammedTime; // Rams
        public bool landedState;
        public double lastLandedTime;
        public double landedKillTimer;
        public double AverageSpeed;
        public double AverageAltitude;
        public int averageCount;
        public int previousPartCount;
        public double lastLostPartTime = 0; // Time of losing last part (up to granularity of the updateTickLength).
        public HashSet<string> everyoneWhoHitMe = new HashSet<string>();
        public HashSet<string> everyoneWhoRammedMe = new HashSet<string>();
        public HashSet<string> everyoneWhoHitMeWithMissiles = new HashSet<string>();
        public HashSet<string> everyoneWhoDamagedMe = new HashSet<string>();
        public Dictionary<string, int> hitCounts = new Dictionary<string, int>();
        public Dictionary<string, float> damageFromBullets = new Dictionary<string, float>();
        public Dictionary<string, float> damageFromMissiles = new Dictionary<string, float>();
        public int shotsFired = 0;
        public Dictionary<string, int> rammingPartLossCounts = new Dictionary<string, int>();
        public Dictionary<string, int> missilePartDamageCounts = new Dictionary<string, int>();
        public Dictionary<string, int> missileHitCounts = new Dictionary<string, int>();
        public GMKillReason gmKillReason = GMKillReason.None;
        public bool cleanDeath = false;
        public string team;

        public double LastDamageTime()
        {
            var lastDamageWasFrom = LastDamageWasFrom();
            switch (lastDamageWasFrom)
            {
                case DamageFrom.Bullet:
                    return lastHitTime;
                case DamageFrom.Missile:
                    return lastMissileHitTime;
                case DamageFrom.Ram:
                    return lastRammedTime;
                default:
                    return 0;
            }
        }
        public DamageFrom LastDamageWasFrom()
        {
            double lastTime = 0;
            var damageFrom = DamageFrom.None;
            if (lastHitTime > lastTime)
            {
                lastTime = lastHitTime;
                damageFrom = DamageFrom.Bullet;
            }
            if (lastMissileHitTime > lastTime)
            {
                lastTime = lastMissileHitTime;
                damageFrom = DamageFrom.Missile;
            }
            if (lastRammedTime > lastTime)
            {
                lastTime = lastRammedTime;
                damageFrom = DamageFrom.Ram;
            }
            return damageFrom;
        }
        public string LastPersonWhoDamagedMe()
        {
            var lastDamageWasFrom = LastDamageWasFrom();
            switch (lastDamageWasFrom)
            {
                case DamageFrom.Bullet:
                    return lastPersonWhoHitMe;
                case DamageFrom.Missile:
                    return lastPersonWhoHitMeWithAMissile;
                case DamageFrom.Ram:
                    return lastPersonWhoRammedMe;
                default:
                    return "";
            }
        }

        public HashSet<string> EveryOneWhoDamagedMe()
        {
            foreach (var hit in everyoneWhoHitMe)
            {
                everyoneWhoDamagedMe.Add(hit);
            }

            foreach (var ram in everyoneWhoRammedMe)
            {
                if (!everyoneWhoDamagedMe.Contains(ram))
                {
                    everyoneWhoDamagedMe.Add(ram);
                }
            }

            foreach (var hit in everyoneWhoHitMeWithMissiles)
            {
                if (!everyoneWhoDamagedMe.Contains(hit))
                {
                    everyoneWhoDamagedMe.Add(hit);
                }
            }

            return everyoneWhoDamagedMe;
        }
    }
    public enum DamageFrom { None, Bullet, Missile, Ram };
    public enum GMKillReason { None, GM, OutOfAmmo, BigRedButton };
    public enum CompetitionStartFailureReason { None, OnlyOneTeam, TeamsChanged, TeamLeaderDisappeared, PilotDisappeared };


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDACompetitionMode : MonoBehaviour
    {
        public static BDACompetitionMode Instance;

        #region Flags and variables
        // Score tracking flags and variables.
        public Dictionary<string, ScoringData> Scores = new Dictionary<string, ScoringData>();
        public Dictionary<string, Tuple<int, double>> DeathOrder = new Dictionary<string, Tuple<int, double>>();
        public Dictionary<string, string> whoCleanShotWho = new Dictionary<string, string>();
        public Dictionary<string, string> whoCleanShotWhoWithMissiles = new Dictionary<string, string>();
        public Dictionary<string, string> whoCleanRammedWho = new Dictionary<string, string>();

        // Competition flags and variables
        public int CompetitionID; // time competition was started
        bool competitionShouldBeRunning = false;
        public double competitionStartTime = -1;
        public double nextUpdateTick = -1;
        private double decisionTick = -1;
        private double finalGracePeriodStart = -1;
        public static int DeathCount = 0;
        public static float gravityMultiplier = 1f;
        float lastGravityMultiplier;
        private string deadOrAlive = "";
        static HashSet<string> outOfAmmo = new HashSet<string>(); // outOfAmmo register for tracking which planes are out of ammo.

        // Action groups
        public static Dictionary<int, KSPActionGroup> KM_dictAG = new Dictionary<int, KSPActionGroup> {
            { 0,  KSPActionGroup.None },
            { 1,  KSPActionGroup.Custom01 },
            { 2,  KSPActionGroup.Custom02 },
            { 3,  KSPActionGroup.Custom03 },
            { 4,  KSPActionGroup.Custom04 },
            { 5,  KSPActionGroup.Custom05 },
            { 6,  KSPActionGroup.Custom06 },
            { 7,  KSPActionGroup.Custom07 },
            { 8,  KSPActionGroup.Custom08 },
            { 9,  KSPActionGroup.Custom09 },
            { 10, KSPActionGroup.Custom10 },
            { 11, KSPActionGroup.Light },
            { 12, KSPActionGroup.RCS },
            { 13, KSPActionGroup.SAS },
            { 14, KSPActionGroup.Brakes },
            { 15, KSPActionGroup.Abort },
            { 16, KSPActionGroup.Gear }
        };

        // Tag mode flags and variables.
        public bool startTag = false; // For tag mode
        public int previousNumberCompetitive = 2; // Also for tag mode

        // KILLER GM - how we look for slowest planes
        public Dictionary<string, double> KillTimer = new Dictionary<string, double>(); // Note that this is only used as an indicator, not a controller, now.
        //public Dictionary<string, double> AverageSpeed = new Dictionary<string, double>();
        //public Dictionary<string, double> AverageAltitude = new Dictionary<string, double>();
        //public Dictionary<string, int> FireCount = new Dictionary<string, int>();
        //public Dictionary<string, int> FireCount2 = new Dictionary<string, int>();

        // pilot actions
        private Dictionary<string, string> pilotActions = new Dictionary<string, string>();
        #endregion

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        void OnGUI()
        {
            GUIStyle cStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.label);
            cStyle.fontStyle = FontStyle.Bold;
            cStyle.fontSize = 22;
            cStyle.alignment = TextAnchor.UpperLeft;

            var displayRow = 100;
            if (!BDArmorySetup.GAME_UI_ENABLED)
            {
                displayRow = 30;
            }

            Rect cLabelRect = new Rect(30, displayRow, Screen.width, 100);

            GUIStyle cShadowStyle = new GUIStyle(cStyle);
            Rect cShadowRect = new Rect(cLabelRect);
            cShadowRect.x += 2;
            cShadowRect.y += 2;
            cShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);

            string message = competitionStatus.ToString();
            if (competitionStarting || competitionStartTime > 0)
            {
                string currentVesselStatus = "";
                if (FlightGlobals.ActiveVessel != null)
                {
                    var vesselName = FlightGlobals.ActiveVessel.GetName();
                    string postFix = "";
                    if (pilotActions.ContainsKey(vesselName))
                        postFix = pilotActions[vesselName];
                    if (Scores.ContainsKey(vesselName))
                    {
                        ScoringData vData = Scores[vesselName];
                        if (Planetarium.GetUniversalTime() - vData.lastHitTime < 2)
                            postFix = " is taking damage from " + vData.lastPersonWhoHitMe;
                    }
                    if (postFix != "" || vesselName != competitionStatus.lastActiveVessel)
                        currentVesselStatus = vesselName + postFix;
                    competitionStatus.lastActiveVessel = vesselName;
                }
                message += "\n" + currentVesselStatus;
            }

            GUI.Label(cShadowRect, message, cShadowStyle);
            GUI.Label(cLabelRect, message, cStyle);

            if (!BDArmorySetup.GAME_UI_ENABLED && competitionStartTime > 0)
            {
                Rect clockRect = new Rect(10, 6, Screen.width, 20);
                GUIStyle clockStyle = new GUIStyle(cStyle);
                clockStyle.fontSize = 14;
                GUIStyle clockShadowStyle = new GUIStyle(clockStyle);
                clockShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
                Rect clockShadowRect = new Rect(clockRect);
                clockShadowRect.x += 2;
                clockShadowRect.y += 2;
                var gTime = Planetarium.GetUniversalTime() - competitionStartTime;
                var minutes = (int)(Math.Floor(gTime / 60));
                var seconds = (int)(gTime % 60);
                string pTime = minutes.ToString("00") + ":" + seconds.ToString("00") + "     " + deadOrAlive;
                GUI.Label(clockShadowRect, pTime, clockShadowStyle);
                GUI.Label(clockRect, pTime, clockStyle);
            }
            if (KSP.UI.Dialogs.FlightResultsDialog.isDisplaying && KSP.UI.Dialogs.FlightResultsDialog.showExitControls) // Prevent the Flight Results window from interrupting things when a certain vessel dies.
            {
                KSP.UI.Dialogs.FlightResultsDialog.Close();
            }
        }

        void OnDestroy()
        {
            StopCompetition();
            StopAllCoroutines();
        }

        #region Competition start/stop routines
        //Competition mode
        public bool competitionStarting = false;
        public bool sequencedCompetitionStarting = false;
        public bool competitionIsActive = false;
        Coroutine competitionRoutine;
        public CompetitionStartFailureReason competitionStartFailureReason;

        public class CompetitionStatus
        {
            private List<Tuple<double, string>> status = new List<Tuple<double, string>>();
            public void Add(string message) { status.Add(new Tuple<double, string>(Planetarium.GetUniversalTime(), message)); }
            public void Set(string message) { status.Clear(); Add(message); }
            public override string ToString()
            {
                var now = Planetarium.GetUniversalTime();
                status = status.Where(s => now - s.Item1 < 5).ToList(); // Update the list of status messages. Only show messages for 5s.
                return string.Join("\n", status.Select(s => s.Item2)); // Join them together to display them.
            }
            public int Count { get { return status.Count; } }
            public string lastActiveVessel = "";
        }

        public CompetitionStatus competitionStatus = new CompetitionStatus();

        bool startCompetitionNow = false;
        Coroutine startCompetitionNowCoroutine;
        public void StartCompetitionNow(float delay = 0)
        {
            startCompetitionNowCoroutine = StartCoroutine(StartCompetitionNowCoroutine(delay));
        }
        IEnumerator StartCompetitionNowCoroutine(float delay = 0) // Skip the "Competition: Waiting for teams to get in position."
        {
            yield return new WaitForSeconds(delay);
            if (competitionStarting)
            {
                competitionStatus.Add("No longer waiting for teams to get in position.");
                startCompetitionNow = true;
            }
        }

        public void StartCompetitionMode(float distance)
        {
            if (!competitionStarting)
            {
                DeathCount = 0;
                ResetCompetitionStuff();
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Starting Competition");
                startCompetitionNow = false;
                if (BDArmorySettings.GRAVITY_HACKS)
                {
                    lastGravityMultiplier = 1f;
                    gravityMultiplier = 1f;
                    PhysicsGlobals.GraviticForceMultiplier = (double)gravityMultiplier;
                    VehiclePhysics.Gravity.Refresh();
                }
                RemoveDebrisNow();
                GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
                GameEvents.onVesselCreate.Add(OnVesselModified);
                if (BDArmorySettings.AUTO_ENABLE_VESSEL_SWITCHING)
                    LoadedVesselSwitcher.Instance.EnableAutoVesselSwitching(true);
                competitionStartFailureReason = CompetitionStartFailureReason.None;
                competitionRoutine = StartCoroutine(DogfightCompetitionModeRoutine(distance));
                if (BDArmorySettings.COMPETITION_START_NOW_AFTER < 11)
                {
                    if (BDArmorySettings.COMPETITION_START_NOW_AFTER > 5)
                        StartCompetitionNow((BDArmorySettings.COMPETITION_START_NOW_AFTER - 5) * 60);
                    else
                        StartCompetitionNow(BDArmorySettings.COMPETITION_START_NOW_AFTER * 10);
                }
                if (BDArmorySettings.KERBAL_SAFETY > 0)
                    KerbalSafetyManager.Instance.CheckAllVesselsForKerbals();
            }
        }

        public void StopCompetition()
        {
            LogResults();
            if (competitionRoutine != null)
            {
                StopCoroutine(competitionRoutine);
            }
            if (startCompetitionNowCoroutine != null)
            {
                StopCoroutine(startCompetitionNowCoroutine);
            }

            competitionStarting = false;
            competitionIsActive = false;
            sequencedCompetitionStarting = false;
            competitionStartTime = -1;
            competitionShouldBeRunning = false;
            GameEvents.onCollision.Remove(AnalyseCollision);
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            GameEvents.onVesselCreate.Remove(OnVesselModified);
            GameEvents.onVesselCreate.Remove(DebrisDelayedCleanUp);
            GameEvents.onCometSpawned.Remove(RemoveCometVessel);
            rammingInformation = null; // Reset the ramming information.
        }

        void CompetitionStarted()
        {
            competitionIsActive = true; //start logging ramming now that the competition has officially started
            competitionStarting = false;
            sequencedCompetitionStarting = false;
            GameEvents.onCollision.Add(AnalyseCollision); // Start collision detection
            GameEvents.onVesselCreate.Add(DebrisDelayedCleanUp);
            DisableCometSpawning();
            GameEvents.onCometSpawned.Add(RemoveCometVessel);
            competitionStartTime = Planetarium.GetUniversalTime();
            nextUpdateTick = competitionStartTime + 2; // 2 seconds before we start tracking
            decisionTick = BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60 ? -1 : competitionStartTime + BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY; // every 60 seconds we do nasty things
            finalGracePeriodStart = -1;
            lastTagUpdateTime = competitionStartTime;
            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Competition Started");
        }

        public void ResetCompetitionStuff()
        {
            // reinitilize everything when the button get hit.
            CompetitionID = (int)DateTime.UtcNow.Subtract(new DateTime(2020, 1, 1)).TotalSeconds;
            DoPreflightChecks();
            Scores.Clear();
            DeathOrder.Clear();
            whoCleanShotWho.Clear();
            whoCleanShotWhoWithMissiles.Clear();
            whoCleanRammedWho.Clear();
            KillTimer.Clear();
            nonCompetitorsToRemove.Clear();
            pilotActions.Clear(); // Clear the pilotActions, so we don't get "<pilot> is Dead" on the next round of the competition.
            rammingInformation = null; // Reset the ramming information.
            finalGracePeriodStart = -1;
            competitionStartTime = competitionIsActive ? Planetarium.GetUniversalTime() : -1;
            nextUpdateTick = competitionStartTime + 2; // 2 seconds before we start tracking
            decisionTick = BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60 ? -1 : competitionStartTime + BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY; // every 60 seconds we do nasty things
            // now find all vessels with weapons managers
            foreach (var pilot in getAllPilots())
            {
                // put these in the scoring dictionary - these are the active participants
                Scores[pilot.vessel.GetName()] = new ScoringData
                {
                    vesselRef = pilot.vessel,
                    weaponManagerRef = pilot.weaponManager,
                    lastFiredTime = Planetarium.GetUniversalTime(),
                    previousPartCount = pilot.vessel.parts.Count,
                    team = pilot.weaponManager.Team.Name
                };
            }
            if (VesselSpawner.Instance.originalTeams.Count == 0) VesselSpawner.Instance.SaveTeams(); // If the vessels weren't spawned in with Vessel Spawner, save the current teams.
        }

        IEnumerator DogfightCompetitionModeRoutine(float distance)
        {
            competitionStarting = true;
            startTag = true; // Tag entry condition, should be true even if tag is not currently enabled, so if tag is enabled later in the competition it will function
            competitionStatus.Set("Competition: Pilots are taking off.");
            var pilots = new Dictionary<BDTeam, List<IBDAIControl>>();
            HashSet<IBDAIControl> readyToLaunch = new HashSet<IBDAIControl>();
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;

                    if (!pilots.TryGetValue(pilot.weaponManager.Team, out List<IBDAIControl> teamPilots))
                    {
                        teamPilots = new List<IBDAIControl>();
                        pilots.Add(pilot.weaponManager.Team, teamPilots);
                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Adding Team " + pilot.weaponManager.Team.Name);
                    }
                    teamPilots.Add(pilot);
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Adding Pilot " + pilot.vessel.GetName());
                    readyToLaunch.Add(pilot);
                }

            foreach (var pilot in readyToLaunch)
            {
                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[10]); // Modular Missiles use lower AGs (1-3) for staging, use a high AG number to not affect them
                pilot.ActivatePilot();
                pilot.CommandTakeOff();
                if (pilot.weaponManager.guardMode)
                {
                    pilot.weaponManager.ToggleGuardMode();
                }
                if (!pilot.vessel.FindPartModulesImplementing<ModuleEngines>().Any(engine => engine.EngineIgnited)) // Find vessels that didn't activate their engines on AG10 and fire their next stage.
                {
                    Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: " + pilot.vessel.vesselName + " didn't activate engines on AG10! Activating ALL their engines.");
                    foreach (var engine in pilot.vessel.FindPartModulesImplementing<ModuleEngines>())
                        engine.Activate();
                }
            }

            //clear target database so pilots don't attack yet
            BDATargetManager.ClearDatabase();

            foreach (var vname in Scores.Keys)
            {
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Adding Score Tracker For " + vname);
            }

            if (pilots.Count < 2)
            {
                Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: Unable to start competition mode - one or more teams is empty");
                competitionStatus.Set("Competition: Failed!  One or more teams is empty.");
                competitionStartFailureReason = CompetitionStartFailureReason.OnlyOneTeam;
                StopCompetition();
                yield break;
            }

            var leaders = new List<IBDAIControl>();
            using (var pilotList = pilots.GetEnumerator())
                while (pilotList.MoveNext())
                {
                    if (pilotList.Current.Value == null)
                    {
                        competitionStatus.Set("Competition: Teams got adjusted during competition start-up, aborting.");
                        competitionStartFailureReason = CompetitionStartFailureReason.OnlyOneTeam;
                        StopCompetition();
                        yield break;
                    }
                    leaders.Add(pilotList.Current.Value[0]);
                }
            while (leaders.Any(leader => leader == null || leader.weaponManager == null || leader.weaponManager.wingCommander == null || leader.weaponManager.wingCommander.weaponManager == null))
            {
                yield return new WaitForFixedUpdate();
                if (leaders.Any(leader => leader == null || leader.weaponManager == null))
                {
                    competitionStatus.Set("Competition: One of the team leaders disappeared during start-up, aborting.");
                    competitionStartFailureReason = CompetitionStartFailureReason.TeamLeaderDisappeared;
                    StopCompetition();
                    yield break;
                }
            }
            foreach (var leader in leaders)
                leader.weaponManager.wingCommander.CommandAllFollow();

            //wait till the leaders are ready to engage (airborne for PilotAI)
            bool ready = false;
            while (!ready)
            {
                ready = true;
                using (var leader = leaders.GetEnumerator())
                    while (leader.MoveNext())
                        if (leader.Current != null && !leader.Current.CanEngage())
                        {
                            ready = false;
                            yield return new WaitForSeconds(1);
                            break;
                        }
            }

            using (var leader = leaders.GetEnumerator())
                while (leader.MoveNext())
                    if (leader.Current == null)
                    {
                        competitionStatus.Set("Competition: A leader vessel has disappeared during competition start-up, aborting.");
                        competitionStartFailureReason = CompetitionStartFailureReason.TeamLeaderDisappeared;
                        StopCompetition();
                        yield break;
                    }

            competitionStatus.Set("Competition: Sending pilots to start position.");
            Vector3 center = Vector3.zero;
            using (var leader = leaders.GetEnumerator())
                while (leader.MoveNext())
                    center += leader.Current.vessel.CoM;
            center /= leaders.Count;
            Vector3 startDirection = Vector3.ProjectOnPlane(leaders[0].vessel.CoM - center, VectorUtils.GetUpDirection(center)).normalized;
            startDirection *= (distance * leaders.Count / 4) + 1250f;
            Quaternion directionStep = Quaternion.AngleAxis(360f / leaders.Count, VectorUtils.GetUpDirection(center));

            for (var i = 0; i < leaders.Count; ++i)
            {
                leaders[i].CommandFlyTo(VectorUtils.WorldPositionToGeoCoords(startDirection, FlightGlobals.currentMainBody));
                startDirection = directionStep * startDirection;
            }

            Vector3 centerGPS = VectorUtils.WorldPositionToGeoCoords(center, FlightGlobals.currentMainBody);

            //wait till everyone is in position
            competitionStatus.Set("Competition: Waiting for teams to get in position.");
            bool waiting = true;
            var sqrDistance = distance * distance;
            while (waiting && !startCompetitionNow)
            {
                waiting = false;

                foreach (var leader in leaders)
                    if (leader == null)
                    {
                        competitionStatus.Set("Competition: A leader vessel has disappeared during competition start-up, aborting.");
                        competitionStartFailureReason = CompetitionStartFailureReason.TeamLeaderDisappeared;
                        StopCompetition(); // A yield has occurred, check that the leaders list hasn't changed in the meantime.
                        yield break;
                    }


                using (var leader = leaders.GetEnumerator())
                    while (leader.MoveNext())
                    {
                        using (var otherLeader = leaders.GetEnumerator())
                            while (otherLeader.MoveNext())
                            {
                                if (leader.Current == otherLeader.Current)
                                    continue;
                                try // Somehow, if a vessel gets destroyed during competition start, the following can throw a null reference exception despite checking for nulls!
                                {
                                    if ((leader.Current.transform.position - otherLeader.Current.transform.position).sqrMagnitude < sqrDistance)
                                        waiting = true;
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning("[BDArmory.BDACompetitionMode]: Exception thrown in DogfightCompetitionModeRoutine: " + e.Message + "\n" + e.StackTrace);
                                    competitionStatus.Set("Competition: A leader vessel has disappeared during competition start-up, aborting.");
                                    competitionStartFailureReason = CompetitionStartFailureReason.TeamLeaderDisappeared;
                                    StopCompetition(); // A yield has occurred, check that the leaders list hasn't changed in the meantime.
                                    yield break;
                                }
                            }

                        // Increase the distance for large teams
                        if (!pilots.ContainsKey(leader.Current.weaponManager.Team))
                        {
                            competitionStatus.Set("Competition: The teams were changed during competition start-up, aborting.");
                            competitionStartFailureReason = CompetitionStartFailureReason.TeamsChanged;
                            StopCompetition();
                            yield break;
                        }
                        var teamDistance = 800f + 100f * pilots[leader.Current.weaponManager.Team].Count;
                        using (var pilot = pilots[leader.Current.weaponManager.Team].GetEnumerator())
                            while (pilot.MoveNext())
                                if (pilot.Current != null
                                        && pilot.Current.currentCommand == PilotCommands.Follow
                                        && (pilot.Current.vessel.CoM - pilot.Current.commandLeader.vessel.CoM).sqrMagnitude > teamDistance * teamDistance)
                                    waiting = true;

                        if (waiting) break;
                    }

                yield return null;
            }
            previousNumberCompetitive = 2; // For entering into tag mode

            //start the match
            foreach (var teamPilots in pilots.Values)
            {
                if (teamPilots == null)
                {
                    competitionStatus.Set("Competition: Teams have been changed during competition start-up, aborting.");
                    competitionStartFailureReason = CompetitionStartFailureReason.TeamsChanged;
                    StopCompetition();
                    yield break;
                }
                foreach (var pilot in teamPilots)
                    if (pilot == null)
                    {
                        competitionStatus.Set("Competition: A pilot has disappeared from team during competition start-up, aborting.");
                        competitionStartFailureReason = CompetitionStartFailureReason.PilotDisappeared;
                        StopCompetition(); // Check that the team pilots haven't been changed during the competition startup.
                        yield break;
                    }
            }
            RadarUtils.ForceUpdateRadarCrossSections(); // Update RCS
            using (var teamPilots = pilots.GetEnumerator())
                while (teamPilots.MoveNext())
                    using (var pilot = teamPilots.Current.Value.GetEnumerator())
                        while (pilot.MoveNext())
                        {
                            if (pilot.Current == null) continue;

                            if (!pilot.Current.weaponManager.guardMode)
                                pilot.Current.weaponManager.ToggleGuardMode();

                            using (var leader = leaders.GetEnumerator())
                                while (leader.MoveNext())
                                    BDATargetManager.ReportVessel(pilot.Current.vessel, leader.Current.weaponManager);

                            pilot.Current.ReleaseCommand();
                            pilot.Current.CommandAttack(centerGPS);
                            pilot.Current.vessel.altimeterDisplayState = AltimeterDisplayState.AGL;
                        }

            competitionStatus.Add("Competition starting!  Good luck!");
            CompetitionStarted();
        }
        #endregion

        HashSet<string> uniqueVesselNames = new HashSet<string>();
        private List<IBDAIControl> getAllPilots()
        {
            var pilots = new List<IBDAIControl>();
            uniqueVesselNames.Clear();
            foreach (var vessel in BDATargetManager.LoadedVessels)
            {
                if (vessel == null || !vessel.loaded) continue;
                if (IsValidVessel(vessel) != InvalidVesselReason.None) continue;
                var pilot = vessel.FindPartModuleImplementing<IBDAIControl>();
                if (pilot.weaponManager.Team.Neutral) continue; // Ignore the neutrals.
                pilots.Add(pilot);
                if (uniqueVesselNames.Contains(vessel.vesselName))
                {
                    var count = 1;
                    var potentialName = vessel.vesselName + "_" + count;
                    while (uniqueVesselNames.Contains(potentialName))
                        potentialName = vessel.vesselName + "_" + (++count);
                    vessel.vesselName = potentialName;
                }
                uniqueVesselNames.Add(vessel.vesselName);
            }
            return pilots;
        }

        #region Vessel validity
        public enum InvalidVesselReason { None, NullVessel, NoAI, NoWeaponManager, NoCommand };
        public InvalidVesselReason IsValidVessel(Vessel vessel, bool attemptFix = true)
        {
            if (vessel == null)
                return InvalidVesselReason.NullVessel;
            var pilot = vessel.FindPartModuleImplementing<IBDAIControl>();
            if (pilot == null) // Check for an AI.
                return InvalidVesselReason.NoAI;
            if (vessel.FindPartModuleImplementing<MissileFire>() == null) // Check for a weapon manager.
                return InvalidVesselReason.NoWeaponManager;
            if (attemptFix && vessel.FindPartModuleImplementing<ModuleCommand>() == null && vessel.FindPartModuleImplementing<KerbalSeat>() == null) // Check for a cockpit or command seat.
                CheckVesselType(vessel); // Attempt to fix it.
            if (vessel.FindPartModuleImplementing<ModuleCommand>() == null && vessel.FindPartModuleImplementing<KerbalSeat>() == null) // Check for a cockpit or command seat.
                return InvalidVesselReason.NoCommand;
            return InvalidVesselReason.None;
        }

        public void OnVesselModified(Vessel vessel)
        {
            CheckVesselType(vessel);
            if (!BDArmorySettings.AUTONOMOUS_COMBAT_SEATS) CheckForAutonomousCombatSeat(vessel);
            if (BDArmorySettings.DESTROY_UNCONTROLLED_WMS) CheckForUncontrolledVessel(vessel);
        }

        HashSet<VesselType> validVesselTypes = new HashSet<VesselType> { VesselType.Plane, VesselType.Ship };
        public void CheckVesselType(Vessel vessel)
        {
            if (!BDArmorySettings.RUNWAY_PROJECT) return;
            if (vessel != null && vessel.vesselName != null)
            {
                var vesselTypeIsValid = validVesselTypes.Contains(vessel.vesselType);
                var hasMissileFire = vessel.FindPartModuleImplementing<MissileFire>() != null;
                if (!vesselTypeIsValid && hasMissileFire) // Found an invalid vessel type with a weapon manager.
                {
                    var message = "Found weapon manager on " + vessel.vesselName + " of type " + vessel.vesselType;
                    if (vessel.vesselName.EndsWith(" " + vessel.vesselType.ToString()))
                        vessel.vesselName = vessel.vesselName.Remove(vessel.vesselName.Length - vessel.vesselType.ToString().Length - 1);
                    vessel.vesselType = VesselType.Plane;
                    message += ", changing vessel name and type to " + vessel.vesselName + ", " + vessel.vesselType;
                    Debug.Log("[BDArmory.BDACompetitionMode]: " + message);
                    return;
                }
                if (vesselTypeIsValid && vessel.vesselType == VesselType.Plane && vessel.vesselName.EndsWith(" Plane") && !Scores.ContainsKey(vessel.vesselName) && Scores.ContainsKey(vessel.vesselName.Remove(vessel.vesselName.Length - 6)) && IsValidVessel(vessel, false) == InvalidVesselReason.None)
                {
                    var message = "Found a valid vessel (" + vessel.vesselName + ") tagged with 'Plane' when it shouldn't be, renaming.";
                    Debug.Log("[BDArmory.BDACompetitionMode]: " + message);
                    vessel.vesselName = vessel.vesselName.Remove(vessel.vesselName.Length - 6);
                    return;
                }
            }
        }

        public void CheckForAutonomousCombatSeat(Vessel vessel)
        {
            if (vessel == null) return;
            var kerbalSeat = vessel.FindPartModuleImplementing<KerbalSeat>();
            if (kerbalSeat != null)
            {
                if (vessel.parts.Count == 1) // Check for a falling combat seat.
                {
                    Debug.Log("[BDArmory.BDACompetitionMode]: Found a lone combat seat, killing it.");
                    PartExploderSystem.AddPartToExplode(vessel.parts[0]);
                    return;
                }
                // Check for a lack of control.
                var kerbalEVA = vessel.FindPartModuleImplementing<KerbalEVA>();
                var AI = vessel.FindPartModuleImplementing<BDModulePilotAI>();
                if (kerbalEVA == null && AI != null && AI.pilotEnabled) // If not controlled by a kerbalEVA in a KerbalSeat, check the regular ModuleCommand parts.
                {
                    var commandModules = vessel.FindPartModulesImplementing<ModuleCommand>();
                    if (commandModules.All(c => c.GetControlSourceState() == CommNet.VesselControlState.None))
                    {
                        Debug.Log("[BDArmory.BDACompetitionMode]: Kerbal has left the seat of " + vessel.vesselName + " and it has no other controls, disabling the AI.");
                        AI.DeactivatePilot();
                    }
                }
            }
        }

        void CheckForUncontrolledVessel(Vessel vessel)
        {
            if (vessel == null || vessel.vesselName == null) return;
            if (vessel.FindPartModuleImplementing<Kerbal>() != null) return; // Check for Kerbals on the inside.
            if (vessel.FindPartModuleImplementing<KerbalEVA>() != null) return; // Check for Kerbals on the outside.
            // Check for drones
            var commandModules = vessel.FindPartModulesImplementing<ModuleCommand>();
            var craftbricked = vessel.FindPartModuleImplementing<ModuleDrainEC>();
            if (commandModules.All(c => c.GetControlSourceState() == CommNet.VesselControlState.None))
                if (Scores.ContainsKey(vessel.vesselName) && Scores[vessel.vesselName].weaponManagerRef != null)
                    StartCoroutine(DelayedExplodeWM(Scores[vessel.vesselName].weaponManagerRef, 5f)); // Uncontrolled vessel, destroy its weapon manager in 5s.
            if (craftbricked != null && craftbricked.bricked)
            {
                if (Scores.ContainsKey(vessel.vesselName) && Scores[vessel.vesselName].weaponManagerRef != null)
                    StartCoroutine(DelayedExplodeWM(Scores[vessel.vesselName].weaponManagerRef, 2f)); // vessel fried by EMP, destroy its weapon manager in 2s.
            }
        }

        IEnumerator DelayedExplodeWM(MissileFire weaponManager, float delay = 1f)
        {
            yield return new WaitForSeconds(delay);
            if (weaponManager != null)
            {
                Debug.Log("[BDArmory.BDACompetitionMode]: " + weaponManager.vessel.vesselName + " has no form of control, killing the weapon manager.");
                PartExploderSystem.AddPartToExplode(weaponManager.part);
            }
        }

        void CheckForBadlyNamedVessels()
        {
            foreach (var wm in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).Where(wm => wm != null).ToList())
                if (wm != null && wm.vessel != null && wm.vessel.vesselName != null)
                {
                    if (wm.vessel.vesselType == VesselType.Plane && wm.vessel.vesselName.EndsWith(" Plane") && !Scores.ContainsKey(wm.vessel.vesselName) && Scores.ContainsKey(wm.vessel.vesselName.Remove(wm.vessel.vesselName.Length - 6)) && IsValidVessel(wm.vessel) == InvalidVesselReason.None)
                    {
                        var message = "Found a valid vessel (" + wm.vessel.vesselName + ") tagged with 'Plane' when it shouldn't be, renaming.";
                        Debug.Log("[BDArmory.BDACompetitionMode]: " + message);
                        wm.vessel.vesselName = wm.vessel.vesselName.Remove(wm.vessel.vesselName.Length - 6);
                    }
                }
        }
        #endregion

        #region Runway Project
        public bool killerGMenabled = false;
        public bool pinataAlive = false;

        public void StartRapidDeployment(float distance)
        {
            if (!BDArmorySettings.RUNWAY_PROJECT) return;
            if (!sequencedCompetitionStarting)
            {
                ResetCompetitionStuff();
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Starting Rapid Deployment ");
                RemoveDebrisNow();
                GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
                GameEvents.onVesselCreate.Add(OnVesselModified);
                if (BDArmorySettings.AUTO_ENABLE_VESSEL_SWITCHING)
                    LoadedVesselSwitcher.Instance.EnableAutoVesselSwitching(true);
                if (BDArmorySettings.KERBAL_SAFETY > 0)
                    KerbalSafetyManager.Instance.CheckAllVesselsForKerbals();
                List<string> commandSequence;
                switch (BDArmorySettings.RUNWAY_PROJECT_ROUND)
                {
                    case 33:
                        commandSequence = new List<string>{
                            "0:MassTrim", // t=0, mass trim
                            "0:ActionGroup:14:0", // t=0, Disable brakes
                            "0:ActionGroup:4", // t=0, AG4 - Launch: Activate base craft engine, retract airbrakes
                            "0:ActionGroup:13:1", // t=0, AG4 - Enable SAS
                            "0:SetThrottle:100", // t=0, Full throttle
                            "35:ActionGroup:1", // t=35, AG1 - Engine shutdown, extend airbrakes
                            "10:ActionGroup:2", // t=45, AG2 - Deploy fairing
                            "3:RemoveFairings", // t=48, Remove fairings from the game
                            "0:ActionGroup:3", // t=48, AG3 - Decouple base craft (-> add your custom engine activations and timers here <-)
                            "0:ActionGroup:12:1", // t=48, Enable RCS
                            "0:ActivateEngines", // t=48, Activate engines (if they're not activated by AG3)
                            "1:TogglePilot:1", // t=49, Activate pilots
                            "0:ActionGroup:16:0", // t=55, Retract gear (if it's not retracted)
                            "6:ToggleGuard:1", // t=55, Activate guard mode (attack)
                            "5:RemoveDebris", // t=60, Remove any other debris and spectators
                            // "0:EnableGM", // t=60, Activate the killer GM
                        };
                        break;
                    default: // Same as S3R3 for now, until we do something different.
                        commandSequence = new List<string>{
                            "0:MassTrim", // t=0, mass trim
                            "0:ActionGroup:14:0", // t=0, Disable brakes
                            "0:ActionGroup:4", // t=0, AG4 - Launch: Activate base craft engine, retract airbrakes
                            "0:ActionGroup:13:1", // t=0, AG4 - Enable SAS
                            "0:SetThrottle:100", // t=0, Full throttle
                            "35:ActionGroup:1", // t=35, AG1 - Engine shutdown, extend airbrakes
                            "10:ActionGroup:2", // t=45, AG2 - Deploy fairing
                            "3:RemoveFairings", // t=48, Remove fairings from the game
                            "0:ActionGroup:3", // t=48, AG3 - Decouple base craft (-> add your custom engine activations and timers here <-)
                            "0:ActionGroup:12:1", // t=48, Enable RCS
                            "0:ActivateEngines", // t=48, Activate engines (if they're not activated by AG3)
                            "1:TogglePilot:1", // t=49, Activate pilots
                            "0:ActionGroup:16:0", // t=55, Retract gear (if it's not retracted)
                            "6:ToggleGuard:1", // t=55, Activate guard mode (attack)
                            "5:RemoveDebris", // t=60, Remove any other debris and spectators
                            // "0:EnableGM", // t=60, Activate the killer GM
                        };
                        break;
                }
                competitionRoutine = StartCoroutine(SequencedCompetition(commandSequence));
            }
        }

        private void DoPreflightChecks()
        {
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                var pilots = getAllPilots();
                foreach (var pilot in pilots)
                {
                    if (pilot.vessel == null) continue;

                    enforcePartCount(pilot.vessel);
                }
            }
        }
        // "JetEngine", "miniJetEngine", "turboFanEngine", "turboJet", "turboFanSize2", "RAPIER"
        static string[] allowedEngineList = { "JetEngine", "miniJetEngine", "turboFanEngine", "turboJet", "turboFanSize2", "RAPIER" };
        static HashSet<string> allowedEngines = new HashSet<string>(allowedEngineList);

        // allow duplicate landing gear
        static string[] allowedDuplicateList = { "GearLarge", "GearFixed", "GearFree", "GearMedium", "GearSmall", "SmallGearBay", "fuelLine", "strutConnector" };
        static HashSet<string> allowedLandingGear = new HashSet<string>(allowedDuplicateList);

        // don't allow "SaturnAL31"
        static string[] bannedPartList = { "SaturnAL31" };
        static HashSet<string> bannedParts = new HashSet<string>(bannedPartList);

        // ammo boxes
        static string[] ammoPartList = { "baha20mmAmmo", "baha30mmAmmo", "baha50CalAmmo", "BDAcUniversalAmmoBox", "UniversalAmmoBoxBDA" };
        static HashSet<string> ammoParts = new HashSet<string>(ammoPartList);

        public void enforcePartCount(Vessel vessel)
        {
            if (!BDArmorySettings.RUNWAY_PROJECT) return;
            switch (BDArmorySettings.RUNWAY_PROJECT_ROUND)
            {
                case 18:
                    break;
                default:
                    return;
            }
            using (List<Part>.Enumerator parts = vessel.parts.GetEnumerator())
            {
                Dictionary<string, int> partCounts = new Dictionary<string, int>();
                List<Part> partsToKill = new List<Part>();
                List<Part> ammoBoxes = new List<Part>();
                int engineCount = 0;
                while (parts.MoveNext())
                {
                    if (parts.Current == null) continue;
                    var partName = parts.Current.name;
                    if (partCounts.ContainsKey(partName))
                    {
                        partCounts[partName]++;
                    }
                    else
                    {
                        partCounts[partName] = 1;
                    }
                    if (allowedEngines.Contains(partName))
                    {
                        engineCount++;
                    }
                    if (bannedParts.Contains(partName))
                    {
                        partsToKill.Add(parts.Current);
                    }
                    if (allowedLandingGear.Contains(partName))
                    {
                        // duplicates allowed
                        continue;
                    }
                    if (ammoParts.Contains(partName))
                    {
                        // can only figure out limits after counting engines.
                        ammoBoxes.Add(parts.Current);
                        continue;
                    }
                    if (partCounts[partName] > 1)
                    {
                        partsToKill.Add(parts.Current);
                    }
                }
                if (engineCount == 0)
                {
                    engineCount = 1;
                }

                while (ammoBoxes.Count > engineCount * 3)
                {
                    partsToKill.Add(ammoBoxes[ammoBoxes.Count - 1]);
                    ammoBoxes.RemoveAt(ammoBoxes.Count - 1);
                }
                if (partsToKill.Count > 0)
                {
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] Vessel Breaking Part Count Rules " + vessel.GetName());
                    foreach (var part in partsToKill)
                    {
                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] KILLPART:" + part.name + ":" + vessel.GetName());
                        PartExploderSystem.AddPartToExplode(part);
                    }
                }
            }
        }

        private void DoRapidDeploymentMassTrim(float targetMass = 65f)
        {
            // in rapid deployment this verified masses etc. 
            var oreID = PartResourceLibrary.Instance.GetDefinition("Ore").id;
            var pilots = getAllPilots();
            var lowestMass = float.MaxValue;
            var highestMass = targetMass; // Trim to highest mass or target mass, whichever is higher.
            foreach (var pilot in pilots)
            {

                if (pilot.vessel == null) continue;

                var notShieldedCount = 0;
                using (List<Part>.Enumerator parts = pilot.vessel.parts.GetEnumerator())
                {
                    while (parts.MoveNext())
                    {
                        if (parts.Current == null) continue;
                        // count the unshielded parts
                        if (!parts.Current.ShieldedFromAirstream)
                        {
                            notShieldedCount++;
                        }
                        // Empty the ore tank and set the fuel tanks to the correct amount.
                        using (IEnumerator<PartResource> resources = parts.Current.Resources.GetEnumerator())
                            while (resources.MoveNext())
                            {
                                if (resources.Current == null) continue;

                                if (resources.Current.resourceName == "Ore")
                                {
                                    if (resources.Current.maxAmount == 1500)
                                    {
                                        resources.Current.amount = 0;
                                    }
                                }
                                else if (resources.Current.resourceName == "LiquidFuel")
                                {
                                    if (resources.Current.maxAmount == 3240)
                                    {
                                        resources.Current.amount = 2160;
                                    }
                                }
                                else if (resources.Current.resourceName == "Oxidizer")
                                {
                                    if (resources.Current.maxAmount == 3960)
                                    {
                                        resources.Current.amount = 2640;
                                    }
                                }
                            }
                    }
                }
                var mass = pilot.vessel.GetTotalMass();

                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: UNSHIELDED:" + notShieldedCount.ToString() + ":" + pilot.vessel.GetName());
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: MASS:" + mass.ToString() + ":" + pilot.vessel.GetName());
                if (mass < lowestMass)
                {
                    lowestMass = mass;
                }
                if (mass > highestMass)
                {
                    highestMass = mass;
                }
            }

            foreach (var pilot in pilots)
            {
                if (pilot.vessel == null) continue;
                var mass = pilot.vessel.GetTotalMass();
                var extraMass = highestMass - mass;
                using (List<Part>.Enumerator parts = pilot.vessel.parts.GetEnumerator())
                    while (parts.MoveNext())
                    {
                        bool massAdded = false;
                        if (parts.Current == null) continue;
                        using (IEnumerator<PartResource> resources = parts.Current.Resources.GetEnumerator())
                            while (resources.MoveNext())
                            {
                                if (resources.Current == null) continue;
                                if (resources.Current.resourceName == "Ore")
                                {
                                    // oreMass = 10;
                                    // ore to add = difference / 10;
                                    if (resources.Current.maxAmount == 1500)
                                    {
                                        var oreAmount = extraMass / 0.01; // 10kg per unit of ore
                                        if (oreAmount > 1500) oreAmount = 1500;
                                        resources.Current.amount = oreAmount;
                                    }
                                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: RESOURCEUPDATE:" + pilot.vessel.GetName() + ":" + resources.Current.amount);
                                    massAdded = true;
                                }
                            }
                        if (massAdded) break;
                    }
            }
        }

        IEnumerator SequencedCompetition(List<string> commandSequence)
        {
            var pilots = getAllPilots();
            if (pilots.Count < 2)
            {
                Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: Unable to start sequenced competition - one or more teams is empty");
                competitionStatus.Set("Competition: Failed!  One or more teams is empty.");
                competitionStartFailureReason = CompetitionStartFailureReason.OnlyOneTeam;
                StopCompetition();
                yield break;
            }
            sequencedCompetitionStarting = true;
            double startTime = Planetarium.GetUniversalTime();
            double nextStep = startTime;

            foreach (var cmdEvent in commandSequence)
            {
                // parse the event
                competitionStatus.Add(cmdEvent);
                var parts = cmdEvent.Split(':');
                if (parts.Count() == 1)
                {
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Competition Command not parsed correctly " + cmdEvent);
                    StopCompetition();
                    yield break;
                }
                var timeStep = int.Parse(parts[0]);
                nextStep = Planetarium.GetUniversalTime() + timeStep;
                while (Planetarium.GetUniversalTime() < nextStep)
                {
                    yield return new WaitForFixedUpdate();
                }

                var command = parts[1];

                switch (command)
                {
                    case "Stage":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Staging.");
                            // activate stage
                            foreach (var pilot in pilots)
                            {
                                Misc.Misc.fireNextNonEmptyStage(pilot.vessel);
                            }
                            break;
                        }
                    case "ActionGroup":
                        {
                            if (parts.Count() < 3 || parts.Count() > 4)
                            {
                                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Competition Command not parsed correctly " + cmdEvent);
                                StopCompetition();
                                yield break;
                            }
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Jiggling action group " + parts[2] + ".");
                            foreach (var pilot in pilots)
                            {
                                if (parts.Count() == 3)
                                {
                                    pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[int.Parse(parts[2])]);
                                }
                                else if (parts.Count() == 4)
                                {
                                    bool state = false;
                                    if (parts[3] != "0")
                                    {
                                        state = true;
                                    }
                                    pilot.vessel.ActionGroups.SetGroup(KM_dictAG[int.Parse(parts[2])], state);
                                }
                            }
                            break;
                        }
                    case "TogglePilot":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Toggling autopilot.");
                            if (parts.Count() == 3)
                            {
                                var newState = true;
                                if (parts[2] == "0")
                                {
                                    newState = false;
                                }
                                foreach (var pilot in pilots)
                                {
                                    if (newState != pilot.pilotEnabled)
                                        pilot.TogglePilot();
                                }
                                if (newState)
                                    competitionStarting = true;
                            }
                            else
                            {
                                foreach (var pilot in pilots)
                                {
                                    pilot.TogglePilot();
                                }
                            }
                            break;
                        }
                    case "ToggleGuard":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Toggling guard mode.");
                            if (parts.Count() == 3)
                            {
                                var newState = true;
                                if (parts[2] == "0")
                                {
                                    newState = false;
                                }
                                foreach (var pilot in pilots)
                                {
                                    if (pilot.weaponManager != null && pilot.weaponManager.guardMode != newState)
                                        pilot.weaponManager.ToggleGuardMode();
                                }
                            }
                            else
                            {
                                foreach (var pilot in pilots)
                                {
                                    if (pilot.weaponManager != null) pilot.weaponManager.ToggleGuardMode();
                                }
                            }
                            break;
                        }
                    case "SetThrottle":
                        {
                            if (parts.Count() == 3)
                            {
                                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Adjusting throttle to " + parts[2] + "%.");
                                var someOtherVessel = pilots[0].vessel == FlightGlobals.ActiveVessel ? pilots[1].vessel : pilots[0].vessel;
                                foreach (var pilot in pilots)
                                {
                                    bool currentVesselIsActive = pilot.vessel == FlightGlobals.ActiveVessel;
                                    if (currentVesselIsActive) LoadedVesselSwitcher.Instance.ForceSwitchVessel(someOtherVessel); // Temporarily switch away so that the throttle change works.
                                    var throttle = int.Parse(parts[2]) * 0.01f;
                                    pilot.vessel.ctrlState.killRot = true;
                                    pilot.vessel.ctrlState.mainThrottle = throttle;
                                    if (currentVesselIsActive) LoadedVesselSwitcher.Instance.ForceSwitchVessel(pilot.vessel); // Switch back again.
                                }
                            }
                            break;
                        }
                    case "RemoveDebris":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Removing debris and non-competitors.");
                            // remove anything that doesn't contain BD Armory modules
                            RemoveNonCompetitors(true);
                            RemoveDebrisNow();
                            break;
                        }
                    case "RemoveFairings":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Removing fairings.");
                            // removes the fairings after deplyment to stop the physical objects consuming CPU
                            var rmObj = new List<physicalObject>();
                            foreach (var phyObj in FlightGlobals.physicalObjects)
                            {
                                if (phyObj.name == "FairingPanel") rmObj.Add(phyObj);
                            }
                            foreach (var phyObj in rmObj)
                            {
                                FlightGlobals.removePhysicalObject(phyObj);
                            }
                            break;
                        }
                    case "EnableGM":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Activating killer GM.");
                            killerGMenabled = true;
                            decisionTick = BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60 ? -1 : Planetarium.GetUniversalTime() + BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY;
                            ResetSpeeds();
                            break;
                        }
                    case "ActivateEngines":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Activating engines.");
                            foreach (var pilot in getAllPilots())
                            {
                                if (!pilot.vessel.FindPartModulesImplementing<ModuleEngines>().Any(engine => engine.EngineIgnited)) // If the vessel didn't activate their engines on AG3, then activate all their engines and hope for the best.
                                {
                                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + pilot.vessel.GetName() + " didn't activate engines on AG3! Activating ALL their engines.");
                                    foreach (var engine in pilot.vessel.FindPartModulesImplementing<ModuleEngines>())
                                        engine.Activate();
                                }
                            }
                            break;
                        }
                    case "MassTrim":
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Performing mass trim.");
                            DoRapidDeploymentMassTrim();
                            break;
                        }
                    default:
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Unknown sequenced command: " + command + ".");
                            StopCompetition();
                            yield break;
                        }
                }
            }
            // will need a terminator routine
            CompetitionStarted();
        }

        // ask the GM to find a 'victim' which means a slow pilot who's not shooting very much
        // obviosly this is evil. 
        // it's enabled by right clicking the M button.
        // I also had it hooked up to the death of the Pinata but that's disconnected right now
        private void FindVictim()
        {
            if (!BDArmorySettings.RUNWAY_PROJECT) return;
            if (decisionTick < 0) return;
            if (Planetarium.GetUniversalTime() < decisionTick) return;
            decisionTick = BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60 ? -1 : Planetarium.GetUniversalTime() + BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY;
            if (!killerGMenabled) return;
            if (Planetarium.GetUniversalTime() - competitionStartTime < BDArmorySettings.COMPETITION_KILLER_GM_GRACE_PERIOD) return;
            // arbitrary and capbricious decisions of life and death

            bool hasFired = true;
            Vessel worstVessel = null;
            double slowestSpeed = 100000;
            int vesselCount = 0;
            using (var loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator())
                while (loadedVessels.MoveNext())
                {
                    if (loadedVessels.Current == null || !loadedVessels.Current.loaded)
                        continue;
                    IBDAIControl pilot = loadedVessels.Current.FindPartModuleImplementing<IBDAIControl>();
                    if (pilot == null || !pilot.weaponManager || pilot.weaponManager.Team.Neutral)
                        continue;

                    var vesselName = loadedVessels.Current.GetName();
                    if (!Scores.ContainsKey(vesselName))
                        continue;

                    vesselCount++;
                    ScoringData vData = Scores[vesselName];

                    var averageSpeed = vData.AverageSpeed / vData.averageCount;
                    var averageAltitude = vData.AverageAltitude / vData.averageCount;
                    averageSpeed = averageAltitude + (averageSpeed * averageSpeed / 200); // kinetic & potential energy
                    if (pilot.weaponManager != null)
                    {
                        if (!pilot.weaponManager.guardMode) averageSpeed *= 0.5;
                    }

                    bool vesselNotFired = (Planetarium.GetUniversalTime() - vData.lastFiredTime) > 120; // if you can't shoot in 2 minutes you're at the front of line

                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Victim Check " + vesselName + " " + averageSpeed.ToString() + " " + vesselNotFired.ToString());
                    if (hasFired)
                    {
                        if (vesselNotFired)
                        {
                            // we found a vessel which hasn't fired
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                            hasFired = false;
                        }
                        else if (averageSpeed < slowestSpeed)
                        {
                            // this vessel fired but is slow
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                        }
                    }
                    else
                    {
                        if (vesselNotFired)
                        {
                            // this vessel was slow and hasn't fired
                            worstVessel = loadedVessels.Current;
                            slowestSpeed = averageSpeed;
                        }
                    }
                }
            // if we have 3 or more vessels kill the slowest
            if (vesselCount > 2 && worstVessel != null)
            {
                var vesselName = worstVessel.GetName();
                if (Scores.ContainsKey(vesselName))
                {
                    Scores[vesselName].lastPersonWhoHitMe = "GM";
                    Scores[vesselName].gmKillReason = GMKillReason.GM; // Indicate that it was us who killed it.
                }
                competitionStatus.Add(vesselName + " was killed by the GM for being too slow.");
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: GM killing " + vesselName + " for being too slow.");
                Misc.Misc.ForceDeadVessel(worstVessel);
            }
            ResetSpeeds();
        }

        private void CheckAltitudeLimits()
        {
            if (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH < 46f) // Kill off those flying too high.
            {
                var limit = (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH < 10f ? BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH / 10f : BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH < 30f ? BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH - 9f : (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_HIGH - 29f) * 5f + 20f) * 1000f;
                foreach (var weaponManager in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList())
                {
                    if (alive.Contains(weaponManager.vessel.vesselName) && weaponManager.vessel.altitude > limit)
                    {
                        Scores[weaponManager.vessel.vesselName].gmKillReason = GMKillReason.GM;
                        var killerName = Scores[weaponManager.vessel.vesselName].lastPersonWhoHitMe;
                        if (killerName == "")
                        {
                            killerName = "Flew too high!";
                            Scores[weaponManager.vessel.vesselName].lastPersonWhoHitMe = killerName;
                        }
                        competitionStatus.Add(weaponManager.vessel.vesselName + " flew too high!");
                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + weaponManager.vessel.vesselName + ":REMOVED:" + killerName);
                        if (KillTimer.ContainsKey(weaponManager.vessel.vesselName)) KillTimer.Remove(weaponManager.vessel.vesselName);
                        Misc.Misc.ForceDeadVessel(weaponManager.vessel);
                    }
                }
            }
            if (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW > -1) // Kill off those flying too low.
            {
                var limit = (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW < 10f ? BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW / 10f : BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW < 30f ? BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW - 9f : (BDArmorySettings.COMPETITION_ALTITUDE_LIMIT_LOW - 29f) * 5f + 20f) * 1000f;
                foreach (var weaponManager in LoadedVesselSwitcher.Instance.WeaponManagers.SelectMany(tm => tm.Value).ToList())
                {
                    if (alive.Contains(weaponManager.vessel.vesselName) && weaponManager.vessel.altitude < limit)
                    {
                        Scores[weaponManager.vessel.vesselName].gmKillReason = GMKillReason.GM;
                        var killerName = Scores[weaponManager.vessel.vesselName].lastPersonWhoHitMe;
                        if (killerName == "")
                        {
                            killerName = "Flew too low!";
                            Scores[weaponManager.vessel.vesselName].lastPersonWhoHitMe = killerName;
                        }
                        competitionStatus.Add(weaponManager.vessel.vesselName + " flew too low!");
                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + weaponManager.vessel.vesselName + ":REMOVED:" + killerName);
                        if (KillTimer.ContainsKey(weaponManager.vessel.vesselName)) KillTimer.Remove(weaponManager.vessel.vesselName);
                        Misc.Misc.ForceDeadVessel(weaponManager.vessel);
                    }
                }
            }
        }
        // reset all the tracked speeds, and copy the shot clock over, because I wanted 2 minutes of shooting to count
        private void ResetSpeeds()
        {
            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] resetting kill clock");
            foreach (var vname in Scores.Keys)
            {
                if (Scores[vname].averageCount == 0)
                {
                    Scores[vname].AverageAltitude = 0;
                    Scores[vname].AverageSpeed = 0;
                }
                else
                {
                    // ensures we always have a sensible value in here
                    Scores[vname].AverageAltitude /= Scores[vname].averageCount;
                    Scores[vname].AverageSpeed /= Scores[vname].averageCount;
                    Scores[vname].averageCount = 1;
                }
            }
        }

        #endregion

        #region Debris clean-up
        private HashSet<Vessel> nonCompetitorsToRemove = new HashSet<Vessel>();
        public void RemoveNonCompetitors(bool now = false)
        {
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel == null) continue;
                if (vessel.vesselType == VesselType.Debris) continue; // Handled by DebrisDelayedCleanUp.
                if (nonCompetitorsToRemove.Contains(vessel)) continue; // Already scheduled for removal.
                bool activePilot = false;
                if (BDArmorySettings.RUNWAY_PROJECT && vessel.GetName() == "Pinata")
                {
                    activePilot = true;
                }
                else
                {
                    int foundActiveParts = 0; // Note: this checks for exactly one of each part.
                    using (var wms = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator()) // Has a weapon manager
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }

                    using (var wms = vessel.FindPartModulesImplementing<IBDAIControl>().GetEnumerator()) // Has an AI
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }

                    using (var wms = vessel.FindPartModulesImplementing<ModuleCommand>().GetEnumerator()) // Has a command module
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                foundActiveParts++;
                                break;
                            }

                    if (foundActiveParts != 3)
                    {
                        using (var wms = vessel.FindPartModulesImplementing<KerbalSeat>().GetEnumerator()) // Command seats are ok
                            while (wms.MoveNext())
                                if (wms.Current != null)
                                {
                                    foundActiveParts++;
                                    break;
                                }
                    }
                    activePilot = foundActiveParts == 3;

                    using (var wms = vessel.FindPartModulesImplementing<MissileBase>().GetEnumerator()) // Allow missiles
                        while (wms.MoveNext())
                            if (wms.Current != null)
                            {
                                activePilot = true;
                                break;
                            }
                }
                if (!activePilot)
                {
                    nonCompetitorsToRemove.Add(vessel);
                    if (vessel.vesselType == VesselType.SpaceObject) // Deal with any new comets or asteroids that have appeared immediately.
                        StartCoroutine(DelayedVesselRemovalCoroutine(vessel, 0));
                    else
                        StartCoroutine(DelayedVesselRemovalCoroutine(vessel, now ? 0f : BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY));
                }
            }
        }

        public void RemoveDebrisNow()
        {
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel == null) continue;
                if (vessel.vesselType == VesselType.Debris) // Clean up any old debris.
                    StartCoroutine(DelayedVesselRemovalCoroutine(vessel, 0));
                if (vessel.vesselType == VesselType.SpaceObject) // Remove comets and asteroids to try to avoid null refs. (Still get null refs from comets, but it seems better with this than without it.)
                    StartCoroutine(DelayedVesselRemovalCoroutine(vessel, 0));
            }
        }

        HashSet<VesselType> debrisTypes = new HashSet<VesselType> { VesselType.Debris, VesselType.SpaceObject }; // Consider space objects as debris.
        void DebrisDelayedCleanUp(Vessel debris)
        {
            try
            {
                if (debris != null && debrisTypes.Contains(debris.vesselType))
                {
                    StartCoroutine(DelayedVesselRemovalCoroutine(debris, debris.vesselType == VesselType.SpaceObject ? 0 : BDArmorySettings.DEBRIS_CLEANUP_DELAY));
                }
            }
            catch (Exception e)
            {
                Debug.Log("DEBUG debris " + debris.vesselName + " is a component? " + (debris is Component) + ", is a monobehaviour? " + (debris is MonoBehaviour) + ". Exception: " + e.Message);
            }
        }

        void RemoveCometVessel(Vessel vessel)
        {
            if (vessel.vesselType == VesselType.SpaceObject)
            {
                Debug.Log("[BDArmory.BDACompetitionMode]: Found a newly spawned " + (vessel.FindVesselModuleImplementing<CometVessel>() != null ? "comet" : "asteroid") + " vessel! Removing it.");
                StartCoroutine(DelayedVesselRemovalCoroutine(vessel, 0f));
            }
        }

        private IEnumerator DelayedVesselRemovalCoroutine(Vessel vessel, float delay)
        {
            var vesselType = vessel.vesselType;
            yield return new WaitForSeconds(delay);
            if (vessel != null && debrisTypes.Contains(vesselType) && !debrisTypes.Contains(vessel.vesselType))
            {
                Debug.Log("[BDArmory.BDACompetitionMode]: Debris " + vessel.vesselName + " is no longer labelled as debris, not removing.");
                yield break;
            }
            if (vessel != null)
            {
                if (vessel.parts.Count == 1 && vessel.parts[0].IsKerbalEVA()) // The vessel is a kerbal on EVA. Ignore it for now.
                {
                    // KerbalSafetyManager.Instance.CheckForFallingKerbals(vessel);
                    if (nonCompetitorsToRemove.Contains(vessel)) nonCompetitorsToRemove.Remove(vessel);
                    yield break;
                    // var kerbalEVA = vessel.FindPartModuleImplementing<KerbalEVA>();
                    // if (kerbalEVA != null)
                    //     StartCoroutine(KerbalSafetyManager.Instance.RecoverWhenPossible(kerbalEVA));
                }
                else
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        Debug.Log("[BDArmory.BDACompetitionMode]: Removing " + vessel.GetName());
                    if (vessel.vesselType == VesselType.SpaceObject)
                    {
                        var cometVessel = vessel.FindVesselModuleImplementing<CometVessel>();
                        if (cometVessel) { Destroy(cometVessel); }
                    }
                    vessel.Die();
                    yield return new WaitForFixedUpdate();
                    if (vessel != null)
                    {
                        var partsToKill = vessel.parts.ToList();
                        foreach (var part in partsToKill)
                            if (part != null)
                                part.Die();
                    }
                }
            }
            if (nonCompetitorsToRemove.Contains(vessel))
            {
                Debug.Log("[BDArmory.BDACompetitionMode]: " + (vessel != null ? vessel.vesselName : null) + " removed.");
                nonCompetitorsToRemove.Remove(vessel);
            }
        }

        void DisableCometSpawning()
        {
            var cometManager = CometManager.Instance;
            if (!cometManager.isActiveAndEnabled) return;
            cometManager.spawnChance = new FloatCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) }); // Set the spawn chance to 0.
            foreach (var comet in cometManager.DiscoveredComets) // Clear known comets.
                RemoveCometVessel(comet);
            foreach (var comet in cometManager.Comets) // Clear all comets.
                RemoveCometVessel(comet);
        }
        #endregion

        // This is called every Time.fixedDeltaTime.
        void FixedUpdate()
        {
            if (competitionIsActive)
                LogRamming();
        }

        // the competition update system
        // cleans up dead vessels, tries to track kills (badly)
        // all of these are based on the vessel name which is probably sub optimal
        // This is triggered every Time.deltaTime.
        HashSet<Vessel> vesselsToKill = new HashSet<Vessel>();
        HashSet<string> alive = new HashSet<string>();
        public void DoUpdate()
        {
            // should be called every frame during flight scenes
            if (competitionStartTime < 0) return;
            if (competitionIsActive)
                competitionShouldBeRunning = true;
            if (competitionShouldBeRunning && !competitionIsActive)
            {
                Debug.Log("DEBUG Competition stopped unexpectedly!");
                competitionShouldBeRunning = false;
            }
            // Example usage of UpcomingCollisions(). Note that the timeToCPA values are only updated after an interval of half the current timeToCPA.
            // if (competitionIsActive)
            //     foreach (var upcomingCollision in UpcomingCollisions(100f).Take(3))
            //         Debug.Log("DEBUG Upcoming potential collision between " + upcomingCollision.Key.Item1 + " and " + upcomingCollision.Key.Item2 + " at distance " + Mathf.Sqrt(upcomingCollision.Value.Item1) + "m in " + upcomingCollision.Value.Item2 + "s.");
            var now = Planetarium.GetUniversalTime();
            if (now < nextUpdateTick)
                return;
            CheckForBadlyNamedVessels();
            double updateTickLength = BDArmorySettings.TAG_MODE ? 0.1 : BDArmorySettings.GRAVITY_HACKS ? 0.5 : 2;
            vesselsToKill.Clear();
            nextUpdateTick = nextUpdateTick + updateTickLength;
            int numberOfCompetitiveVessels = 0;
            alive.Clear();
            string doaUpdate = "ALIVE: ";
            // check all the planes
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel == null || !vessel.loaded) // || vessel.packed) // Allow packed craft to avoid the packed craft being considered dead (e.g., when command seats spawn).
                    continue;

                var mf = vessel.FindPartModuleImplementing<MissileFire>();

                if (mf != null)
                {
                    // things to check
                    // does it have fuel?
                    string vesselName = vessel.GetName();
                    ScoringData vData = null;
                    if (Scores.ContainsKey(vesselName))
                    {
                        vData = Scores[vesselName];
                    }

                    // this vessel really is alive
                    if ((vessel.vesselType != VesselType.Debris) && !vesselName.EndsWith("Debris")) // && !vesselName.EndsWith("Plane") && !vesselName.EndsWith("Probe"))
                    {
                        if (!VesselSpawner.Instance.vesselsSpawningContinuously && DeathOrder.ContainsKey(vesselName)) // This isn't an issue when continuous spawning is active.
                        {
                            Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: Dead vessel found alive " + vesselName);
                            //DeathOrder.Remove(vesselName);
                        }
                        // vessel is still alive
                        alive.Add(vesselName);
                        doaUpdate += " *" + vesselName + "* ";
                        numberOfCompetitiveVessels++;
                    }
                    pilotActions[vesselName] = "";

                    // try to create meaningful activity strings
                    if (mf.AI != null && mf.AI.currentStatus != null)
                    {
                        pilotActions[vesselName] = "";
                        if (mf.vessel.LandedOrSplashed)
                        {
                            if (mf.vessel.Landed)
                            {
                                pilotActions[vesselName] = " is landed";
                            }
                            else
                            {
                                pilotActions[vesselName] = " is splashed";
                            }
                        }
                        var activity = mf.AI.currentStatus;
                        if (activity == "Taking off")
                            pilotActions[vesselName] = " is taking off";
                        else if (activity == "Follow")
                        {
                            if (mf.AI.commandLeader != null && mf.AI.commandLeader.vessel != null)
                                pilotActions[vesselName] = " is following " + mf.AI.commandLeader.vessel.GetName();
                        }
                        else if (activity.StartsWith("Gain Alt"))
                            pilotActions[vesselName] = " is gaining altitude";
                        else if (activity.StartsWith("Terrain"))
                            pilotActions[vesselName] = " is avoiding terrain";
                        else if (activity == "Orbiting")
                            pilotActions[vesselName] = " is orbiting";
                        else if (activity == "Extending")
                            pilotActions[vesselName] = " is extending ";
                        else if (activity == "AvoidCollision")
                            pilotActions[vesselName] = " is avoiding collision";
                        else if (activity == "Evading")
                        {
                            if (mf.incomingThreatVessel != null)
                                pilotActions[vesselName] = " is evading " + mf.incomingThreatVessel.GetName();
                            else
                                pilotActions[vesselName] = " is taking evasive action";
                        }
                        else if (activity == "Attack")
                        {
                            if (mf.currentTarget != null && mf.currentTarget.name != null)
                                pilotActions[vesselName] = " is attacking " + mf.currentTarget.Vessel.GetName();
                            else
                                pilotActions[vesselName] = " is attacking";
                        }
                        else if (activity == "Ramming Speed!")
                        {
                            if (mf.currentTarget != null && mf.currentTarget.name != null)
                                pilotActions[vesselName] = " is trying to ram " + mf.currentTarget.Vessel.GetName();
                            else
                                pilotActions[vesselName] = " is in ramming speed";
                        }
                    }

                    // update the vessel scoring structure
                    if (vData != null)
                    {
                        var partCount = vessel.parts.Count();
                        if (BDArmorySettings.RUNWAY_PROJECT)
                        {
                            if (partCount != vData.previousPartCount)
                            {
                                // part count has changed, check for broken stuff
                                enforcePartCount(vessel);
                            }
                        }
                        if (vData.previousPartCount < vessel.parts.Count)
                            vData.lastLostPartTime = now;
                        vData.previousPartCount = vessel.parts.Count;

                        if (vessel.LandedOrSplashed)
                        {
                            if (!vData.landedState)
                            {
                                // was flying, is now landed
                                vData.lastLandedTime = now;
                                vData.landedState = true;
                                if (vData.landedKillTimer == 0)
                                {
                                    vData.landedKillTimer = now;
                                }
                            }
                        }
                        else
                        {
                            if (vData.landedState)
                            {
                                vData.lastLandedTime = now;
                                vData.landedState = false;
                            }
                            if (vData.landedKillTimer != 0)
                            {
                                // safely airborne for 15 seconds
                                if (now - vData.landedKillTimer > 15)
                                {
                                    vData.landedKillTimer = 0;
                                }
                            }
                        }
                    }

                    // after this point we're checking things that might result in kills.
                    if (now - competitionStartTime < BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD) continue;

                    // keep track if they're shooting for the GM
                    if (mf.currentGun != null)
                    {
                        if (mf.currentGun.recentlyFiring)
                        {
                            // keep track that this aircraft was shooting things
                            if (vData != null)
                            {
                                vData.lastFiredTime = now;
                            }
                            if (mf.currentTarget != null && mf.currentTarget.Vessel != null)
                            {
                                pilotActions[vesselName] = " is shooting at " + mf.currentTarget.Vessel.GetName();
                            }
                        }
                    }
                    // does it have ammunition: no ammo => Disable guard mode
                    if (!BDArmorySettings.INFINITE_AMMO)
                    {
                        if (mf.outOfAmmo && !outOfAmmo.Contains(vesselName)) // Report being out of weapons/ammo once.
                        {
                            outOfAmmo.Add(vesselName);
                            if (vData != null && (now - vData.lastHitTime < 2))
                            {
                                competitionStatus.Add(vesselName + " damaged by " + vData.LastPersonWhoDamagedMe() + " and lost weapons");
                            }
                            else
                            {
                                competitionStatus.Add(vesselName + " is out of Ammunition");
                            }
                        }
                        if (mf.guardMode) // If we're in guard mode, check to see if we should disable it.
                        {
                            var pilotAI = vessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                            var surfaceAI = vessel.FindPartModuleImplementing<BDModuleSurfaceAI>(); // Get the surface AI if the vessel has one.
                            if ((pilotAI == null && surfaceAI == null) || (mf.outOfAmmo && (BDArmorySettings.DISABLE_RAMMING || !(pilotAI != null && pilotAI.allowRamming)))) // if we've lost the AI or the vessel is out of weapons/ammo and ramming is not allowed.
                                mf.guardMode = false;
                        }
                    }

                    // update the vessel scoring structure
                    if (vData != null)
                    {
                        vData.AverageSpeed += vessel.srfSpeed;
                        vData.AverageAltitude += vessel.altitude;
                        vData.averageCount++;
                        if (vData.landedState && !BDArmorySettings.DISABLE_KILL_TIMER)
                        {
                            KillTimer[vesselName] = (int)(now - vData.landedKillTimer);
                            if (now - vData.landedKillTimer > BDArmorySettings.COMPETITION_KILL_TIMER)
                            {
                                vesselsToKill.Add(mf.vessel);
                                competitionStatus.Add(vesselName + " landed too long.");
                            }
                        }
                        else if (KillTimer.ContainsKey(vesselName))
                            KillTimer.Remove(vesselName);
                    }
                }
            }
            if (BDArmorySettings.TAG_MODE)
            {
                foreach (var vesselName in Scores.Keys)
                    UpdateTag(alive.Contains(vesselName) ? Scores[vesselName].weaponManagerRef : null, vesselName, previousNumberCompetitive);
            }
            string aliveString = string.Join(",", alive.ToArray());
            previousNumberCompetitive = numberOfCompetitiveVessels;
            // Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] STILLALIVE: " + aliveString); // This just fills the logs needlessly.
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                // If we find a vessel named "Pinata" that's a special case object
                // this should probably be configurable.
                if (!pinataAlive && alive.Contains("Pinata"))
                {
                    Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: Setting Pinata Flag to Alive!");
                    pinataAlive = true;
                    competitionStatus.Add("Enabling Pinata");
                }
                else if (pinataAlive && !alive.Contains("Pinata"))
                {
                    // switch everyone onto separate teams when the Pinata Dies
                    LoadedVesselSwitcher.Instance.MassTeamSwitch(true);
                    pinataAlive = false;
                    competitionStatus.Add("Pinata is dead - competition is now a Free for all");
                    // start kill clock
                    if (!killerGMenabled)
                    {
                        // disabled for now, should be in a competition settings UI
                        //killerGMenabled = true;
                    }

                }
            }
            doaUpdate += "     DEAD: ";
            foreach (string key in Scores.Keys)
            {
                // check everyone who's no longer alive
                if (!alive.Contains(key))
                {
                    if (BDArmorySettings.RUNWAY_PROJECT && key == "Pinata") continue;
                    if (!DeathOrder.ContainsKey(key))
                    {
                        // adding pilot into death order
                        DeathOrder[key] = new Tuple<int, double>(DeathOrder.Count, now - competitionStartTime);
                        pilotActions[key] = " is Dead";
                        var whoKilledMe = "";

                        DeathCount++;

                        if (Scores[key].gmKillReason == GMKillReason.None && now - Scores[key].LastDamageTime() < 10) // Recent kills that weren't instigated by the GM (or similar).
                        {
                            // if last hit was recent that person gets the kill
                            whoKilledMe = Scores[key].LastPersonWhoDamagedMe();
                            Scores[key].cleanDeath = true;

                            var lastDamageWasFrom = Scores[key].LastDamageWasFrom();
                            switch (lastDamageWasFrom)
                            {
                                case DamageFrom.Bullet:
                                    if (!whoCleanShotWho.ContainsKey(key))
                                    {
                                        // twice - so 2 points
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":CLEANKILL:" + whoKilledMe);
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":KILLED:" + whoKilledMe);
                                        whoCleanShotWho.Add(key, whoKilledMe);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            Competition.BDAScoreService.Instance.TrackKill(whoKilledMe, key);
                                        whoKilledMe += " (BOOM! HEADSHOT!)";
                                    }
                                    break;
                                case DamageFrom.Missile:
                                    if (!whoCleanShotWhoWithMissiles.ContainsKey(key))
                                    {
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":CLEANMISSILEKILL:" + whoKilledMe);
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":KILLED:" + whoKilledMe);
                                        whoCleanShotWhoWithMissiles.Add(key, whoKilledMe);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            Competition.BDAScoreService.Instance.TrackKill(whoKilledMe, key);
                                        whoKilledMe += " (BOOM! HEADSHOT!)";
                                    }
                                    break;
                                case DamageFrom.Ram:
                                    if (!whoCleanRammedWho.ContainsKey(key))
                                    {
                                        // if ram killed
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":CLEANRAMKILL:" + whoKilledMe);
                                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":KILLED VIA RAMMERY BY:" + whoKilledMe);
                                        whoCleanRammedWho.Add(key, whoKilledMe);
                                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                                            Competition.BDAScoreService.Instance.TrackKill(whoKilledMe, key);
                                        whoKilledMe += " (BOOM! HEADSHOT!)";
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        else if (Scores[key].everyoneWhoHitMe.Count > 0 || Scores[key].everyoneWhoRammedMe.Count > 0 || Scores[key].everyoneWhoHitMeWithMissiles.Count > 0)
                        {
                            List<string> killReasons = new List<string>();
                            if (Scores[key].everyoneWhoHitMe.Count > 0)
                                killReasons.Add("Hits");
                            if (Scores[key].everyoneWhoHitMeWithMissiles.Count > 0)
                                killReasons.Add("Missiles");
                            if (Scores[key].everyoneWhoRammedMe.Count > 0)
                                killReasons.Add("Rams");
                            whoKilledMe = String.Join(", ", Scores[key].EveryOneWhoDamagedMe()) + (Scores[key].gmKillReason != GMKillReason.None ? ", " + Scores[key].gmKillReason : "") + " (" + String.Join(" ", killReasons) + ")";

                            foreach (var killer in Scores[key].EveryOneWhoDamagedMe())
                            {
                                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":KILLED:" + killer);
                            }
                            if (BDArmorySettings.REMOTE_LOGGING_ENABLED && Scores[key].gmKillReason == GMKillReason.None) // Don't count kills by the GM.
                                Competition.BDAScoreService.Instance.ComputeAssists(key, "", now - competitionStartTime);
                        }
                        if (whoKilledMe != "")
                        {
                            switch (Scores[key].LastDamageWasFrom())
                            {
                                case DamageFrom.Bullet:
                                case DamageFrom.Missile:
                                    competitionStatus.Add(key + " was killed by " + whoKilledMe);
                                    break;
                                case DamageFrom.Ram:
                                    competitionStatus.Add(key + " was rammed to death by " + whoKilledMe);
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            competitionStatus.Add(key + " was killed");
                            Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + ":KILLED:NOBODY");
                        }
                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                            Competition.BDAScoreService.Instance.TrackDeath(key);
                    }
                    doaUpdate += " :" + key + ": ";
                }
            }
            deadOrAlive = doaUpdate;

            var numberOfCompetitiveTeams = LoadedVesselSwitcher.Instance.WeaponManagers.Count;
            if (now - competitionStartTime > BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD && (numberOfCompetitiveVessels < 2 || (!BDArmorySettings.TAG_MODE && numberOfCompetitiveTeams < 2)) && !VesselSpawner.Instance.vesselsSpawningContinuously)
            {
                if (finalGracePeriodStart < 0)
                    finalGracePeriodStart = now;
                if (!(BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD > 60) && now - finalGracePeriodStart > BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD)
                {
                    competitionStatus.Add("All Pilots are Dead");
                    foreach (string key in alive)
                    {
                        competitionStatus.Add(key + " wins the round!");
                    }
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]:No viable competitors, Automatically dumping scores");
                    StopCompetition();
                }
            }

            //Reset gravity
            if (BDArmorySettings.GRAVITY_HACKS && competitionIsActive)
            {
                int maxVesselsActive = (VesselSpawner.Instance.vesselsSpawningContinuously && BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0) ? BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS : Scores.Count;
                double time = now - competitionStartTime;
                gravityMultiplier = 1f + 7f * (float)(DeathCount % maxVesselsActive) / (float)(maxVesselsActive - 1); // From 1G to 8G.
                gravityMultiplier += VesselSpawner.Instance.vesselsSpawningContinuously ? Mathf.Sqrt(5f - 5f * Mathf.Cos((float)time / 600f * Mathf.PI)) : Mathf.Sqrt((float)time / 60f); // Plus up to 3.16G.
                PhysicsGlobals.GraviticForceMultiplier = (double)gravityMultiplier;
                VehiclePhysics.Gravity.Refresh();
                if (Mathf.RoundToInt(10 * gravityMultiplier) != Mathf.RoundToInt(10 * lastGravityMultiplier)) // Only write a message when it shows something different.
                {
                    lastGravityMultiplier = gravityMultiplier;
                    competitionStatus.Add("Competition: Adjusting gravity to " + gravityMultiplier.ToString("0.0") + "G!");
                }
            }

            // use the exploder system to remove vessels that should be nuked
            foreach (var vessel in vesselsToKill)
            {
                var vesselName = vessel.GetName();
                var killerName = "";
                if (Scores.ContainsKey(vesselName))
                {
                    killerName = Scores[vesselName].LastPersonWhoDamagedMe();
                    if (killerName == "")
                    {
                        Scores[vesselName].lastPersonWhoHitMe = "Landed Too Long"; // only do this if it's not already damaged
                        killerName = "Landed Too Long";
                    }
                }
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + vesselName + ":REMOVED:" + killerName);
                if (KillTimer.ContainsKey(vesselName)) KillTimer.Remove(vesselName);
                Misc.Misc.ForceDeadVessel(vessel);
            }

            if (!(BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY > 60))
                RemoveNonCompetitors();

            CheckAltitudeLimits();
            if (BDArmorySettings.RUNWAY_PROJECT)
            {
                FindVictim();
            }
            // Debug.Log("[BDArmory.BDACompetitionMode" + CompetitionID.ToString() + "]: Done With Update");
            if (BDArmorySettings.TAG_MODE) lastTagUpdateTime = now;

            if (!VesselSpawner.Instance.vesselsSpawningContinuously && BDArmorySettings.COMPETITION_DURATION > 0 && now - competitionStartTime >= BDArmorySettings.COMPETITION_DURATION * 60d)
            {
                LogResults("due to out-of-time");
                StopCompetition();
            }
        }

        // This now also writes the competition logs to GameData/BDArmory/Logs/<CompetitionID>[-tag].log
        public void LogResults(string message = "", string tag = "")
        {
            if (competitionStartTime < 0)
            {
                Debug.Log("[BDArmory.BDACompetitionMode]: No active competition, not dumping results.");
                return;
            }
            CheckMemoryUsage();
            if (VesselSpawner.Instance.vesselsSpawningContinuously) // Dump continuous spawning scores instead.
            {
                VesselSpawner.Instance.DumpContinuousSpawningScores(tag);
                return;
            }


            var logStrings = new List<string>();

            // get everyone who's still alive
            alive.Clear();
            competitionStatus.Add("Dumping scores for competition " + CompetitionID.ToString() + (tag != "" ? " " + tag : ""));
            logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: Dumping Results" + (message != "" ? " " + message : "") + " after " + (int)(Planetarium.GetUniversalTime() - competitionStartTime) + "s (of " + (BDArmorySettings.COMPETITION_DURATION * 60d) + "s) at " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz"));

            // Find out who's still alive
            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel == null || !vessel.loaded || vessel.packed)
                    continue;
                if (vessel.FindPartModuleImplementing<MissileFire>() != null)
                    alive.Add(vessel.vesselName);
            }

            // General result. (Note: use hand-coded JSON to make parsing easier in python.)
            var survivingTeams = new HashSet<string>();
            foreach (var vesselName in alive)
            {
                if (Scores.ContainsKey(vesselName))
                    survivingTeams.Add(Scores[vesselName].team);
            }
            if (survivingTeams.Count == 0)
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: RESULT:Mutual Annihilation");
            else if (survivingTeams.Count == 1)
            {
                var winningTeam = survivingTeams.First();
                var winningTeamMembers = Scores.Where(s => s.Value.team == winningTeam).Select(s => s.Key);
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: RESULT:Win:{\"team\": " + $"\"{winningTeam}\", \"members\": [" + string.Join(", ", winningTeamMembers.Select(m => $"\"{m.Replace("\"", "\\\"")}\"")) + "]}");
            }
            else
            {
                var drawTeams = survivingTeams.ToDictionary(t => t, t => Scores.Where(s => s.Value.team == t).Select(s => s.Key));
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: RESULT:Draw:[" + string.Join(", ", drawTeams.Select(t => "{\"team\": " + $"\"{t.Key}\"" + ", \"members\": [" + string.Join(", ", t.Value.Select(m => $"\"{m.Replace("\"", "\\\"")}\"")) + "]}")) + "]");
            }

            // Record ALIVE/DEAD status of each craft.
            foreach (var vesselName in alive) // List ALIVE craft first
            {
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: ALIVE:" + vesselName);
            }
            foreach (string key in Scores.Keys) // Then DEAD or MIA.
            {
                if (!alive.Contains(key))
                {
                    if (DeathOrder.ContainsKey(key))
                    {
                        logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: DEAD:" + DeathOrder[key].Item1 + ":" + DeathOrder[key].Item2.ToString("0.0") + ":" + key); // DEAD: <death order>:<death time>:<vessel name>
                    }
                    else
                    {
                        logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: MIA:" + key);
                    }
                }
            }

            // Who shot who.
            foreach (var key in Scores.Keys)
                if (Scores[key].hitCounts.Count > 0)
                {
                    string whoShotMe = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHOSHOTWHO:" + key;
                    foreach (var vesselName in Scores[key].hitCounts.Keys)
                        whoShotMe += ":" + Scores[key].hitCounts[vesselName] + ":" + vesselName;
                    logStrings.Add(whoShotMe);
                }

            // Damage from bullets
            foreach (var key in Scores.Keys)
                if (Scores[key].damageFromBullets.Count > 0)
                {
                    string whoDamagedMeWithBullets = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHODAMAGEDWHOWITHBULLETS:" + key;
                    foreach (var vesselName in Scores[key].damageFromBullets.Keys)
                        whoDamagedMeWithBullets += ":" + Scores[key].damageFromBullets[vesselName].ToString("0.0") + ":" + vesselName;
                    logStrings.Add(whoDamagedMeWithBullets);
                }

            // Who hit who with missiles.
            foreach (var key in Scores.Keys)
                if (Scores[key].missileHitCounts.Count > 0)
                {
                    string whoHitMeWithMissiles = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHOHITWHOWITHMISSILES:" + key;
                    foreach (var vesselName in Scores[key].missileHitCounts.Keys)
                        whoHitMeWithMissiles += ":" + Scores[key].missileHitCounts[vesselName] + ":" + vesselName;
                    logStrings.Add(whoHitMeWithMissiles);
                }

            // Who shot who with missiles.
            foreach (var key in Scores.Keys)
                if (Scores[key].missilePartDamageCounts.Count > 0)
                {
                    string whoShotMeWithMissiles = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHOPARTSHITWHOWITHMISSILES:" + key;
                    foreach (var vesselName in Scores[key].missilePartDamageCounts.Keys)
                        whoShotMeWithMissiles += ":" + Scores[key].missilePartDamageCounts[vesselName] + ":" + vesselName;
                    logStrings.Add(whoShotMeWithMissiles);
                }

            // Damage from missiles
            foreach (var key in Scores.Keys)
                if (Scores[key].damageFromMissiles.Count > 0)
                {
                    string whoDamagedMeWithMissiles = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHODAMAGEDWHOWITHMISSILES:" + key;
                    foreach (var vesselName in Scores[key].damageFromMissiles.Keys)
                        whoDamagedMeWithMissiles += ":" + Scores[key].damageFromMissiles[vesselName].ToString("0.0") + ":" + vesselName;
                    logStrings.Add(whoDamagedMeWithMissiles);
                }

            // Who rammed who.
            foreach (var key in Scores.Keys)
                if (Scores[key].rammingPartLossCounts.Count > 0)
                {
                    string whoRammedMe = "[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: WHORAMMEDWHO:" + key;
                    foreach (var vesselName in Scores[key].rammingPartLossCounts.Keys)
                        whoRammedMe += ":" + Scores[key].rammingPartLossCounts[vesselName] + ":" + vesselName;
                    logStrings.Add(whoRammedMe);
                }

            // Other kill reasons
            foreach (var key in Scores.Keys)
                if (Scores[key].gmKillReason != GMKillReason.None)
                    logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: OTHERKILL:" + key + ":" + Scores[key].gmKillReason);

            // Log clean kills/rams
            foreach (var key in whoCleanShotWho.Keys)
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: CLEANKILL:" + key + ":" + whoCleanShotWho[key]);
            foreach (var key in whoCleanShotWhoWithMissiles.Keys)
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: CLEANMISSILEKILL:" + key + ":" + whoCleanShotWhoWithMissiles[key]);
            foreach (var key in whoCleanRammedWho.Keys)
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: CLEANRAM:" + key + ":" + whoCleanRammedWho[key]);

            // Accuracy
            foreach (var key in Scores.Keys)
                logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: ACCURACY:" + key + ":" + Scores[key].Score + "/" + Scores[key].shotsFired);

            // Time "IT" and kills while "IT" logging
            if (BDArmorySettings.TAG_MODE)
            {
                foreach (var key in Scores.Keys)
                    logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: TAGSCORE:" + key + ":" + Scores[key].tagScore.ToString("0.0"));

                foreach (var key in Scores.Keys)
                    logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: TIMEIT:" + key + ":" + Scores[key].tagTotalTime.ToString("0.0"));

                foreach (var key in Scores.Keys)
                    if (Scores[key].tagKillsWhileIt > 0)
                        logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: KILLSWHILEIT:" + key + ":" + Scores[key].tagKillsWhileIt);

                foreach (var key in Scores.Keys)
                    if (Scores[key].tagTimesIt > 0)
                        logStrings.Add("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: TIMESIT:" + key + ":" + Scores[key].tagTimesIt);
            }

            // Dump the log results to a file
            if (CompetitionID > 0)
            {
                var folder = Environment.CurrentDirectory + "/GameData/BDArmory/Logs";
                if (BDATournament.Instance.tournamentStatus == TournamentStatus.Running)
                {
                    folder = Path.Combine(folder, "Tournament " + BDATournament.Instance.tournamentID, "Round " + BDATournament.Instance.currentRound);
                    tag = "Heat " + BDATournament.Instance.currentHeat;
                }
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllLines(Path.Combine(folder, CompetitionID.ToString() + (tag != "" ? "-" + tag : "") + ".log"), logStrings);
            }
            // Also dump the results to the normal log.
            foreach (var line in logStrings)
                Debug.Log(line);
        }

        #region Ramming
        // Ramming Logging
        public class RammingTargetInformation
        {
            public Vessel vessel; // The other vessel involved in a collision.
            public double lastUpdateTime = 0; // Last time the timeToCPA was updated.
            public float timeToCPA = 0f; // Time to closest point of approach.
            public bool potentialCollision = false; // Whether a collision might happen shortly.
            public double potentialCollisionDetectionTime = 0; // The latest time the potential collision was detected.
            public int partCountJustPriorToCollision; // The part count of the colliding vessel just prior to the collision.
            public float sqrDistance; // Distance^2 at the time of collision.
            public float angleToCoM = 0f; // The angle from a vessel's velocity direction to the center of mass of the target.
            public bool collisionDetected = false; // Whether a collision has actually been detected.
            public double collisionDetectedTime; // The time that the collision actually occurs.
            public bool ramming = false; // True if a ram was attempted between the detection of a potential ram and the actual collision.
        };
        public class RammingInformation
        {
            public Vessel vessel; // This vessel.
            public string vesselName; // The GetName() name of the vessel (in case vessel gets destroyed and we can't get it from there).
            public int partCount; // The part count of a vessel.
            public float radius; // The vessels "radius" at the time the potential collision was detected.
            public Dictionary<string, RammingTargetInformation> targetInformation; // Information about the ramming target.
        };
        public Dictionary<string, RammingInformation> rammingInformation;

        // Initialise the rammingInformation dictionary with the required vessels.
        public void InitialiseRammingInformation()
        {
            double currentTime = Planetarium.GetUniversalTime();
            rammingInformation = new Dictionary<string, RammingInformation>();
            var pilots = getAllPilots();
            foreach (var pilot in pilots)
            {
                var pilotAI = pilot.vessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                if (pilotAI == null) continue;
                var targetRammingInformation = new Dictionary<string, RammingTargetInformation>();
                foreach (var otherPilot in pilots)
                {
                    if (otherPilot == pilot) continue; // Don't include same-vessel information.
                    var otherPilotAI = otherPilot.vessel.FindPartModuleImplementing<BDModulePilotAI>(); // Get the pilot AI if the vessel has one.
                    if (otherPilotAI == null) continue;
                    targetRammingInformation.Add(otherPilot.vessel.vesselName, new RammingTargetInformation { vessel = otherPilot.vessel });
                }
                rammingInformation.Add(pilot.vessel.vesselName, new RammingInformation
                {
                    vessel = pilot.vessel,
                    vesselName = pilot.vessel.GetName(),
                    partCount = pilot.vessel.parts.Count,
                    radius = pilot.vessel.GetRadius(),
                    targetInformation = targetRammingInformation,
                });
            }
        }

        // Update the ramming information dictionary with expected times to closest point of approach.
        private float maxTimeToCPA = 5f; // Don't look more than 5s ahead.
        public void UpdateTimesToCPAs()
        {
            double currentTime = Planetarium.GetUniversalTime();
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                var pilotAI = vessel != null ? vessel.FindPartModuleImplementing<BDModulePilotAI>() : null; // Get the pilot AI if the vessel has one.

                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                    var otherPilotAI = otherVessel != null ? otherVessel.FindPartModuleImplementing<BDModulePilotAI>() : null; // Get the pilot AI if the vessel has one.
                    if (pilotAI == null || otherPilotAI == null) // One of the vessels or pilot AIs has been destroyed.
                    {
                        rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = maxTimeToCPA; // Set the timeToCPA to maxTimeToCPA, so that it's not considered for new potential collisions.
                        rammingInformation[otherVesselName].targetInformation[vesselName].timeToCPA = maxTimeToCPA; // Set the timeToCPA to maxTimeToCPA, so that it's not considered for new potential collisions.
                    }
                    else
                    {
                        if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].lastUpdateTime > rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA / 2f) // When half the time is gone, update it.
                        {
                            float timeToCPA = AIUtils.ClosestTimeToCPA(vessel, otherVessel, maxTimeToCPA); // Look up to maxTimeToCPA ahead.
                            if (timeToCPA > 0f && timeToCPA < maxTimeToCPA) // If the closest approach is within the next maxTimeToCPA, log it.
                                rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = timeToCPA;
                            else // Otherwise set it to the max value.
                                rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA = maxTimeToCPA;
                            // This is symmetric, so update the symmetric value and set the lastUpdateTime for both so that we don't bother calculating the same thing twice.
                            rammingInformation[otherVesselName].targetInformation[vesselName].timeToCPA = rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA;
                            rammingInformation[vesselName].targetInformation[otherVesselName].lastUpdateTime = currentTime;
                            rammingInformation[otherVesselName].targetInformation[vesselName].lastUpdateTime = currentTime;
                        }
                    }
                }
            }
        }

        // Get the upcoming collisions ordered by predicted separation^2 (for Scott to adjust camera views).
        public IOrderedEnumerable<KeyValuePair<Tuple<string, string>, Tuple<float, float>>> UpcomingCollisions(float distanceThreshold, bool sortByDistance = true)
        {
            var upcomingCollisions = new Dictionary<Tuple<string, string>, Tuple<float, float>>();
            if (rammingInformation != null)
                foreach (var vesselName in rammingInformation.Keys)
                    foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                        if (rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision && rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA < maxTimeToCPA && String.Compare(vesselName, otherVesselName) < 0)
                            if (rammingInformation[vesselName].vessel != null && rammingInformation[otherVesselName].vessel != null)
                            {
                                var predictedSqrSeparation = Vector3.SqrMagnitude(rammingInformation[vesselName].vessel.CoM - rammingInformation[otherVesselName].vessel.CoM);
                                if (predictedSqrSeparation < distanceThreshold * distanceThreshold)
                                    upcomingCollisions.Add(
                                        new Tuple<string, string>(vesselName, otherVesselName),
                                        new Tuple<float, float>(predictedSqrSeparation, rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA)
                                    );
                            }
            return upcomingCollisions.OrderBy(d => sortByDistance ? d.Value.Item1 : d.Value.Item2);
        }

        // Check for potential collisions in the near future and update data structures as necessary.
        private float potentialCollisionDetectionTime = 1f; // 1s ought to be plenty.
        private void CheckForPotentialCollisions()
        {
            float collisionMargin = 4f; // Sum of radii is less than this factor times the separation.
            double currentTime = Planetarium.GetUniversalTime();
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    if (!rammingInformation.ContainsKey(otherVesselName))
                    {
                        Debug.Log("DEBUG other vessel (" + otherVesselName + ") is missing from rammingInformation!");
                        return;
                    }
                    if (!rammingInformation[vesselName].targetInformation.ContainsKey(otherVesselName))
                    {
                        Debug.Log("DEBUG other vessel (" + otherVesselName + ") is missing from rammingInformation[vessel].targetInformation!");
                        return;
                    }
                    if (!rammingInformation[otherVesselName].targetInformation.ContainsKey(vesselName))
                    {
                        Debug.Log("DEBUG vessel (" + vesselName + ") is missing from rammingInformation[otherVessel].targetInformation!");
                        return;
                    }
                    var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                    if (rammingInformation[vesselName].targetInformation[otherVesselName].timeToCPA < potentialCollisionDetectionTime) // Closest point of approach is within the detection time.
                    {
                        if (vessel != null && otherVessel != null) // If one of the vessels has been destroyed, don't calculate new potential collisions, but allow the timer on existing potential collisions to run out so that collision analysis can still use it.
                        {
                            var separation = Vector3.Magnitude(vessel.transform.position - otherVessel.transform.position);
                            if (separation < collisionMargin * (vessel.GetRadius() + otherVessel.GetRadius())) // Potential collision detected.
                            {
                                if (!rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision) // Register the part counts and angles when the potential collision is first detected.
                                { // Note: part counts and vessel radii get updated whenever a new potential collision is detected, but not angleToCoM (which is specific to a colliding pair).
                                    rammingInformation[vesselName].partCount = vessel.parts.Count;
                                    rammingInformation[otherVesselName].partCount = otherVessel.parts.Count;
                                    rammingInformation[vesselName].radius = vessel.GetRadius();
                                    rammingInformation[otherVesselName].radius = otherVessel.GetRadius();
                                    rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM = Vector3.Angle(vessel.srf_vel_direction, otherVessel.CoM - vessel.CoM);
                                    rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM = Vector3.Angle(otherVessel.srf_vel_direction, vessel.CoM - otherVessel.CoM);
                                }

                                // Update part counts if vessels get shot and potentially lose parts before the collision happens.
                                if (!Scores.ContainsKey(rammingInformation[vesselName].vesselName)) CheckVesselType(rammingInformation[vesselName].vessel); // It may have become a "vesselName Plane" if the WM is badly placed.
                                try
                                {
                                    if (Scores[rammingInformation[vesselName].vesselName].lastHitTime > rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollisionDetectionTime)
                                        if (rammingInformation[vesselName].partCount != vessel.parts.Count)
                                        {
                                            if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: " + vesselName + " lost " + (rammingInformation[vesselName].partCount - vessel.parts.Count) + " parts from getting shot.");
                                            rammingInformation[vesselName].partCount = vessel.parts.Count;
                                        }
                                    if (!Scores.ContainsKey(rammingInformation[otherVesselName].vesselName)) CheckVesselType(rammingInformation[otherVesselName].vessel); // It may have become a "vesselName Plane" if the WM is badly placed.
                                    if (Scores[rammingInformation[otherVesselName].vesselName].lastHitTime > rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime)
                                        if (rammingInformation[vesselName].partCount != vessel.parts.Count)
                                        {
                                            if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: " + otherVesselName + " lost " + (rammingInformation[otherVesselName].partCount - otherVessel.parts.Count) + " parts from getting shot.");
                                            rammingInformation[otherVesselName].partCount = otherVessel.parts.Count;
                                        }
                                }
                                catch (KeyNotFoundException e)
                                {
                                    List<string> badVesselNames = new List<string>();
                                    if (!Scores.ContainsKey(rammingInformation[vesselName].vesselName)) badVesselNames.Add(rammingInformation[vesselName].vesselName);
                                    if (!Scores.ContainsKey(rammingInformation[otherVesselName].vesselName)) badVesselNames.Add(rammingInformation[otherVesselName].vesselName);
                                    Debug.LogWarning("[BDArmory.BDACompetitionMode]: A badly named vessel is messing up the collision detection: " + string.Join(", ", badVesselNames) + " | " + e.Message);
                                }

                                // Set the potentialCollision flag to true and update the latest potential collision detection time.
                                rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision = true;
                                rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime = currentTime;
                                rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollision = true;
                                rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollisionDetectionTime = currentTime;

                                // Register intent to ram.
                                var pilotAI = vessel.FindPartModuleImplementing<BDModulePilotAI>();
                                rammingInformation[vesselName].targetInformation[otherVesselName].ramming |= (pilotAI != null && pilotAI.ramming); // Pilot AI is alive and trying to ram.
                                var otherPilotAI = otherVessel.FindPartModuleImplementing<BDModulePilotAI>();
                                rammingInformation[otherVesselName].targetInformation[vesselName].ramming |= (otherPilotAI != null && otherPilotAI.ramming); // Other pilot AI is alive and trying to ram.
                            }
                        }
                    }
                    else if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime > 2f * potentialCollisionDetectionTime) // Potential collision is no longer relevant.
                    {
                        rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision = false;
                        rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollision = false;
                    }
                }
            }
        }

        // Analyse a collision to figure out if someone rammed someone else and who should get awarded for it.
        private void AnalyseCollision(EventReport data)
        {
            if (data.origin == null) return; // The part is gone. Nothing much we can do about it.
            double currentTime = Planetarium.GetUniversalTime();
            float collisionMargin = 2f; // Compare the separation to this factor times the sum of radii to account for inaccuracies in the vessels size and position. Hopefully, this won't include other nearby vessels.
            var vessel = data.origin.vessel;
            if (vessel == null) // Can vessel be null here? It doesn't appear so.
            {
                if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: in AnalyseCollision the colliding part belonged to a null vessel!");
                return;
            }
            bool hitVessel = false;
            if (rammingInformation.ContainsKey(vessel.vesselName)) // If the part was attached to a vessel,
            {
                var vesselName = vessel.vesselName; // For convenience.
                var destroyedPotentialColliders = new List<string>();
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys) // for each other vessel,
                    if (rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision) // if it was potentially about to collide,
                    {
                        var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                        if (otherVessel == null) // Vessel that was potentially colliding has been destroyed. It's more likely that an alive potential collider is the real collider, so remember it in case there are no living potential colliders.
                        {
                            destroyedPotentialColliders.Add(otherVesselName);
                            continue;
                        }
                        var separation = Vector3.Magnitude(vessel.transform.position - otherVessel.transform.position);
                        if (separation < collisionMargin * (rammingInformation[vesselName].radius + rammingInformation[otherVesselName].radius)) // and their separation is less than the sum of their radii,
                        {
                            if (!rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected) // Take the values when the collision is first detected.
                            {
                                rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected = true; // register it as involved in the collision. We'll check for damaged parts in CheckForDamagedParts.
                                rammingInformation[otherVesselName].targetInformation[vesselName].collisionDetected = true; // The information is symmetric.
                                rammingInformation[vesselName].targetInformation[otherVesselName].partCountJustPriorToCollision = rammingInformation[otherVesselName].partCount;
                                rammingInformation[otherVesselName].targetInformation[vesselName].partCountJustPriorToCollision = rammingInformation[vesselName].partCount;
                                rammingInformation[vesselName].targetInformation[otherVesselName].sqrDistance = (otherVessel != null) ? Vector3.SqrMagnitude(vessel.CoM - otherVessel.CoM) : (Mathf.Pow(collisionMargin * (rammingInformation[vesselName].radius + rammingInformation[otherVesselName].radius), 2f) + 1f); // FIXME Should destroyed vessels have 0 sqrDistance instead?
                                rammingInformation[otherVesselName].targetInformation[vesselName].sqrDistance = rammingInformation[vesselName].targetInformation[otherVesselName].sqrDistance;
                                rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetectedTime = currentTime;
                                rammingInformation[otherVesselName].targetInformation[vesselName].collisionDetectedTime = currentTime;
                                if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: Collision detected between " + vesselName + " and " + otherVesselName);
                            }
                            hitVessel = true;
                        }
                    }
                if (!hitVessel) // No other living vessels were potential targets, add in the destroyed ones (if any).
                {
                    foreach (var otherVesselName in destroyedPotentialColliders) // Note: if there are more than 1, then multiple craft could be credited with the kill, but this is unlikely.
                    {
                        rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected = true; // register it as involved in the collision. We'll check for damaged parts in CheckForDamagedParts.
                        hitVessel = true;
                    }
                }
                if (!hitVessel) // We didn't hit another vessel, maybe it crashed and died.
                {
                    if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: " + vesselName + " hit something else.");
                    foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                    {
                        rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollision = false; // Set potential collisions to false.
                        rammingInformation[otherVesselName].targetInformation[vesselName].potentialCollision = false; // Set potential collisions to false.
                    }
                }
            }
        }

        // Check for parts being lost on the various vessels for which collisions have been detected.
        private void CheckForDamagedParts()
        {
            double currentTime = Planetarium.GetUniversalTime();
            float headOnLimit = 20f;
            var collidingVesselsBySeparation = new Dictionary<string, KeyValuePair<float, IOrderedEnumerable<KeyValuePair<string, float>>>>();

            // First, we're going to filter the potentially colliding vessels and sort them by separation.
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                var collidingVesselDistances = new Dictionary<string, float>();

                // For each potential collision that we've waited long enough for, refine the potential colliding vessels.
                foreach (var otherVesselName in rammingInformation[vesselName].targetInformation.Keys)
                {
                    if (!rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected) continue; // Other vessel wasn't involved in a collision with this vessel.
                    if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime > potentialCollisionDetectionTime) // We've waited long enough for the parts that are going to explode to explode.
                    {
                        // First, check the vessels marked as colliding with this vessel for lost parts. If at least one other lost parts or was destroyed, exclude any that didn't lose parts (set collisionDetected to false).
                        bool someOneElseLostParts = false;
                        foreach (var tmpVesselName in rammingInformation[vesselName].targetInformation.Keys)
                        {
                            if (!rammingInformation[vesselName].targetInformation[tmpVesselName].collisionDetected) continue; // Other vessel wasn't involved in a collision with this vessel.
                            var tmpVessel = rammingInformation[vesselName].targetInformation[tmpVesselName].vessel;
                            if (tmpVessel == null || rammingInformation[vesselName].targetInformation[tmpVesselName].partCountJustPriorToCollision - tmpVessel.parts.Count > 0)
                            {
                                someOneElseLostParts = true;
                                break;
                            }
                        }
                        if (someOneElseLostParts) // At least one other vessel lost parts or was destroyed.
                        {
                            foreach (var tmpVesselName in rammingInformation[vesselName].targetInformation.Keys)
                            {
                                if (!rammingInformation[vesselName].targetInformation[tmpVesselName].collisionDetected) continue; // Other vessel wasn't involved in a collision with this vessel.
                                var tmpVessel = rammingInformation[vesselName].targetInformation[tmpVesselName].vessel;
                                if (tmpVessel != null && rammingInformation[vesselName].targetInformation[tmpVesselName].partCountJustPriorToCollision == tmpVessel.parts.Count) // Other vessel didn't lose parts, mark it as not involved in this collision.
                                {
                                    rammingInformation[vesselName].targetInformation[tmpVesselName].collisionDetected = false;
                                    rammingInformation[tmpVesselName].targetInformation[vesselName].collisionDetected = false;
                                }
                            }
                        } // Else, the collided with vessels didn't lose any parts, so we don't know who this vessel really collided with.

                        // If the other vessel is still a potential collider, add it to the colliding vessels dictionary with its distance to this vessel.
                        if (rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected)
                            collidingVesselDistances.Add(otherVesselName, rammingInformation[vesselName].targetInformation[otherVesselName].sqrDistance);
                    }
                }

                // If multiple vessels are involved in a collision with this vessel, the lost parts counts are going to be skewed towards the first vessel processed. To counteract this, we'll sort the colliding vessels by their distance from this vessel.
                var collidingVessels = collidingVesselDistances.OrderBy(d => d.Value);
                if (collidingVesselDistances.Count > 0)
                    collidingVesselsBySeparation.Add(vesselName, new KeyValuePair<float, IOrderedEnumerable<KeyValuePair<string, float>>>(collidingVessels.First().Value, collidingVessels));

                if (BDArmorySettings.DEBUG_RAMMING_LOGGING && collidingVesselDistances.Count > 1) // DEBUG
                {
                    foreach (var otherVesselName in collidingVesselDistances.Keys) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: colliding vessel distances^2 from " + vesselName + ": " + otherVesselName + " " + collidingVesselDistances[otherVesselName]);
                    foreach (var otherVesselName in collidingVessels) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: sorted order: " + otherVesselName.Key);
                }
            }
            var sortedCollidingVesselsBySeparation = collidingVesselsBySeparation.OrderBy(d => d.Value.Key); // Sort the outer dictionary by minimum separation from the nearest colliding vessel.

            // Then we're going to try to figure out who should be awarded the ram.
            foreach (var vesselNameKVP in sortedCollidingVesselsBySeparation)
            {
                var vesselName = vesselNameKVP.Key;
                var vessel = rammingInformation[vesselName].vessel;
                foreach (var otherVesselNameKVP in vesselNameKVP.Value.Value)
                {
                    var otherVesselName = otherVesselNameKVP.Key;
                    if (!rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected) continue; // Other vessel wasn't involved in a collision with this vessel.
                    if (currentTime - rammingInformation[vesselName].targetInformation[otherVesselName].potentialCollisionDetectionTime > potentialCollisionDetectionTime) // We've waited long enough for the parts that are going to explode to explode.
                    {
                        var otherVessel = rammingInformation[vesselName].targetInformation[otherVesselName].vessel;
                        var pilotAI = vessel != null ? vessel.FindPartModuleImplementing<BDModulePilotAI>() : null;
                        var otherPilotAI = otherVessel != null ? otherVessel.FindPartModuleImplementing<BDModulePilotAI>() : null;

                        // Count the number of parts lost.
                        var rammedPartsLost = (otherPilotAI == null) ? rammingInformation[vesselName].targetInformation[otherVesselName].partCountJustPriorToCollision : rammingInformation[vesselName].targetInformation[otherVesselName].partCountJustPriorToCollision - otherVessel.parts.Count;
                        var rammingPartsLost = (pilotAI == null) ? rammingInformation[otherVesselName].targetInformation[vesselName].partCountJustPriorToCollision : rammingInformation[otherVesselName].targetInformation[vesselName].partCountJustPriorToCollision - vessel.parts.Count;
                        rammingInformation[otherVesselName].partCount -= rammedPartsLost; // Immediately adjust the parts count for more accurate tracking.
                        rammingInformation[vesselName].partCount -= rammingPartsLost;
                        // Update any other collisions that are waiting to count parts.
                        foreach (var tmpVesselName in rammingInformation[vesselName].targetInformation.Keys)
                            if (rammingInformation[tmpVesselName].targetInformation[vesselName].collisionDetected)
                                rammingInformation[tmpVesselName].targetInformation[vesselName].partCountJustPriorToCollision = rammingInformation[vesselName].partCount;
                        foreach (var tmpVesselName in rammingInformation[otherVesselName].targetInformation.Keys)
                            if (rammingInformation[tmpVesselName].targetInformation[otherVesselName].collisionDetected)
                                rammingInformation[tmpVesselName].targetInformation[otherVesselName].partCountJustPriorToCollision = rammingInformation[otherVesselName].partCount;

                        // Figure out who should be awarded the ram.
                        var rammingVessel = rammingInformation[vesselName].vesselName;
                        var rammedVessel = rammingInformation[otherVesselName].vesselName;
                        var headOn = false;
                        if (rammingInformation[vesselName].targetInformation[otherVesselName].ramming ^ rammingInformation[otherVesselName].targetInformation[vesselName].ramming) // Only one of the vessels was ramming.
                        {
                            if (!rammingInformation[vesselName].targetInformation[otherVesselName].ramming) // Switch who rammed who if the default is backwards.
                            {
                                rammingVessel = rammingInformation[otherVesselName].vesselName;
                                rammedVessel = rammingInformation[vesselName].vesselName;
                                var tmp = rammingPartsLost;
                                rammingPartsLost = rammedPartsLost;
                                rammedPartsLost = tmp;
                            }
                        }
                        else // Both or neither of the vessels were ramming.
                        {
                            if (rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM < headOnLimit && rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM < headOnLimit) // Head-on collision detected, both get awarded with ramming the other.
                            {
                                headOn = true;
                            }
                            else
                            {
                                if (rammingInformation[vesselName].targetInformation[otherVesselName].angleToCoM > rammingInformation[otherVesselName].targetInformation[vesselName].angleToCoM) // Other vessel had a better angleToCoM, so switch who rammed who.
                                {
                                    rammingVessel = rammingInformation[otherVesselName].vesselName;
                                    rammedVessel = rammingInformation[vesselName].vesselName;
                                    var tmp = rammingPartsLost;
                                    rammingPartsLost = rammedPartsLost;
                                    rammedPartsLost = tmp;
                                }
                            }
                        }

                        LogRammingVesselScore(rammingVessel, rammedVessel, rammedPartsLost, rammingPartsLost, headOn, true, true, rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetectedTime); // Log the ram.

                        // Set the collisionDetected flag to false, since we've now logged this collision. We set both so that the collision only gets logged once.
                        rammingInformation[vesselName].targetInformation[otherVesselName].collisionDetected = false;
                        rammingInformation[otherVesselName].targetInformation[vesselName].collisionDetected = false;
                    }
                }
            }
        }

        // Actually log the ram to various places. Note: vesselName and targetVesselName need to be those returned by the GetName() function to match the keys in Scores.
        public void LogRammingVesselScore(string rammingVesselName, string rammedVesselName, int rammedPartsLost, int rammingPartsLost, bool headOn, bool logToCompetitionStatus, bool logToDebug, double timeOfCollision)
        {
            if (logToCompetitionStatus)
            {
                if (!headOn)
                    competitionStatus.Add(rammedVesselName + " got RAMMED by " + rammingVesselName + " and lost " + rammedPartsLost + " parts (" + rammingVesselName + " lost " + rammingPartsLost + " parts).");
                else
                    competitionStatus.Add(rammedVesselName + " and " + rammingVesselName + " RAMMED each other and lost " + rammedPartsLost + " and " + rammingPartsLost + " parts, respectively.");
            }
            if (logToDebug)
            {
                if (!headOn)
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + rammedVesselName + " got RAMMED by " + rammingVesselName + " and lost " + rammedPartsLost + " parts (" + rammingVesselName + " lost " + rammingPartsLost + " parts).");
                else
                    Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + rammedVesselName + " and " + rammingVesselName + " RAMMED each other and lost " + rammedPartsLost + " and " + rammingPartsLost + " parts, respectively.");
            }

            // Log score information for the ramming vessel.
            LogRammingToScoreData(rammingVesselName, rammedVesselName, timeOfCollision, rammedPartsLost);
            // If it was a head-on, log scores for the rammed vessel too.
            if (headOn) LogRammingToScoreData(rammedVesselName, rammingVesselName, timeOfCollision, rammingPartsLost);
        }

        // Write ramming information to the Scores dictionary.
        private void LogRammingToScoreData(string rammingVesselName, string rammedVesselName, double timeOfCollision, int partsLost)
        {
            // Log attributes for the ramming vessel.
            if (!Scores.ContainsKey(rammingVesselName))
            {
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] Scores does not contain the key " + rammingVesselName);
                return;
            }
            var vData = Scores[rammingVesselName];
            vData.totalDamagedPartsDueToRamming += partsLost;
            var key = rammingVesselName + ":" + rammedVesselName;

            // Log attributes for the rammed vessel.
            if (!Scores.ContainsKey(rammedVesselName))
            {
                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "] Scores does not contain the key " + rammedVesselName);
                return;
            }
            var tData = Scores[rammedVesselName];
            tData.lastRammedTime = timeOfCollision;
            tData.lastPersonWhoRammedMe = rammingVesselName;
            tData.everyoneWhoRammedMe.Add(rammingVesselName);
            tData.everyoneWhoDamagedMe.Add(rammingVesselName);
            if (tData.rammingPartLossCounts.ContainsKey(rammingVesselName))
                tData.rammingPartLossCounts[rammingVesselName] += partsLost;
            else
                tData.rammingPartLossCounts.Add(rammingVesselName, partsLost);

            if (BDArmorySettings.REMOTE_LOGGING_ENABLED)
                BDAScoreService.Instance.TrackRammedParts(rammingVesselName, rammedVesselName, partsLost);
        }

        Dictionary<string, int> partsCheck;
        void CheckForMissingParts()
        {
            if (partsCheck == null)
            {
                partsCheck = new Dictionary<string, int>();
                foreach (var vesselName in rammingInformation.Keys)
                {
                    partsCheck.Add(vesselName, rammingInformation[vesselName].vessel.parts.Count);
                    if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: " + vesselName + " started with " + partsCheck[vesselName] + " parts.");
                }
            }
            foreach (var vesselName in rammingInformation.Keys)
            {
                var vessel = rammingInformation[vesselName].vessel;
                if (vessel != null)
                {
                    if (partsCheck[vesselName] != vessel.parts.Count)
                    {
                        if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: Parts Check: " + vesselName + " has lost " + (partsCheck[vesselName] - vessel.parts.Count) + " parts." + (vessel.parts.Count > 0 ? "" : " and is no more."));
                        partsCheck[vesselName] = vessel.parts.Count;
                    }
                }
                else if (partsCheck[vesselName] > 0)
                {
                    if (BDArmorySettings.DEBUG_RAMMING_LOGGING) Debug.Log("[BDArmory.BDACompetitionMode]: Ram logging: Parts Check: " + vesselName + " has been destroyed, losing " + partsCheck[vesselName] + " parts.");
                    partsCheck[vesselName] = 0;
                }
            }
        }

        // Main calling function to control ramming logging.
        private void LogRamming()
        {
            if (!competitionIsActive) return;
            if (rammingInformation == null) InitialiseRammingInformation();
            UpdateTimesToCPAs();
            CheckForPotentialCollisions();
            CheckForDamagedParts();
            if (BDArmorySettings.DEBUG_RAMMING_LOGGING) CheckForMissingParts(); // DEBUG
        }
        #endregion

        #region Tag
        public double lastTagUpdateTime;
        // Function to update tag
        private void UpdateTag(MissileFire mf, string key, int previousNumberCompetitive)
        {
            var updateTickLength = Planetarium.GetUniversalTime() - lastTagUpdateTime;
            var vData = Scores[key];
            if (alive.Contains(key)) // Vessel that is being updated is alive
            {
                // Update tag mode scoring
                if ((mf.Team.Name == "IT") && (previousNumberCompetitive > 1) && (!vData.landedState)) // Don't keep increasing score if we're the only ones left or we're landed
                {
                    vData.tagTotalTime += updateTickLength;
                    vData.tagScore += updateTickLength * previousNumberCompetitive * (previousNumberCompetitive - 1) / 5; // Rewards craft accruing time with more competitors
                }
                else if ((vData.tagIsIt) && (previousNumberCompetitive > 1) && (!vData.landedState)) // We need this in case the person who was "IT" died before the updating code ran
                {
                    mf.SetTeam(BDTeam.Get("IT"));
                    mf.vessel.ActionGroups.ToggleGroup(KM_dictAG[8]); // Trigger AG8 on becoming "IT"
                    vData.tagTotalTime += updateTickLength;
                    vData.tagScore += updateTickLength * previousNumberCompetitive * (previousNumberCompetitive - 1) / 5;
                }

                // If a vessel is NOT IT, make sure it's on the right team (this is important for continuous spawning
                if ((!startTag) && (!vData.tagIsIt) && (mf.Team.Name != "NO"))
                    mf.SetTeam(BDTeam.Get("NO"));

                // Update Tag Mode! If we're IT (or no one is IT yet) and we get hit, change everyone's teams and update the scoring
                double lastDamageTime = vData.LastDamageTime();
                // Be a little more lenient on detecting damage that didn't occur within the last update tick once tag has started, sometimes the update ticks take longer and damage isn't detected otherwise
                if (((startTag) && (Planetarium.GetUniversalTime() - lastDamageTime <= updateTickLength)) || ((vData.tagIsIt) && (Planetarium.GetUniversalTime() - lastDamageTime <= (updateTickLength * 5))))
                {
                    // We've started tag, we don't need the entry condition boolean anymore
                    if (startTag)
                        startTag = false;

                    // Update teams
                    var pilots = getAllPilots();
                    if (pilots.All(p => p.vessel.GetName() != vData.LastPersonWhoDamagedMe())) // IT was killed off by GM or BRB.
                        TagResetTeams();
                    else
                    {
                        foreach (var pilot in pilots)
                        {
                            if (!Scores.ContainsKey(pilot.vessel.GetName())) { Debug.Log("DEBUG 1 Scores doesn't contain " + pilot.vessel.GetName()); continue; } // How can this happen? This occurred for a vessel that got labelled as a Rover or Debris! Check that the vessel has the mf attached to the cockpit (e.g. JohnF's plane).
                            if (pilot.vessel.GetName() == vData.LastPersonWhoDamagedMe()) // Set the person who scored hits as "IT"
                            {
                                if (pilot.vessel.GetName() == key) Debug.Log("DEBUG " + key + " tagged themself with " + vData.LastDamageWasFrom() + " at " + vData.LastDamageTime().ToString("G1") + "!");
                                competitionStatus.Add(pilot.vessel.GetDisplayName() + " is IT!");
                                pilot.weaponManager.SetTeam(BDTeam.Get("IT"));
                                Scores[pilot.vessel.GetName()].tagIsIt = true;
                                Scores[pilot.vessel.GetName()].tagTimesIt++;
                                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[8]); // Trigger AG8 on becoming "IT"
                                Scores[pilot.vessel.GetName()].tagTotalTime += Math.Min(Planetarium.GetUniversalTime() - lastDamageTime, updateTickLength);
                                Scores[pilot.vessel.GetName()].tagScore += Math.Min(Planetarium.GetUniversalTime() - lastDamageTime, updateTickLength)
                                    * previousNumberCompetitive * (previousNumberCompetitive - 1) / 5;
                                Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + pilot.vessel.GetDisplayName() + " is IT!");
                            }
                            else // Everyone else is "NOT IT"
                            {
                                pilot.weaponManager.SetTeam(BDTeam.Get("NO"));
                                Scores[pilot.vessel.GetName()].tagIsIt = false;
                                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[9]); // Trigger AG9 on becoming "NOT IT"
                                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + pilot.vessel.GetDisplayName() + " is NOT IT!");
                            }
                        }
                        foreach (var pilot in pilots)
                            pilot.weaponManager.ForceScan(); // Update targets.
                    }
                }
            }
            else // Vessel that is being updated is dead
            {
                // If the player who was "IT" died declare a new "IT" player
                if (Scores[key].tagIsIt)
                {
                    Scores[key].tagIsIt = false;
                    var tagKillerIs = Scores[key].LastPersonWhoDamagedMe();
                    if ((Scores.ContainsKey(tagKillerIs)) && (tagKillerIs != "") && (alive.Contains(tagKillerIs))) // We have a killer who is alive
                    {
                        if (tagKillerIs == key) Debug.Log("DEBUG " + tagKillerIs + " tagged themself to death with " + vData.LastDamageWasFrom() + " at " + vData.LastDamageTime().ToString("G1") + "!");
                        Scores[tagKillerIs].tagIsIt = true;
                        Scores[tagKillerIs].tagTimesIt++;
                        Scores[tagKillerIs].tagTotalTime += Math.Min(Planetarium.GetUniversalTime() - Scores[key].LastDamageTime(), updateTickLength);
                        Scores[tagKillerIs].tagScore += Math.Min(Planetarium.GetUniversalTime() - Scores[key].LastDamageTime(), updateTickLength)
                            * previousNumberCompetitive * (previousNumberCompetitive - 1) / 5;
                        Debug.Log("[BDArmory.BDACompetitionMode:" + CompetitionID.ToString() + "]: " + key + " died, " + tagKillerIs + " is IT!"); // FIXME, killing the IT craft with the GM/BRB breaks this.
                        competitionStatus.Add(tagKillerIs + " is IT!");
                        foreach (var pilot in getAllPilots())
                            pilot.weaponManager.ForceScan(); // Update targets.
                    }
                    else // We don't have a killer who is alive, reset teams
                        TagResetTeams();
                }
                else
                {
                    if (Scores.ContainsKey(Scores[key].LastPersonWhoDamagedMe()) && Scores[Scores[key].LastPersonWhoDamagedMe()].tagIsIt) // "IT" player got a kill, let's log it
                    {
                        Scores[Scores[key].LastPersonWhoDamagedMe()].tagKillsWhileIt++;
                    }
                }
            }
        }

        void TagResetTeams()
        {
            char T = 'A';
            var pilots = getAllPilots();
            foreach (var pilot in pilots)
            {
                if (!Scores.ContainsKey(pilot.vessel.GetName())) { Debug.Log("DEBUG 2 Scores doesn't contain " + pilot.vessel.GetName()); continue; }
                pilot.weaponManager.SetTeam(BDTeam.Get(T.ToString()));
                Scores[pilot.vessel.GetName()].tagIsIt = false;
                pilot.vessel.ActionGroups.ToggleGroup(KM_dictAG[9]); // Trigger AG9 on becoming "NOT IT"
                T++;
            }
            foreach (var pilot in pilots)
                pilot.weaponManager.ForceScan(); // Update targets.
            startTag = true;
        }
        #endregion

        public void CheckMemoryUsage() // DEBUG
        {
            List<string> strings = new List<string>();
            strings.Add("System memory: " + SystemInfo.systemMemorySize + "MB");
            strings.Add("Reserved: " + UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024 + "MB");
            strings.Add("Allocated: " + UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024 + "MB");
            strings.Add("Mono heap: " + UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / 1024 / 1024 + "MB");
            strings.Add("Mono used: " + UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1024 / 1024 + "MB");
            strings.Add("plus unspecified runtime (native) memory.");
            Debug.Log("[BDArmory.BDACompetitionMode]: DEBUG Memory Usage: " + string.Join(", ", strings));
        }

        public void CheckNumbersOfThings() // DEBUG
        {
            List<string> strings = new List<string>();
            strings.Add("FlightGlobals.Vessels: " + FlightGlobals.Vessels.Count);
            strings.Add("Non-competitors to remove: " + nonCompetitorsToRemove.Count);
            strings.Add("EffectBehaviour<ParticleSystem>: " + EffectBehaviour.FindObjectsOfType<ParticleSystem>().Length);
            strings.Add("EffectBehaviour<KSPParticleEmitter>: " + EffectBehaviour.FindObjectsOfType<KSPParticleEmitter>().Length);
            strings.Add("KSPParticleEmitters: " + FindObjectsOfType<KSPParticleEmitter>().Length);
            strings.Add("KSPParticleEmitters including inactive: " + Resources.FindObjectsOfTypeAll(typeof(KSPParticleEmitter)).Length);
            Debug.Log("DEBUG " + string.Join(", ", strings));
            Dictionary<string, int> emitterNames = new Dictionary<string, int>();
            foreach (var pe in Resources.FindObjectsOfTypeAll(typeof(KSPParticleEmitter)).Cast<KSPParticleEmitter>())
            {
                if (!pe.isActiveAndEnabled)
                {
                    if (emitterNames.ContainsKey(pe.gameObject.name))
                        ++emitterNames[pe.gameObject.name];
                    else
                        emitterNames.Add(pe.gameObject.name, 1);
                }
            }
            Debug.Log("DEBUG inactive/disabled emitter names: " + string.Join(", ", emitterNames.Select(pe => pe.Key + ":" + pe.Value)));

            strings.Clear();
            strings.Add("Parts: " + FindObjectsOfType<Part>().Length + " active of " + Resources.FindObjectsOfTypeAll(typeof(Part)).Length);
            strings.Add("Vessels: " + FindObjectsOfType<Vessel>().Length + " active of " + Resources.FindObjectsOfTypeAll(typeof(Vessel)).Length);
            strings.Add("CometVessels: " + FindObjectsOfType<CometVessel>().Length);
            Debug.Log("DEBUG " + string.Join(", ", strings));
        }

        public void RunDebugChecks()
        {
            CheckMemoryUsage();
            CheckNumbersOfThings();
        }
    }
}
