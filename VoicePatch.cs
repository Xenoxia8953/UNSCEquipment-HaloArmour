using Aki.Reflection.Patching;
using Aki.Reflection.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;

namespace VoiceAdd
{
    public class VoicePatch : ModulePatch
    {
        private static Type _targetType;

        public VoicePatch()
        {
            _targetType = PatchConstants.EftTypes.Single(IsTargetType);
        }

        private bool IsTargetType(Type type)
        {
            return type.GetMethod("TakePhrasePath") != null;
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetType.GetMethod("TakePhrasePath");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            string json = new StreamReader("./user/mods/Tyr-UNSCEquipment/voices.json").ReadToEnd();
            Dictionary<string, string> voices = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            Dictionary<string, string> value = Traverse.Create(_targetType).Field<Dictionary<string, string>>("dictionary_0").Value;

            foreach (var voicename in voices.Keys)
            {
                voices.TryGetValue(voicename, out var voicebundle);
                if (!value.ContainsKey(voicename))
                    value.Add(voicename, voicebundle);
            }
        }
    }
}
