using UnityEngine;

namespace CyberNinja.Config
{
    [CreateAssetMenu(menuName = "Config/Unit", fileName = "Unit")]
    public class UnitConfig : ScriptableObject
    {
        public LayerMask mouseStickLookLayer;
        [Space]
        public float maxDamage = 100;
        public float abilityInputBlockTime = 0.1f;
        [Space]
        public float minLayerHit = 0.2f;
        [Space]
        public float layerWeightLerp = 0.3f;
        public float layerWeightTreshold = 0.02f;
        [Space]
        public float defaultMoveSpeed = 5f;
    }
}