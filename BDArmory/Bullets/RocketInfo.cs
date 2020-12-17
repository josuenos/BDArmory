using System;
using System.Collections.Generic;
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
        public bool explosive { get; private set; }
        public float tntMass { get; private set; }
        public int subProjectileCount { get; private set; }
        public float thrustDeviation { get; private set; }
        public string rocketModelPath { get; private set; }

        public static RocketInfos rockets;

        public RocketInfo(string name, float rocketMass, float caliber, float thrust, float thrustTime,
                          bool shaped, bool flak, bool explosive, float tntMass, int subProjectileCount, float thrustDeviation, string rocketModelPath)

        {
            this.name = name;
            this.rocketMass = rocketMass;
            this.caliber = caliber;
            this.thrust = thrust;
            this.thrustTime = thrustTime;
            this.shaped = shaped;
            this.flak = flak;
            this.explosive = explosive;
            this.tntMass = tntMass;
            this.subProjectileCount = subProjectileCount;
            this.thrustDeviation = thrustDeviation;
            this.rocketModelPath = rocketModelPath;
        }

        public static void Load()
        {
            try
            {
                rockets = new RocketInfos();
                UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("ROCKET");
                for (int i = 0; i < nodes.Length; i++)
                {
                    ConfigNode node = nodes[i].config;
                    rockets.Add(
                        new RocketInfo(
                        node.GetValue("name"),
                        float.Parse(node.GetValue("rocketMass")),
                        float.Parse(node.GetValue("caliber")),
                        float.Parse(node.GetValue("thrust")),
                        float.Parse(node.GetValue("thrustTime")),
                        Convert.ToBoolean(node.GetValue("shaped")),
                        Convert.ToBoolean(node.GetValue("flak")),
                        Convert.ToBoolean(node.GetValue("explosive")),
                        float.Parse(node.GetValue("tntMass")),
                        int.Parse(node.GetValue("subProjectileCount")),
                        float.Parse(node.GetValue("thrustDeviation")),
                        node.GetValue("rocketModelPath")
                        )
                        );
                }
            }
            catch (Exception e)
            {
                Debug.Log("[BDArmory]: Error Loading Rocket Config | " + e.ToString());
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
