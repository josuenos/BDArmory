using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.UI;

namespace BDArmory.Control
{
    // A serializable configuration for loading and saving the tournament state.
    [Serializable]
    public class RoundConfig : VesselSpawner.SpawnConfig
    {
        public RoundConfig(int round, int heat, bool completed, VesselSpawner.SpawnConfig config) : base(config) { this.round = round; this.heat = heat; this.completed = completed; SerializeTeams(); }
        public int round;
        public int heat;
        public bool completed;
        [SerializeField] private string serializedTeams;
        public void SerializeTeams()
        {
            if (teamsSpecific == null)
            {
                serializedTeams = null;
                return;
            }
            var teamStrings = new List<string>();
            foreach (var team in teamsSpecific)
            {
                teamStrings.Add("[" + string.Join(",", team) + "]");
            }
            serializedTeams = "[" + string.Join(",", teamStrings) + "]";
            craftFiles = null; // Avoid including the file list twice in the tournament.state file.
        }
        public void DeserializeTeams()
        {
            if (teamsSpecific == null) teamsSpecific = new List<List<string>>();
            else teamsSpecific.Clear();
            if (!string.IsNullOrEmpty(serializedTeams))
            {
                var teams = serializedTeams.Substring(1, serializedTeams.Length - 2).Split(new string[] { "],[" }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim(new char[] { '[', ']' })).ToList();
                foreach (var team in teams)
                {
                    var files = team.Split(',').ToList();
                    if (files.Count > 0)
                        teamsSpecific.Add(files);
                }
            }
            if (teamsSpecific.Count == 0) teamsSpecific = null;
        }
    }

    public enum TournamentType { FFA, Teams };

    [Serializable]
    public class TournamentState
    {
        public uint tournamentID;
        private List<string> craftFiles; // For FFA style tournaments.
        private List<List<string>> teamFiles; // For teams style tournaments.
        public int vesselCount;
        public int teamCount;
        public int teamsPerHeat;
        public int vesselsPerTeam;
        public bool fullTeams;
        public TournamentType tournamentType = TournamentType.FFA;
        [NonSerialized] public Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>> rounds; // <Round, <Heat, Crafts>>
        [NonSerialized] public Dictionary<int, HashSet<int>> completed = new Dictionary<int, HashSet<int>>();
        [NonSerialized] private List<Queue<string>> teamSpawnQueues = new List<Queue<string>>();
        private string message;

