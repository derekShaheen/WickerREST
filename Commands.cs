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

namespace SkInterface
{
    public static class ActiveConfig
    {
        public static bool isRevealed = false;
        public static bool isPlaying = false;
    }

    public static class CommandActions
    {
        [CommandHandler("revealMap", "Test")]
        public static void RevealMapHttp(HttpListenerResponse response)
        {
            RevealMap(response);
        }

        [CommandHandler("ping", "Main")]
        public static void PingHttp(HttpListenerResponse response)
        {
            SkInterface.Instance.LogResponse(response, "Pong!");
        }

        [CommandHandler("pingg")]
        public static void PingHttp2(HttpListenerResponse response)
        {
            SkInterface.Instance.LogResponse(response, "Pong!");
        }

        [CommandHandler("ping")]
        public static void PingHttp3(HttpListenerResponse response)
        {
            //SkInterface.Instance.LogResponse(response, "Pong2!");
            SkInterface.Instance.LoggerInstance.Msg("Pong2!");
        }

        [CommandHandler("inputTest", "Main")]
        public static void InputTestHTTP(HttpListenerResponse response, string TestCase, string input = "test", string input2 = "testlols")
        {
            // Write output of each var
            StringBuilder responseContent = new StringBuilder();
            // Append output of each var to the response content
            responseContent.AppendLine("TestCase: '" + TestCase + "'");
            responseContent.AppendLine("input: '" + input + "'");
            responseContent.AppendLine("input2: '" + input2 + "'");

            // Now send the accumulated response content as one response
            SkInterface.Instance.LogResponse(response, responseContent.ToString());
        }

        [CommandHandler("secondInputTest", "Main")]
        public static void InputTest2HTTP(HttpListenerResponse response, string input)
        {
            SkInterface.Instance.LogResponse(response, "Received:" + " '" + input + "'");
        }

        [GameVariable("GameReadyToPlay")]
        public static string GetGameReadyToPlay()
        {
            return GameManager.gameReadyToPlay.ToString();
        }

        [GameVariable("IsGameLoaded")]
        public static string GetGameIsLoaded()
        {
            if (GameManager.Instance != null)
                return GameManager.Instance.isLoadedGame.ToString();

            return "False";
        }
        [GameVariable("GameFullyInitialized")]
        public static string GetGmeFullyInitialized()
        {
            return GameManager.gameFullyInitialized.ToString();
        }

        private static int relicCount = 0;

        [GameVariable("FoodProduced")]
        public static string GetRelicCount()
        {
            if (GameManager.Instance == null)
            {
                return "-1";
            }

            //if (GameManager.Instance != null && relicCount <= 0)
            //{
            //    var relicResources = UnityEngine.Object.FindObjectsOfType<Villager>();
            //    relicCount = relicResources.Length;
            //}

            return GameManager.Instance.villageStats.GetCurrentTrackedFoodProduced().Count.ToString();
        }


        public static bool IsPlaying { get => ActiveConfig.isPlaying; set => ActiveConfig.isPlaying = value; }
        public static bool IsRevealed { get => ActiveConfig.isRevealed; set => ActiveConfig.isRevealed = value; }

        private static void RevealMap(HttpListenerResponse response)
        {
            if (SkInterface.Instance == null)
            {
                return;
            }

            if (GameManager.Instance == null || !GameManager.gameFullyInitialized)
            {

                SkInterface.Instance.LogResponse(response, "Must be in-game to reveal map!");
                return;
            }

            if (GameManager.Instance != null && GameManager.gameFullyInitialized)
            {
                SkInterface.Instance.LogResponse(response, "Revealing map...");
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