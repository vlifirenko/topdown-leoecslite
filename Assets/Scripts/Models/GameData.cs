using System;
using CyberNinja.Models.Enums;

namespace CyberNinja.Models
{
    [Serializable]
    public class GameData
    {
        public EInputType inputType;
        public bool isMasterMute;
        public bool isMusicMute;
        public bool isEffectsMute;
        public bool isEnvironmentMute;
    }
}