using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SkRest
{
    public static class ActiveConfig
    {
        public static bool isRevealed = false;
        public static bool isPlaying = false;
    }

    public static class CommandActions
    {
        [CommandHandler("/revealMap")]
        public static void RevealMapHttp(HttpListenerResponse response)
        {
            RevealMap(response);
        }

        [CommandHandler("/ping")]
        public static void PingHttp(HttpListenerResponse response)
        {
            SkRESTClient.Instance.LoggerInstance.Msg("Pong!");
            SkRESTClient.Instance.SendResponse(response, "Pong!");
        }

        public static bool IsPlaying { get => ActiveConfig.isPlaying; set => ActiveConfig.isPlaying = value; }
        public static bool IsRevealed { get => ActiveConfig.isRevealed; set => ActiveConfig.isRevealed = value; }

        private static void RevealMap(HttpListenerResponse response)
        {
            if (SkRESTClient.Instance == null)
            {
                return;
            }

            if (GameManager.Instance == null || !GameManager.gameFullyInitialized)
            {

                SkRESTClient.Instance.LoggerInstance.Msg("Must be in-game to reveal map!");
                SkRESTClient.Instance.SendResponse(response, "Must be in-game to reveal map!");
                return;
            }

            if (GameManager.Instance != null && GameManager.gameFullyInitialized)
            {
                SkRESTClient.Instance.LoggerInstance.Msg("Revealing map...");
                SkRESTClient.Instance.SendResponse(response, "Revealing map...");
                GameManager.Instance.cameraManager.fogOfWarEffect.mFog.enabled = false;
                ActiveConfig.isRevealed = true;

                var relicResources = UnityEngine.Object.FindObjectsOfType<RelicExtractionResource>();
                foreach (var relic in relicResources)
                {
                    relic.availableForExtraction = true;
                }
            }
        }

        [HarmonyPatch(typeof(FOWSystem), "IsExplored")]
        class Patch_FOWSystem_IsExplored
        {
            static bool Prefix(Vector3 pos, ref bool __result)
            {
                if (ActiveConfig.isRevealed)
                {
                    __result = true; // Set the result to true
                    return false; // Skip the original method
                }
                return true; // Continue with the original method
            }
        }
    }
}