        /* Generate rounds and heats by shuffling the crafts list and breaking it into groups.
         * The last heat in a round will have fewer craft if the number of craft is not divisible by the number of vessels per heat.
         * The vessels per heat is limited to the number of available craft.
         */
        public bool Generate(string folder, int numberOfRounds, int vesselsPerHeat)
        {
            tournamentID = (uint)DateTime.UtcNow.Subtract(new DateTime(2020, 1, 1)).TotalSeconds;
            tournamentType = TournamentType.FFA;
            var abs_folder = Environment.CurrentDirectory + $"/AutoSpawn/{folder}";
            if (!Directory.Exists(abs_folder))
            {
                message = "Tournament folder (" + folder + ") containing craft files does not exist.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
                return false;
            }
            craftFiles = Directory.GetFiles(abs_folder).Where(f => f.EndsWith(".craft")).ToList();
            vesselCount = craftFiles.Count;
            int fullHeatCount;
            switch (vesselsPerHeat)
            {
                case 0: // Auto
                    var autoVesselsPerHeat = OptimiseVesselsPerHeat(craftFiles.Count);
                    vesselsPerHeat = autoVesselsPerHeat.Item1;
                    fullHeatCount = Mathf.CeilToInt(craftFiles.Count / vesselsPerHeat) - autoVesselsPerHeat.Item2;
                    break;
                case 1: // Unlimited (all vessels in one heat).
                    vesselsPerHeat = craftFiles.Count;
                    fullHeatCount = 1;
                    break;
                default:
                    vesselsPerHeat = Mathf.Clamp(vesselsPerHeat, 1, craftFiles.Count);
                    fullHeatCount = craftFiles.Count / vesselsPerHeat;
                    break;
            }
            rounds = new Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>>();
            Debug.Log("[BDATournament]: Generating " + numberOfRounds + " rounds for tournament " + tournamentID + ", each with " + vesselsPerHeat + " vessels per heat.");
            for (int roundIndex = 0; roundIndex < numberOfRounds; ++roundIndex)
            {
                craftFiles.Shuffle();
                int vesselsThisHeat = vesselsPerHeat;
                int count = 0;
                List<string> selectedFiles = craftFiles.Take(vesselsThisHeat).ToList();
                rounds.Add(rounds.Count, new Dictionary<int, VesselSpawner.SpawnConfig>());
                int heatIndex = 0;
                while (selectedFiles.Count > 0)
                {
                    rounds[roundIndex].Add(rounds[roundIndex].Count, new VesselSpawner.SpawnConfig(
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x,
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y,
                        BDArmorySettings.VESSEL_SPAWN_ALTITUDE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE,
                        BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED,
                        true, // Kill everything first.
                        BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS, // Assign teams.
                        0, // Number of teams.
                        null, // List of team numbers.
                        null, // List of List of teams' vessels.
                        null, // No folder, we're going to specify the craft files.
                        selectedFiles.ToList() // Add a copy of the craft files list.
                    ));
                    count += vesselsThisHeat;
                    vesselsThisHeat = heatIndex++ < fullHeatCount ? vesselsPerHeat : vesselsPerHeat - 1; // Take one less for the remaining heats to distribute the deficit of craft files.
                    selectedFiles = craftFiles.Skip(count).Take(vesselsThisHeat).ToList();
                }
            }
            teamFiles = null; // Clear the teams lists.
            return true;
        }

        /// <summary>
        /// Generate a tournament.state file for teams tournaments.
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="numberOfRounds"></param>
        /// <param name="teamsPerHeat"></param>
        /// <param name="vesselsPerTeam"></param>
        /// <param name="numberOfTeams"></param>
        /// <returns></returns>
        public bool Generate(string folder, int numberOfRounds, int teamsPerHeat, int vesselsPerTeam, int numberOfTeams)
        {
            tournamentID = (uint)DateTime.UtcNow.Subtract(new DateTime(2020, 1, 1)).TotalSeconds;
            tournamentType = TournamentType.Teams;
            var abs_folder = Environment.CurrentDirectory + $"/AutoSpawn/{folder}";
            if (!Directory.Exists(abs_folder))
            {
                message = "Tournament folder (" + folder + ") containing craft files or team folders does not exist.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
                return false;
            }
            if (numberOfTeams > 1) // Make teams from the files in the spawn folder.
            {
                craftFiles = Directory.GetFiles(abs_folder).Where(f => f.EndsWith(".craft")).ToList();
                if (craftFiles.Count < numberOfTeams)
                {
                    message = "Insufficient vessels in AutoSpawn" + (!string.IsNullOrEmpty(folder) ? "/" + folder : "") + " to make " + numberOfTeams + " teams.";
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDATournament]: " + message);
                    return false;
                }
                craftFiles.Shuffle();

                int numberPerTeam = craftFiles.Count / numberOfTeams;
                int residue = craftFiles.Count - numberPerTeam * numberOfTeams;
                teamFiles = new List<List<string>>();
                for (int teamCount = 0, count = 0; teamCount < numberOfTeams; ++teamCount)
                {
                    var toTake = numberPerTeam + (teamCount < residue ? 1 : 0);
                    teamFiles.Add(craftFiles.Skip(count).Take(toTake).ToList());
                    count += toTake;
                }
            }
            else // Make teams from the folders under the spawn folder.
            {
                var teamDirs = Directory.GetDirectories(Environment.CurrentDirectory + $"/AutoSpawn/{folder}");
                teamFiles = new List<List<string>>();
                foreach (var teamDir in teamDirs)
                {
                    var currentTeamFiles = Directory.GetFiles(teamDir).Where(f => f.EndsWith(".craft")).ToList();
                    if (currentTeamFiles.Count > 0)
                        teamFiles.Add(currentTeamFiles);
                }
                foreach (var team in teamFiles)
                    team.Shuffle();
                craftFiles = teamFiles.SelectMany(v => v).ToList();
            }
            vesselCount = craftFiles.Count;
            if (teamFiles.Count < 2)
            {
                message = "Insufficient " + (numberOfTeams == 1 ? "craft files" : "folders") + " in '" + folder + "' to generate a tournament.";
                if (BDACompetitionMode.Instance) BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
                return false;
            }
            teamCount = teamFiles.Count;
            teamsPerHeat = Mathf.Clamp(teamsPerHeat, 2, teamFiles.Count);
            this.teamsPerHeat = teamsPerHeat;
            this.vesselsPerTeam = vesselsPerTeam;
            fullTeams = BDArmorySettings.TOURNAMENT_FULL_TEAMS;
            var teamsIndex = Enumerable.Range(0, teamFiles.Count).ToList();
            teamSpawnQueues.Clear();

