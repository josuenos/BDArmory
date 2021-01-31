using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Modules;
using UnityEngine;

namespace BDArmory.Misc
{
    public struct MissileLaunchParams
    {
        public float minLaunchRange;
        public float maxLaunchRange;

        private float rtr;

        /// <summary>
        /// Gets the maximum no-escape range.
        /// </summary>
        /// <value>The max no-escape range.</value>
        public float rangeTr
        {
            get
            {
                return rtr;
            }
        }

        public MissileLaunchParams(float min, float max)
        {
            minLaunchRange = min;
            maxLaunchRange = max;
            rtr = (max + min) / 2;
        }

        /// <summary>
        /// Gets the dynamic launch parameters.
        /// </summary>
        /// <returns>The dynamic launch parameters.</returns>
        /// <param name="launcherVelocity">Launcher velocity.</param>
        /// <param name="targetVelocity">Target velocity.</param>
        public static MissileLaunchParams GetDynamicLaunchParams(MissileBase missile, Vector3 targetVelocity, Vector3 targetPosition)
        {
            Vector3 launcherVelocity = missile.vessel.Velocity();
            float launcherSpeed = (float)missile.vessel.srfSpeed;
            float minLaunchRange = missile.minStaticLaunchRange;
            float maxLaunchRange = missile.maxStaticLaunchRange;

            float missileActiveTime = 2f;

            float rangeAddMin = 0;
            float rangeAddMax = 0;
            float relSpeed;

            // Calculate relative speed
            Vector3 relV = targetVelocity - launcherVelocity;
            Vector3 vectorToTarget = targetPosition - missile.part.transform.position;
            Vector3 relVProjected = Vector3.Project(relV, vectorToTarget);
            relSpeed = -Mathf.Sign(Vector3.Dot(relVProjected, vectorToTarget)) * relVProjected.magnitude;

            // Basic time estimate for missile to drop and travel a safe distance from vessel assuming constant acceleration and firing vessel not accelerating
            if (missile.GetComponent<BDModularGuidance>() == null)
            {
                MissileLauncher ml = missile.GetComponent<MissileLauncher>();
                float maxMissileAccel = ml.thrust / missile.part.mass;
                float blastRadius = Mathf.Min(missile.GetBlastRadius(), 150f); // Allow missiles with absurd blast ranges to still be launched if desired
                missileActiveTime = Mathf.Min((missile.vessel.LandedOrSplashed ? 0f : missile.dropTime) + Mathf.Sqrt(2 * blastRadius / maxMissileAccel), 2f); // Clamp at 2s for now
            }

            float missileMaxRangeTime = 8f; // Placeholder value since this doesn't really matter much in BDA combat

            // Add to ranges
            rangeAddMin += relSpeed * missileActiveTime;
            rangeAddMax += relSpeed * missileMaxRangeTime;

            // Add altitude term to max
            double diffAlt = missile.vessel.altitude - FlightGlobals.getAltitudeAtPos(targetPosition);
            rangeAddMax += (float)diffAlt;

            float min = Mathf.Clamp(minLaunchRange + rangeAddMin, 0, BDArmorySettings.MAX_ENGAGEMENT_RANGE);
            float max = Mathf.Clamp(maxLaunchRange + rangeAddMax, min + 100, BDArmorySettings.MAX_ENGAGEMENT_RANGE);

            return new MissileLaunchParams(min, max);
        }
    }
}
