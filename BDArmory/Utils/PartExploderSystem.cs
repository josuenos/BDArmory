using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Utils
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class PartExploderSystem : MonoBehaviour
    {
        private static readonly HashSet<Part> ExplodingParts = new HashSet<Part>();
        private static List<Part> nowExploding = new List<Part>();

        public static void AddPartToExplode(Part p)
        {
            if (p != null)
            { ExplodingParts.Add(p); }
        }

        private void OnDestroy()
        {
            ExplodingParts.Clear();
        }

        public void Update()
        {
            if (ExplodingParts.Count == 0) return;

            do
            {
                ExplodingParts.Remove(null); // Clear out any null parts.
                nowExploding = ExplodingParts.Where(p => !ExplodingParts.Contains(p.parent)).ToList(); // Explode outer-most parts first to avoid creating new vessels needlessly.
                foreach (var part in nowExploding)
                {
                    part.explode();
                    ExplodingParts.Remove(part);
                }
            } while (ExplodingParts.Count > 0);
        }
    }
}