            int fullHeatCount = teamFiles.Count / teamsPerHeat;
            rounds = new Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>>();
            Debug.Log("[BDATournament]: Generating " + numberOfRounds + " rounds for tournament " + tournamentID + ", each with " + teamsPerHeat + " teams per heat.");
            for (int roundIndex = 0; roundIndex < numberOfRounds; ++roundIndex)
            {
                teamsIndex.Shuffle();
                int teamsThisHeat = teamsPerHeat;
                int count = 0;
                var selectedTeams = teamsIndex.Take(teamsThisHeat).ToList();
                var selectedCraft = SelectTeamCraft(selectedTeams, vesselsPerTeam);
                rounds.Add(rounds.Count, new Dictionary<int, VesselSpawner.SpawnConfig>());
                int heatIndex = 0;
                while (selectedTeams.Count > 0)
                {
                    rounds[roundIndex].Add(rounds[roundIndex].Count, new VesselSpawner.SpawnConfig(
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x,
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y,
                        BDArmorySettings.VESSEL_SPAWN_ALTITUDE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE,
                        BDArmorySettings.VESSEL_SPAWN_DISTANCE_TOGGLE,
                        BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED,
                        true, // Kill everything first.
                        BDArmorySettings.VESSEL_SPAWN_REASSIGN_TEAMS, // Assign teams.
                        numberOfTeams, // Number of teams indicator.
                        null, //selectedCraft.Select(c => c.Count).ToList(), // Not used here.
                        selectedCraft, // List of lists of vessels. For splitting specific vessels into specific teams.
                        null, // No folder, we're going to specify the craft files.
                        null //selectedCraft.SelectMany(c => c.ToList()).ToList() // Add a copy of the craft files list.
                    ));
                    count += teamsThisHeat;
                    teamsThisHeat = heatIndex++ < fullHeatCount ? teamsPerHeat : teamsPerHeat - 1; // Take one less for the remaining heats to distribute the deficit of teams.
                    selectedTeams = teamsIndex.Skip(count).Take(teamsThisHeat).ToList();
                    selectedCraft = SelectTeamCraft(selectedTeams, vesselsPerTeam);
                }
            }
            return true;
        }

