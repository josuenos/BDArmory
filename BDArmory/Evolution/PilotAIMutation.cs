using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Evolution
{
    public class PilotAIMutation : VariantMutation
    {
        const string moduleName = "BDModulePilotAI";
        public string paramName;
        public float value;
        private List<MutatedPart> mutatedParts = new List<MutatedPart>();

        public PilotAIMutation(string paramName, float value)
        {
            this.paramName = paramName;
            this.value = value;
        }

        public ConfigNode Apply(ConfigNode craft, VariantEngine engine)
        {
            ConfigNode mutatedCraft = craft.CreateCopy();
            List<ConfigNode> matchingNodes = engine.FindModuleNodes(mutatedCraft, moduleName);
            if( matchingNodes.Count == 1 )
            {
                var node = matchingNodes[0];
                float existingValue;
                float.TryParse(node.GetValue(paramName), out existingValue);
                if( engine.MutateNode(node, paramName, value) )
                {
                    ConfigNode partNode = engine.FindParentPart(mutatedCraft, node);
                    string partName = partNode.GetValue("part");
                    mutatedParts.Add(new MutatedPart(partName, moduleName, paramName, existingValue, value));
                }
            }
            else
            {
                Debug.Log("[BDArmory.PilotAIMutation]: Evolution PilotAIMutation wrong number of pilot modules");
            }
            return mutatedCraft;
        }

        public Variant GetVariant(string id, string name)
        {
            return new Variant(id, name, mutatedParts, "", 0);
        }
    }
}
