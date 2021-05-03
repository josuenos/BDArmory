using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BDArmory.Bullets
{
    public class RocketInfo
    {
        public string name { get; private set; }
        public float rocketMass { get; private set; }
        public float caliber { get; private set; }
        public float thrust { get; private set; }
        public float thrustTime { get; private set; }
        public bool shaped { get; private set; }
        public bool flak { get; private set; }
        public bool EMP { get; private set; }
        public bool choker { get; private set; }
        public bool gravitic { get; private set; }
        public bool impulse { get; private set; }
        public bool explosive { get; private set; }
        public float tntMass { get; private set; }
        public int subProjectileCount { get; private set; }
        public float thrustDeviation { get; private set; }
        public string rocketModelPath { get; private set; }

        public static RocketInfos rockets;
        public static HashSet<string> rocketNames;
        public static RocketInfo defaultRocket;

        public RocketInfo(string name, float rocketMass, float caliber, float thrust, float thrustTime,
                         bool shaped, bool flak, bool EMP, bool choker, bool gravitic, bool impulse, bool explosive, float tntMass, int subProjectileCount, float thrustDeviation, string rocketModelPath)
        {
            this.name = name;
            this.rocketMass = rocketMass;
            this.caliber = caliber;
            this.thrust = thrust;
            this.thrustTime = thrustTime;
            this.shaped = shaped;
            this.flak = flak;
            this.EMP = EMP;
            this.choker = choker;
            this.gravitic = gravitic;
            this.impulse = impulse;
            this.explosive = explosive;
            this.tntMass = tntMass;
            this.subProjectileCount = subProjectileCount;
            this.thrustDeviation = thrustDeviation;
            this.rocketModelPath = rocketModelPath;
        }

        public static void Load()
        {
            if (rockets != null) return; // Only load them once on startup.
            rockets = new RocketInfos();
            if (rocketNames == null) rocketNames = new HashSet<string>();
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("ROCKET");
            ConfigNode node;

            // First locate BDA's default rocket definition so we can fill in missing fields.
            if (defaultRocket == null)
                for (int i = 0; i < nodes.Length; ++i)
                {
                    if (nodes[i].parent.name != "BD_Rockets") continue; // Ignore other config files.
                    node = nodes[i].config;
                    if (!node.HasValue("name") || (string)ParseField(nodes[i].config, "name", typeof(string)) != "def") continue; // Ignore other configs.
                    Debug.Log("[BDArmory.RocketInfo]: Parsing default rocket definition from " + nodes[i].parent.name);
                    defaultRocket = new RocketInfo(
                        "def",
                        (float)ParseField(node, "rocketMass", typeof(float)),
                        (float)ParseField(node, "caliber", typeof(float)),
                        (float)ParseField(node, "thrust", typeof(float)),
                        (float)ParseField(node, "thrustTime", typeof(float)),
                        (bool)ParseField(node, "shaped", typeof(bool)),
                        (bool)ParseField(node, "flak", typeof(bool)),
                        (bool)ParseField(node, "EMP", typeof(bool)),
                        (bool)ParseField(node, "choker", typeof(bool)),
                        (bool)ParseField(node, "gravitic", typeof(bool)),
                        (bool)ParseField(node, "impulse", typeof(bool)),
                        (bool)ParseField(node, "explosive", typeof(bool)),
                        (float)ParseField(node, "tntMass", typeof(float)),
                        (int)ParseField(node, "subProjectileCount", typeof(int)),
                        (float)ParseField(node, "thrustDeviation", typeof(float)),
                        (string)ParseField(node, "rocketModelPath", typeof(string))
                    );
                    rockets.Add(defaultRocket);
                    rocketNames.Add("def");
                    break;
                }
            if (defaultRocket == null) throw new ArgumentException("Failed to find BDArmory's default rocket definition.", "defaultRocket");

            // Now add in the rest of the rockets.
            for (int i = 0; i < nodes.Length; i++)
            {
                string name_ = "";
                try
                {
                    node = nodes[i].config;
                    name_ = (string)ParseField(node, "name", typeof(string));
                    if (rocketNames.Contains(name_)) // Avoid duplicates.
                    {
                        if (nodes[i].parent.name != "BD_Rockets" || name_ != "def") // Don't report the default bullet definition as a duplicate.
                            Debug.LogError("[BDArmory.RocketInfo]: Rocket definition " + name_ + " from " + nodes[i].parent.name + " already exists, skipping.");
                        continue;
                    }
                    Debug.Log("[BDArmory.RocketInfo]: Parsing definition of rocket " + name_ + " from " + nodes[i].parent.name);
                    rockets.Add(
                        new RocketInfo(
                            name_,
                            (float)ParseField(node, "rocketMass", typeof(float)),
                            (float)ParseField(node, "caliber", typeof(float)),
                            (float)ParseField(node, "thrust", typeof(float)),
                            (float)ParseField(node, "thrustTime", typeof(float)),
                            (bool)ParseField(node, "shaped", typeof(bool)),
                            (bool)ParseField(node, "flak", typeof(bool)),
                            (bool)ParseField(node, "EMP", typeof(bool)),
                            (bool)ParseField(node, "choker", typeof(bool)),
                            (bool)ParseField(node, "gravitic", typeof(bool)),
                            (bool)ParseField(node, "impulse", typeof(bool)),
                            (bool)ParseField(node, "explosive", typeof(bool)),
                            (float)ParseField(node, "tntMass", typeof(float)),
                            (int)ParseField(node, "subProjectileCount", typeof(int)),
                            (float)ParseField(node, "thrustDeviation", typeof(float)),
                            (string)ParseField(node, "rocketModelPath", typeof(string))
                        )
                    );
                    rocketNames.Add(name_);
                }
                catch (Exception e)
                {
                    Debug.LogError("[BDArmory.RocketInfo]: Error Loading Rocket Config '" + name_ + "' | " + e.ToString());
                }
            }
        }

        private static object ParseField(ConfigNode node, string field, Type type)
        {
            try
            {
                if (!node.HasValue(field))
                    throw new ArgumentNullException(field, "Field '" + field + "' is missing.");
                var value = node.GetValue(field);
                try
                {
                    if (type == typeof(string))
                    { return value; }
                    else if (type == typeof(bool))
                    { return bool.Parse(value); }
                    else if (type == typeof(int))
                    { return int.Parse(value); }
                    else if (type == typeof(float))
                    { return float.Parse(value); }
                    else
                    { throw new ArgumentException("Invalid type specified."); }
                }
                catch (Exception e)
                { throw new ArgumentException("Field '" + field + "': '" + value + "' could not be parsed as '" + type.ToString() + "' | " + e.ToString(), field); }
            }
            catch (Exception e)
            {
                if (defaultRocket != null)
                {
                    // Give a warning about the missing or invalid value, then use the default value using reflection to find the field.
                    var defaultValue = typeof(RocketInfo).GetProperty(field, BindingFlags.Public | BindingFlags.Instance).GetValue(defaultRocket);
                    Debug.LogError("[BDArmory.RocketInfo]: Using default value of " + defaultValue.ToString() + " for " + field + " | " + e.ToString());
                    return defaultValue;
                }
                else
                    throw;
            }
        }
    }

    public class RocketInfos : List<RocketInfo>
    {
        public RocketInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}