        List<List<string>> SelectTeamCraft(List<int> selectedTeams, int vesselsPerTeam)
        {
            if (teamSpawnQueues.Count == 0) // Set up the spawn queues.
            {
                foreach (var teamIndex in teamFiles)
                    teamSpawnQueues.Add(new Queue<string>());
            }

            List<List<string>> selectedCraft = new List<List<string>>();
            List<string> currentTeam = new List<string>();
            foreach (var index in selectedTeams)
            {
                if (teamSpawnQueues[index].Count < vesselsPerTeam)
                {
                    // First append craft files that aren't already in the queue.
                    var craftToAdd = teamFiles[index].Where(c => !teamSpawnQueues[index].Contains(c)).ToList();
                    craftToAdd.Shuffle();
                    foreach (var craft in craftToAdd)
                    {
                        teamSpawnQueues[index].Enqueue(craft);
                    }
                    if (BDArmorySettings.TOURNAMENT_FULL_TEAMS)
                    {
                        // Then continue to fill the queue with craft files until we have enough.
                        while (teamSpawnQueues[index].Count < vesselsPerTeam)
                        {
                            craftToAdd = teamFiles[index].ToList();
                            craftToAdd.Shuffle();
                            foreach (var craft in craftToAdd)
                            {
                                teamSpawnQueues[index].Enqueue(craft);
                            }
                        }
                    }
                }
                currentTeam.Clear();
                while (currentTeam.Count < vesselsPerTeam && teamSpawnQueues[index].Count > 0)
                {
                    currentTeam.Add(teamSpawnQueues[index].Dequeue());
                }
                selectedCraft.Add(currentTeam.ToList());
            }
            return selectedCraft;
        }

        Tuple<int, int> OptimiseVesselsPerHeat(int count)
        {
            var options = new List<int> { 8, 7, 6 };
            foreach (var val in options)
            {
                if (count % val == 0)
                    return new Tuple<int, int>(val, 0);
            }
            var result = OptimiseVesselsPerHeat(count + 1);
            return new Tuple<int, int>(result.Item1, result.Item2 + 1);
        }

