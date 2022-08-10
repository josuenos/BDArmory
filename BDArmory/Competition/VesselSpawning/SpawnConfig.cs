using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Competition.VesselSpawning
{
    /// <summary>
    /// Configuration for spawning groups of vessels.
    /// 
    /// Note:
    /// This is currently partially specific to SpawnAllVesselsOnce and SpawnVesselsContinuosly.
    /// TODO: Make this generic and make CircularSpawnConfig a derived class of this.
    /// </summary>
    [Serializable]
    public class SpawnConfig
    {
        public SpawnConfig(int worldIndex, double latitude, double longitude, double altitude, float distance, bool absDistanceOrFactor, float easeInSpeed = 1f, bool killEverythingFirst = true, bool assignTeams = true, int numberOfTeams = 0, List<int> teamCounts = null, List<List<string>> teamsSpecific = null, string folder = "", List<string> craftFiles = null)
        {
            this.worldIndex = worldIndex;
            this.latitude = latitude;
            this.longitude = longitude;
            this.altitude = altitude;
            this.distance = distance;
            this.absDistanceOrFactor = absDistanceOrFactor;
            this.easeInSpeed = easeInSpeed;
            this.killEverythingFirst = killEverythingFirst;
            this.assignTeams = assignTeams;
            this.numberOfTeams = numberOfTeams;
            this.teamCounts = teamCounts; if (teamCounts != null) this.numberOfTeams = this.teamCounts.Count;
            this.teamsSpecific = teamsSpecific;
            this.folder = folder ?? "";
            this.craftFiles = craftFiles;
        }
        public SpawnConfig(SpawnConfig other)
        {
            this.worldIndex = other.worldIndex;
            this.latitude = other.latitude;
            this.longitude = other.longitude;
            this.altitude = other.altitude;
            this.distance = other.distance;
            this.absDistanceOrFactor = other.absDistanceOrFactor;
            this.easeInSpeed = other.easeInSpeed;
            this.killEverythingFirst = other.killEverythingFirst;
            this.assignTeams = other.assignTeams;
            this.numberOfTeams = other.numberOfTeams;
            this.teamCounts = other.teamCounts;
            this.teamsSpecific = other.teamsSpecific;
            this.folder = other.folder;
            this.craftFiles = other.craftFiles?.ToList();
        }
        public int worldIndex;
        public double latitude;
        public double longitude;
        public double altitude;
        public float distance;
        public bool absDistanceOrFactor; // If true, the distance value is used as-is, otherwise it is used as a factor giving the actual distance: (N+1)*distance, where N is the number of vessels.
        public float easeInSpeed;
        public bool killEverythingFirst = true;
        public bool assignTeams = true;
        public int numberOfTeams = 0; // Number of teams (or FFA, Folders or Inf). For evenly (as possible) splitting vessels into teams.
        public List<int> teamCounts; // List of team numbers. For unevenly splitting vessels into teams based on their order in the tournament state file for the round. E.g., when spawning from folders.
        public List<List<string>> teamsSpecific; // Dictionary of vessels and teams. For splitting specific vessels into specific teams.
        public string folder = "";
        public List<string> craftFiles = null;
    }

    /// <summary>
    /// Configuration for spawning individual vessels. 
    /// </summary>
    [Serializable]
    public struct VesselSpawnConfig
    {
        public string craftURL; // The craft file.
        public Vector3 position; // World-space coordinates (x,y,z) to place the vessel once spawned (before adjusting for terrain altitude).
        public Vector3 direction; // Direction to point the plane horizontally (i.e., heading).
        public float altitude; // Altitude above terrain / water to adjust spawning position to.
        public float pitch; // Pitch if spawning airborne.
        public bool airborne; // Whether the vessel should be spawned in an airborne configuration or not.
        public int teamIndex;
        public bool reuseURLVesselName; // Reuse the vesselName for the same craftURL (for continuous spawning).
        public VesselSpawnConfig(string craftURL, Vector3 position, Vector3 direction, float altitude, float pitch, bool airborne, int teamIndex = 0, bool reuseURLVesselName = false)
        {
            this.craftURL = craftURL;
            this.position = position;
            this.direction = direction;
            this.altitude = altitude;
            this.pitch = pitch;
            this.airborne = airborne;
            this.teamIndex = teamIndex;
            this.reuseURLVesselName = reuseURLVesselName;
        }
    }
}