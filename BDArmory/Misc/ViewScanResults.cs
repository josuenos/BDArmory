using System.Collections.Generic;
using BDArmory.Modules;
using UnityEngine;

namespace BDArmory.Misc
{
    public struct ViewScanResults
    {
        #region Missiles
        public bool foundMissile;
        public bool foundHeatMissile;
        public bool foundRadarMissile;
        public bool foundAGM;
        public float missileThreatDistance;
        public List<IncomingMissile> incomingMissiles; // List of incoming missiles sorted by distance.
        #endregion

        #region Guns
        public bool firingAtMe;
        public float missDistance;
        public Vector3 threatPosition;
        public Vessel threatVessel;
        public MissileFire threatWeaponManager;
        #endregion
    }

    public struct IncomingMissile
    {
        public MissileBase.TargetingModes guidanceType; // Missile guidance type
        public float distance; // Missile distance
        public Vector3 position; // Missile position
        public Vessel vessel; // Missile vessel
        public MissileFire weaponManager; // WM of source vessel for regular missiles or WM of missile for modular missiles.
    }
}
