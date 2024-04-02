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

namespace SkRESTClient
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
            SkRESTClient.Instance.LogResponse(response, "Pong!");
        }

        [CommandHandler("/ping2")]
        public static void PingHttp2(HttpListenerResponse response)
        {
            SkRESTClient.Instance.LogResponse(response, "Pong!");
        }

        [CommandHandler("/ping3")]
        public static void PingHttp3(HttpListenerResponse response)
        {
            SkRESTClient.Instance.LogResponse(response, "Pong!");
        }

        [CommandHandler("/inputTest")]
        public static void InputTestHTTP(HttpListenerResponse response, string input = "")
        {
            SkRESTClient.Instance.LogResponse(response, "Received:" + " '" + input + "'");
        }

        [GameVariable("GameManagerName")]
        public static string GetGameManagerName()
        {
            if (GameManager.Instance != null)
                return GameManager.Instance.name;

            return "GameManager not found!";
        }

        [GameVariable("GameManagerName2")]
        public static string GetGameManagerName2()
        {
            if (GameManager.Instance != null)
                return GameManager.Instance.name;

            return "GameManager not found!";
        }
        [GameVariable("GameManagerName3")]
        public static string GetGameManagerName3()
        {
            if (GameManager.Instance != null)
                return GameManager.Instance.name;

            return "GameManager not found!";
        }

        private static int relicCount = 0;

        [GameVariable("RelicCount")]
        public static string GetRelicCount()
        {
            if (GameManager.Instance == null)
            {
                relicCount = -1;
            }

            if (GameManager.Instance != null && relicCount <= 0)
            {
                var relicResources = UnityEngine.Object.FindObjectsOfType<RelicExtractionResource>();
                relicCount = relicResources.Length;
            }

            return relicCount.ToString();
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

                SkRESTClient.Instance.LogResponse(response, "Must be in-game to reveal map!");
                return;
            }

            if (GameManager.Instance != null && GameManager.gameFullyInitialized)
            {
                SkRESTClient.Instance.LogResponse(response, "Revealing map...");
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