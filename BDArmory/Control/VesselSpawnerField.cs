using System;
using System.IO;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace BDArmory.Control
{
    public class SpawnLocation
    {
        public string name;
        public Vector2d location;

        public SpawnLocation(string _name, Vector2d _location) { name = _name; location = _location; }
        public override string ToString() { return name + ", " + location.ToString("G6"); }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class VesselSpawnerField : Attribute
    {
        public VesselSpawnerField() { }

        static List<SpawnLocation> defaultLocations = new List<SpawnLocation>{
            new SpawnLocation("KSC", new Vector2d(-0.04762, -74.8593)),
            new SpawnLocation("Inland KSC", new Vector2d(20.5939, -146.567)),
            new SpawnLocation("Desert Runway", new Vector2d(-6.44958, -144.038)),
            new SpawnLocation("Kurgan's spot", new Vector2d(-28.4595, -9.15156)),
            new SpawnLocation("Alpine Lake", new Vector2d(-23.48, 119.83)),
            new SpawnLocation("Big Canyon", new Vector2d(6.97865, -170.804)),
            new SpawnLocation("Bowl 1", new Vector2d(35.6559, -77.4941)),
            new SpawnLocation("Bowl 2", new Vector2d(3.8744, -78.0039)),
            new SpawnLocation("Bowl 3", new Vector2d(0.268284, -80.5195)),
            new SpawnLocation("Bowl 4", new Vector2d(-2.962, 179.91)),
            new SpawnLocation("Bowl 5", new Vector2d(47.16, 134.08)),
            new SpawnLocation("Canyon", new Vector2d(-52.7592, -4.71081)),
            new SpawnLocation("Colorado", new Vector2d(41.715, 82.29)),
            new SpawnLocation("Crater Isle", new Vector2d(8.159, 179.65)),
            new SpawnLocation("Crater Lake", new Vector2d(-18.86, 66.47)),
            new SpawnLocation("Crater Sea", new Vector2d(7.213, -177.34)),
            new SpawnLocation("East Peninsula", new Vector2d(-1.57, -39.12)),
            new SpawnLocation("Great Lake", new Vector2d(-31.958, 81.654)),
            new SpawnLocation("Half-pipe", new Vector2d(-21.1388, 72.6437)),
            new SpawnLocation("Ice field", new Vector2d(80.3343, -32.0119)),
            new SpawnLocation("Kermau-Sur-Mer", new Vector2d(33.911, -172.26)),
            new SpawnLocation("Land Bridge", new Vector2d(-48.055, 13.33)),
            new SpawnLocation("Lonely Mt", new Vector2d(24.48, -116.444)),
            new SpawnLocation("Manley Delta", new Vector2d(39.0705, -136.193)),
            new SpawnLocation("Manley Valley", new Vector2d(45.6, -137.3)),
            new SpawnLocation("Marshlands", new Vector2d(16.83, -162.813)),
            new SpawnLocation("Mountain Bowl", new Vector2d(21.772, -112.569)),
            new SpawnLocation("Mtn. Springs", new Vector2d(30.6516, -40.6589)),
            new SpawnLocation("Oasis", new Vector2d(10.5383, -121.837)),
            new SpawnLocation("Oyster Bay", new Vector2d(8.342, 85.613)),
            new SpawnLocation("Penninsula", new Vector2d(-1.2664, -106.896)),
            new SpawnLocation("Pyramids", new Vector2d(-6.4743, -141.662)),
            new SpawnLocation("Src of deNile", new Vector2d(28.8112, -134.795)),
            new SpawnLocation("Suez", new Vector2d(10.955, -96.9358)),
            new SpawnLocation("The Scar", new Vector2d(16.88, 50.48)),
            new SpawnLocation("Western Approach", new Vector2d(0.2, -84.26)),
            new SpawnLocation("White Cliffs", new Vector2d(25.689, -144.14)),
        };

        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(VesselSpawner.spawnLocationsCfg);
            if (fileNode == null)
                fileNode = new ConfigNode();

            if (!fileNode.HasNode("Config"))
                fileNode.AddNode("Config");

            ConfigNode settings = fileNode.GetNode("Config");
            foreach (var field in typeof(VesselSpawner).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (field == null || !field.IsDefined(typeof(VesselSpawnerField), false)) continue;
                if (field.Name == "spawnLocations") continue; // We'll do the spawn locations separately.
                var fieldValue = field.GetValue(null);
                settings.SetValue(field.Name, field.GetValue(null).ToString(), true);
            }

            if (!fileNode.HasNode("BDASpawnLocations"))
                fileNode.AddNode("BDASpawnLocations");

            ConfigNode spawnLocations = fileNode.GetNode("BDASpawnLocations");

            spawnLocations.ClearValues();
            foreach (var spawnLocation in VesselSpawner.spawnLocations)
                spawnLocations.AddValue("LOCATION", spawnLocation.ToString());

            if (!Directory.GetParent(VesselSpawner.spawnLocationsCfg).Exists)
            { Directory.GetParent(VesselSpawner.spawnLocationsCfg).Create(); }
            var success = fileNode.Save(VesselSpawner.spawnLocationsCfg);
            if (success && File.Exists(VesselSpawner.oldSpawnLocationsCfg)) // Remove the old settings if it exists and the new settings were saved.
            { File.Delete(VesselSpawner.oldSpawnLocationsCfg); }
        }

        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(VesselSpawner.spawnLocationsCfg);
            if (fileNode == null)
            {
                fileNode = ConfigNode.Load(VesselSpawner.oldSpawnLocationsCfg); // Try the old location.
            }
            VesselSpawner.spawnLocations = new List<SpawnLocation>();
            if (fileNode != null)
            {
                if (fileNode.HasNode("Config"))
                {
                    ConfigNode settings = fileNode.GetNode("Config");
                    foreach (var field in typeof(VesselSpawner).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (field == null || !field.IsDefined(typeof(VesselSpawnerField), false)) continue;
                        if (field.Name == "spawnLocations") continue; // We'll do the spawn locations separately.
                        if (!settings.HasValue(field.Name)) continue;
                        object parsedValue = ParseValue(field.FieldType, settings.GetValue(field.Name));
                        if (parsedValue != null)
                        {
                            field.SetValue(null, parsedValue);
                        }
                    }
                }

                if (fileNode.HasNode("BDASpawnLocations"))
                {
                    ConfigNode settings = fileNode.GetNode("BDASpawnLocations");
                    foreach (var spawnLocation in settings.GetValues("LOCATION"))
                    {
                        var parsedValue = (SpawnLocation)ParseValue(typeof(SpawnLocation), spawnLocation);
                        if (parsedValue != null)
                        {
                            VesselSpawner.spawnLocations.Add(parsedValue);
                        }
                    }
                }
            }

            // Add defaults if they're missing and we're not instructed not to.
            if (VesselSpawner.UpdateSpawnLocations)
            {
                foreach (var location in defaultLocations.ToList())
                    if (!VesselSpawner.spawnLocations.Select(l => l.name).ToList().Contains(location.name))
                        VesselSpawner.spawnLocations.Add(location);
            }
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (type == typeof(string))
                {
                    return value;
                }
                else if (type == typeof(bool))
                {
                    return Boolean.Parse(value);
                }
                else if (type == typeof(Vector2d))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    if (strings.Length == 2)
                    {
                        double x = double.Parse(strings[0]);
                        double y = double.Parse(strings[1]);
                        return new Vector2d(x, y);
                    }
                }
                else if (type == typeof(SpawnLocation))
                {
                    var parts = value.Split(new char[] { ',' }, 2);
                    if (parts.Length == 2)
                    {
                        var name = (string)ParseValue(typeof(string), parts[0]);
                        var location = (Vector2d)ParseValue(typeof(Vector2d), parts[1]);
                        if (name != null && location != null)
                            return new SpawnLocation(name, location);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.LogError("[BDArmory.VesselSpawnerField]: Failed to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
