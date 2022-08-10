using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Extensions
{
    public static class VesselExtensions
    {
        public static HashSet<Vessel.Situations> InOrbitSituations = new HashSet<Vessel.Situations> { Vessel.Situations.ORBITING, Vessel.Situations.SUB_ORBITAL, Vessel.Situations.ESCAPING };

        public static bool InOrbit(this Vessel v)
        {
            if (v == null) return false;
            return InOrbitSituations.Contains(v.situation);
        }

        public static bool InVacuum(this Vessel v)
        {
            return v.atmDensity <= 0.001f;
        }

        public static bool IsUnderwater(this Vessel v)
        {
            if (!v) return false;
            return v.altitude < -20; //some boats sit slightly underwater, this is only for submersibles
        }

        public static Vector3d Velocity(this Vessel v)
        {
            try
            {
                if (v == null) return Vector3d.zero;
                if (!v.InOrbit())
                {
                    return v.srf_velocity;
                }
                else
                {
                    return v.obt_velocity;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BDArmory.VesselExtensions]: Exception thrown in Velocity: " + e.Message + "\n" + e.StackTrace);
                //return v.srf_velocity;
                return new Vector3d(0, 0, 0);
            }
        }

        public static double GetFutureAltitude(this Vessel vessel, float predictionTime = 10)
        {
            Vector3 futurePosition = vessel.CoM + vessel.Velocity() * predictionTime
                                                + 0.5f * vessel.acceleration_immediate * Mathf.Pow(predictionTime, 2);

            return GetRadarAltitudeAtPos(futurePosition);
        }

        public static Vector3 GetFuturePosition(this Vessel vessel, float predictionTime = 10)
        {
            return vessel.CoM + vessel.Velocity() * predictionTime + 0.5f * vessel.acceleration_immediate * Math.Pow(predictionTime, 2);
        }

        public static float GetRadarAltitudeAtPos(Vector3 position)
        {
            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(position);
            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(position);

            float radarAlt = Mathf.Clamp(
                (float)(FlightGlobals.currentMainBody.GetAltitude(position) -
                        FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos)), 0,
                (float)FlightGlobals.currentMainBody.GetAltitude(position));
            return radarAlt;
        }

        // Get a vessel's "radius".
        public static float GetRadius(this Vessel vessel, bool average = false)
        {
            // Get vessel size.
            Vector3 size = vessel.vesselSize;

            if (average) // Get the average of the dimensions.
                return (size.x + size.y + size.z) / 6f;

            // Get largest dimension.
            return Mathf.Max(Mathf.Max(size.x, size.y), size.z) / 2f;
        }
    }
}