        public bool SaveState(string stateFile)
        {
            try
            {
                List<string> strings = new List<string>();

                strings.Add(JsonUtility.ToJson(this));
                foreach (var round in rounds.Keys)
                    foreach (var heat in rounds[round].Keys)
                        strings.Add(JsonUtility.ToJson(new RoundConfig(round, heat, completed.ContainsKey(round) && completed[round].Contains(heat), rounds[round][heat])));

                File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, stateFile), strings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool LoadState(string stateFile)
        {
            try
            {
                if (!File.Exists(Path.Combine(Environment.CurrentDirectory, stateFile))) return false;
                var strings = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, stateFile));
                var data = JsonUtility.FromJson<TournamentState>(strings[0]);
                tournamentID = data.tournamentID;
                vesselCount = data.vesselCount;
                teamCount = data.teamCount;
                teamsPerHeat = data.teamsPerHeat;
                vesselsPerTeam = data.vesselsPerTeam;
                fullTeams = data.fullTeams;
                tournamentType = data.tournamentType;
                rounds = new Dictionary<int, Dictionary<int, VesselSpawner.SpawnConfig>>();
                completed = new Dictionary<int, HashSet<int>>();
                for (int i = 1; i < strings.Length; ++i)
                {
                    if (strings[i].Length > 0)
                    {
                        var roundConfig = JsonUtility.FromJson<RoundConfig>(strings[i]);
                        roundConfig.DeserializeTeams();
                        if (!rounds.ContainsKey(roundConfig.round)) rounds.Add(roundConfig.round, new Dictionary<int, VesselSpawner.SpawnConfig>());
                        rounds[roundConfig.round].Add(roundConfig.heat, new VesselSpawner.SpawnConfig(
                            roundConfig.latitude,
                            roundConfig.longitude,
                            roundConfig.altitude,
                            roundConfig.distance,
                            roundConfig.absDistanceOrFactor,
                            roundConfig.easeInSpeed,
                            roundConfig.killEverythingFirst,
                            roundConfig.assignTeams,
                            roundConfig.numberOfTeams,
                            roundConfig.teamCounts == null || roundConfig.teamCounts.Count == 0 ? null : roundConfig.teamCounts,
                            roundConfig.teamsSpecific == null || roundConfig.teamsSpecific.Count == 0 ? null : roundConfig.teamsSpecific,
                            roundConfig.folder,
                            roundConfig.craftFiles
                        ));
                        if (roundConfig.completed)
                        {
                            if (!completed.ContainsKey(roundConfig.round)) completed.Add(roundConfig.round, new HashSet<int>());
                            completed[roundConfig.round].Add(roundConfig.heat);
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }
    }

    public enum TournamentStatus { Stopped, Running, Waiting, Completed };

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDATournament : MonoBehaviour
    {
        public static BDATournament Instance;

        #region Flags and Variables
        TournamentState tournamentState;
        string stateFile = "GameData/BDArmory/tournament.state";
        string message;
        private Coroutine runTournamentCoroutine;
        public TournamentStatus tournamentStatus = TournamentStatus.Stopped;
        public uint tournamentID = 0;
        public TournamentType tournamentType = TournamentType.FFA;
        public int numberOfRounds = 0;
        public int currentRound = 0;
        public int numberOfHeats = 0;
        public int currentHeat = 0;
        public int heatsRemaining = 0;
        public int vesselCount = 0;
        public int teamCount = 0;
        public int teamsPerHeat = 0;
        public int vesselsPerTeam = 0;
        public bool fullTeams = false;
        bool competitionStarted = false;
        #endregion

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        void Start()
        {
            StartCoroutine(LoadStateWhenReady());
        }

        IEnumerator LoadStateWhenReady()
        {
            while (BDACompetitionMode.Instance == null)
                yield return null;
            LoadTournamentState(); // Load the last state.
        }

        void OnDestroy()
        {
            StopTournament(); // Stop any running tournament.
            SaveTournamentState(); // Save the last state.
        }

        // Load tournament state from disk
        bool LoadTournamentState(string stateFile = "")
        {
            if (stateFile != "") this.stateFile = stateFile;
            tournamentState = new TournamentState();
            if (tournamentState.LoadState(this.stateFile))
            {
                message = "Tournament state loaded from " + this.stateFile;
                tournamentID = tournamentState.tournamentID;
                tournamentType = tournamentState.tournamentType;
                vesselCount = tournamentState.vesselCount;
                teamCount = tournamentState.teamCount;
                teamsPerHeat = tournamentState.teamsPerHeat;
                vesselsPerTeam = tournamentState.vesselsPerTeam;
                fullTeams = tournamentState.fullTeams;
                numberOfRounds = tournamentState.rounds.Count;
                numberOfHeats = numberOfRounds > 0 ? tournamentState.rounds[0].Count : 0;
                heatsRemaining = tournamentState.rounds.Select(r => r.Value.Count).Sum() - tournamentState.completed.Select(c => c.Value.Count).Sum();
            }
            else
                message = "Failed to load tournament state.";
            Debug.Log("[BDATournament]: " + message);
            // if (BDACompetitionMode.Instance != null)
            //     BDACompetitionMode.Instance.competitionStatus.Add(message);
            tournamentStatus = heatsRemaining > 0 ? TournamentStatus.Stopped : TournamentStatus.Completed;
            return true;
        }

        // Save tournament state to disk
        bool SaveTournamentState()
        {
            if (tournamentState.SaveState(stateFile))
                message = "Tournament state saved to " + stateFile;
            else
                message = "Failed to save tournament state.";
            Debug.Log("[BDATournament]: " + message);
            // if (BDACompetitionMode.Instance != null)
            //     BDACompetitionMode.Instance.competitionStatus.Add(message);
            return true;
        }

        public void SetupTournament(string folder, int rounds, int vesselsPerHeat = 0, int teamsPerHeat = 0, int vesselsPerTeam = 0, int numberOfTeams = 0, string stateFile = "")
        {
            if (stateFile != "") this.stateFile = stateFile;
            tournamentState = new TournamentState();
            if (numberOfTeams == 0) // FFA
            {
                if (!tournamentState.Generate(folder, rounds, vesselsPerHeat)) return;
            }
            else // Folders or random teams
            {
                if (!tournamentState.Generate(folder, rounds, teamsPerHeat, vesselsPerTeam, numberOfTeams)) return;
            }
            tournamentID = tournamentState.tournamentID;
            tournamentType = tournamentState.tournamentType;
            vesselCount = tournamentState.vesselCount;
            teamCount = tournamentState.teamCount;
            this.teamsPerHeat = tournamentState.teamsPerHeat;
            this.vesselsPerTeam = tournamentState.vesselsPerTeam;
            fullTeams = tournamentState.fullTeams;
            numberOfRounds = tournamentState.rounds.Count;
            numberOfHeats = numberOfRounds > 0 ? tournamentState.rounds[0].Count : 0;
            heatsRemaining = tournamentState.rounds.Select(r => r.Value.Count).Sum() - tournamentState.completed.Select(c => c.Value.Count).Sum();
            tournamentStatus = heatsRemaining > 0 ? TournamentStatus.Stopped : TournamentStatus.Completed;
            message = "Tournament generated for " + vesselCount + " craft found in AutoSpawn" + (folder == "" ? "" : "/" + folder);
            BDACompetitionMode.Instance.competitionStatus.Add(message);
            Debug.Log("[BDATournament]: " + message);
            SaveTournamentState();
        }

        public void RunTournament()
        {
            BDACompetitionMode.Instance.StopCompetition();
            VesselSpawner.Instance.CancelVesselSpawn();
            if (runTournamentCoroutine != null)
                StopCoroutine(runTournamentCoroutine);
            runTournamentCoroutine = StartCoroutine(RunTournamentCoroutine());
        }

        public void StopTournament()
        {
            if (runTournamentCoroutine != null)
            {
                StopCoroutine(runTournamentCoroutine);
                runTournamentCoroutine = null;
            }
            tournamentStatus = heatsRemaining > 0 ? TournamentStatus.Stopped : TournamentStatus.Completed;
        }

        IEnumerator RunTournamentCoroutine()
        {
            yield return new WaitForFixedUpdate();
            foreach (var roundIndex in tournamentState.rounds.Keys)
            {
                currentRound = roundIndex;
                foreach (var heatIndex in tournamentState.rounds[roundIndex].Keys)
                {
                    currentHeat = heatIndex;
                    if (tournamentState.completed.ContainsKey(roundIndex) && tournamentState.completed[roundIndex].Contains(heatIndex)) continue; // We've done that heat.

                    message = "Running heat " + heatIndex + " of round " + roundIndex + " of tournament " + tournamentState.tournamentID;
                    BDACompetitionMode.Instance.competitionStatus.Add(message);
                    Debug.Log("[BDATournament]: " + message);

                    int attempts = 0;
                    competitionStarted = false;
                    while (!competitionStarted && attempts++ < 3) // 3 attempts is plenty
                    {
                        tournamentStatus = TournamentStatus.Running;
                        yield return ExecuteHeat(roundIndex, heatIndex);
                        if (!competitionStarted)
                            switch (VesselSpawner.Instance.spawnFailureReason)
                            {
                                case VesselSpawner.SpawnFailureReason.None: // Successful spawning, but competition failed to start for some reason.
                                    BDACompetitionMode.Instance.competitionStatus.Add("Failed to start heat due to " + BDACompetitionMode.Instance.competitionStartFailureReason + ", trying again.");
                                    break;
                                case VesselSpawner.SpawnFailureReason.VesselLostParts: // Recoverable spawning failure.
                                case VesselSpawner.SpawnFailureReason.TimedOut: // Recoverable spawning failure.
                                    BDACompetitionMode.Instance.competitionStatus.Add("Failed to start heat due to " + VesselSpawner.Instance.spawnFailureReason + ", trying again.");
                                    break;
                                default: // Spawning is unrecoverable.
                                    BDACompetitionMode.Instance.competitionStatus.Add("Failed to start heat due to " + VesselSpawner.Instance.spawnFailureReason + ", aborting.");
                                    attempts = 3;
                                    break;
                            }
                    }
                    if (!competitionStarted)
                    {
                        Debug.Log("[BDATournament]: Failed to run heat, failure reasons: " + VesselSpawner.Instance.spawnFailureReason + ", " + BDACompetitionMode.Instance.competitionStartFailureReason);
                        tournamentStatus = TournamentStatus.Stopped;
                        yield break;
                    }

                    // Register the heat as completed.
                    if (!tournamentState.completed.ContainsKey(roundIndex)) tournamentState.completed.Add(roundIndex, new HashSet<int>());
                    tournamentState.completed[roundIndex].Add(heatIndex);
                    SaveTournamentState();
                    heatsRemaining = tournamentState.rounds.Select(r => r.Value.Count).Sum() - tournamentState.completed.Select(c => c.Value.Count).Sum();

                    if (heatsRemaining > 0)
                    {
                        // Wait a bit for any user action
                        tournamentStatus = TournamentStatus.Waiting;
                        double startTime = Planetarium.GetUniversalTime();
                        while ((Planetarium.GetUniversalTime() - startTime) < BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS)
                        {
                            BDACompetitionMode.Instance.competitionStatus.Add("Waiting " + (BDArmorySettings.TOURNAMENT_DELAY_BETWEEN_HEATS - (Planetarium.GetUniversalTime() - startTime)).ToString("0") + "s, then running next heat.");
                            yield return new WaitForSeconds(1);
                        }
                    }
                }
                message = "All heats in round " + roundIndex + " have been run.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
            }
            message = "All rounds in tournament " + tournamentState.tournamentID + " have been run.";
            BDACompetitionMode.Instance.competitionStatus.Add(message);
            Debug.Log("[BDATournament]: " + message);
            tournamentStatus = TournamentStatus.Completed;
        }

        IEnumerator ExecuteHeat(int roundIndex, int heatIndex)
        {
            VesselSpawner.Instance.SpawnAllVesselsOnce(tournamentState.rounds[roundIndex][heatIndex]);
            while (VesselSpawner.Instance.vesselsSpawning)
                yield return new WaitForFixedUpdate();
            if (!VesselSpawner.Instance.vesselSpawnSuccess)
            {
                tournamentStatus = TournamentStatus.Stopped;
                yield break;
            }
            yield return new WaitForFixedUpdate();

            // NOTE: runs in separate coroutine
            BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
            yield return new WaitForFixedUpdate(); // Give the competition start a frame to get going.

            // start timer coroutine for the duration specified in settings UI
            var duration = Core.BDArmorySettings.COMPETITION_DURATION * 60f;
            message = "Starting " + (duration > 0 ? "a " + duration.ToString("F0") + "s" : "an unlimited") + " duration competition.";
            Debug.Log("[BDATournament]: " + message);
            BDACompetitionMode.Instance.competitionStatus.Add(message);
            while (BDACompetitionMode.Instance.competitionStarting)
                yield return new WaitForFixedUpdate(); // Wait for the competition to actually start.
            if (!BDACompetitionMode.Instance.competitionIsActive)
            {
                var message = "Competition failed to start.";
                BDACompetitionMode.Instance.competitionStatus.Add(message);
                Debug.Log("[BDATournament]: " + message);
                tournamentStatus = TournamentStatus.Stopped;
                yield break;
            }
            competitionStarted = true;
            while (BDACompetitionMode.Instance.competitionIsActive) // Wait for the competition to finish.
                yield return new WaitForSeconds(1);
        }
    }
}