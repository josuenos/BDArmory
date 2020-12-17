using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Bullets
{
    public class BulletInfo
    {
        public string name { get; private set; }
        public float caliber { get; private set; }
        public float bulletMass { get; private set; }
        public float bulletVelocity { get; private set; }
        public bool explosive { get; private set; }
        public float tntMass { get; private set; }
        public string fuzeType { get; private set; }
        public float apBulletMod { get; private set; }
        public int subProjectileCount { get; private set; }
        public string bulletDragTypeName { get; private set; }
        public string projectileColor { get; private set; }
        public string startColor { get; private set; }
        public bool fadeColor { get; private set; }

        public static BulletInfos bullets;

        public BulletInfo(string name, float caliber, float bulletVelocity, float bulletMass,
                          bool explosive, float tntMass, string fuzeType, float apBulletDmg, 
                          int subProjectileCount,string bulletDragTypeName, string projectileColor, string startColor, bool fadeColor)
        {
            this.name = name;
            this.caliber = caliber;
            this.bulletVelocity = bulletVelocity;
            this.bulletMass = bulletMass;
            this.explosive = explosive;
            this.tntMass = tntMass;
            this.fuzeType = fuzeType;
            this.apBulletMod = apBulletDmg;
            this.subProjectileCount = subProjectileCount;
            this.bulletDragTypeName = bulletDragTypeName;
            this.projectileColor = projectileColor;
            this.startColor = startColor;
            this.fadeColor = fadeColor;
        }

        public static void Load()
        {
            try
            {
                bullets = new BulletInfos();
                UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("BULLET");
                for (int i = 0; i < nodes.Length; i++)
                {
                    ConfigNode node = nodes[i].config;
                    bullets.Add(
                        new BulletInfo(
                        node.GetValue("name"),
                        float.Parse(node.GetValue("caliber")),
                        float.Parse(node.GetValue("bulletVelocity")),
                        float.Parse(node.GetValue("bulletMass")),
                        Convert.ToBoolean(node.GetValue("explosive")),
                        float.Parse(node.GetValue("tntMass")),
                        node.GetValue("fuzeType"),
                        float.Parse(node.GetValue("apBulletMod")),
                        int.Parse(node.GetValue("subProjectileCount")),
                        node.GetValue("bulletDragTypeName"),
                        node.GetValue("projectileColor"),
                        node.GetValue("startColor"),
                        Convert.ToBoolean(node.GetValue("fadeColor"))
                        )
                        );
                }
            }
            catch (Exception e)
            {
                Debug.Log("[BDArmory]: Error Loading Bullet Config | " + e.ToString());
            }
        }
    }

    public class BulletInfos : List<BulletInfo>
    {
        public BulletInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}
