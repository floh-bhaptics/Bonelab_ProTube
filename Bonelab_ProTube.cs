﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using System.Text;
using System.Text.Json;

[assembly: MelonInfo(typeof(Bonelab_ProTube.Bonelab_ProTube), "Bonelab_ProTube", "1.0.0", "Florian Fahrenberger")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace Bonelab_ProTube
{
    public class Bonelab_ProTube : MelonMod
    {
        public static string configPath = Directory.GetCurrentDirectory() + "\\UserData\\";
        public static bool dualWield = false;

        public override void OnInitializeMelon()
        {
            InitializeProTube();
        }

        public static void saveChannel(string channelName, string proTubeName)
        {
            string fileName = configPath + channelName + ".pro";
            File.WriteAllText(fileName, proTubeName, Encoding.UTF8);
        }

        public static string readChannel(string channelName)
        {
            string fileName = configPath + channelName + ".pro";
            if (!File.Exists(fileName)) return "";
            return File.ReadAllText(fileName, Encoding.UTF8);
        }

        public static void dualWieldSort()
        {
            //MelonLogger.Msg("Channels: " + ForceTubeVRInterface.ListChannels());
            JsonDocument doc = JsonDocument.Parse(ForceTubeVRInterface.ListChannels());
            JsonElement pistol1 = doc.RootElement.GetProperty("channels").GetProperty("pistol1");
            JsonElement pistol2 = doc.RootElement.GetProperty("channels").GetProperty("pistol2");
            if ((pistol1.GetArrayLength() > 0) && (pistol2.GetArrayLength() > 0))
            {
                dualWield = true;
                MelonLogger.Msg("Two ProTube devices detected, player is dual wielding.");
                if ((readChannel("pistol1") == "") || (readChannel("pistol2") == ""))
                {
                    MelonLogger.Msg("No configuration files found, saving current right and left hand pistols.");
                    saveChannel("pistol1", pistol1[0].GetProperty("name").ToString());
                    saveChannel("pistol2", pistol2[0].GetProperty("name").ToString());
                }
                else
                {
                    string rightHand = readChannel("pistol1");
                    string leftHand = readChannel("pistol2");
                    MelonLogger.Msg("Found and loaded configuration. Right hand: " + rightHand + ", Left hand: " + leftHand);
                    ForceTubeVRInterface.ClearChannel(4);
                    ForceTubeVRInterface.ClearChannel(5);
                    ForceTubeVRInterface.AddToChannel(4, rightHand);
                    ForceTubeVRInterface.AddToChannel(5, leftHand);
                }
            }
        }

        private async void InitializeProTube()
        {
            MelonLogger.Msg("Initializing ProTube gear...");
            await ForceTubeVRInterface.InitAsync(true);
            Thread.Sleep(10000);
            dualWieldSort();
        }

        [HarmonyPatch(typeof(Il2CppSLZ.Props.Weapons.Gun), "Fire", new Type[] { })]
        public class bhaptics_FireGun
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppSLZ.Props.Weapons.Gun __instance)
            {
                bool rightHanded = false;
                bool twoHanded = false;
                bool supportHand = __instance._isSlideGrabbed;

                if (__instance == null) return;
                if (__instance.triggerGrip == null) return;
                if (__instance.triggerGrip.attachedHands == null) return;
                try { if (__instance.AmmoCount() <= 0) return; }
                catch (NullReferenceException) { MelonLogger.Msg("NullReference in AmmoCount."); return; }
                twoHanded = (__instance.triggerGrip.attachedHands.Count > 1);
                
                foreach (var myHand in __instance.triggerGrip.attachedHands)
                {
                    if (myHand.handedness == Il2CppSLZ.Handedness.RIGHT) rightHanded = true;
                }

                if (__instance.otherGrips != null)
                {
                    foreach (var myGrip in __instance.otherGrips)
                    {
                        if (myGrip.attachedHands.Count > 0)
                        {
                            foreach (var myHand in myGrip.attachedHands)
                            {
                                if ((myHand.handedness == Il2CppSLZ.Handedness.LEFT) && (rightHanded)) supportHand = true;
                                if ((myHand.handedness == Il2CppSLZ.Handedness.RIGHT) && (!rightHanded)) supportHand = true;
                            }
                        }
                    }
                }

                //tactsuitVr.LOG("Kickforce: " + __instance.kickForce.ToString());
                float intensity = Mathf.Min(Mathf.Max(__instance.kickForce / 12.0f, 1.0f), 0.5f);
                byte kickPower = (byte)(int)(intensity * 255);
                ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                if (!rightHanded) myChannel = ForceTubeVRChannel.pistol2;
                ForceTubeVRInterface.Kick(kickPower, myChannel);
            }
        }


        [HarmonyPatch(typeof(Il2CppSLZ.Interaction.InventorySlotReceiver), "OnHandGrab", new Type[] { typeof(Il2CppSLZ.Interaction.Hand) })]
        public class bhaptics_SlotGrab
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppSLZ.Interaction.InventorySlotReceiver __instance, Il2CppSLZ.Interaction.Hand hand)
            {
                if (__instance.isInUIMode) return;
                if (hand == null) return;
                bool rightHand = (hand.handedness == Il2CppSLZ.Handedness.RIGHT);
                ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                if (!rightHand) myChannel = ForceTubeVRChannel.pistol2;
                ForceTubeVRInterface.Rumble(200, 100f, myChannel);
            }
        }

        [HarmonyPatch(typeof(Il2CppSLZ.Interaction.InventorySlotReceiver), "OnHandDrop", new Type[] { typeof(Il2CppSLZ.Interaction.IGrippable) })]
        public class bhaptics_SlotInsert
        {
            [HarmonyPostfix]
            public static void Postfix(Il2CppSLZ.Interaction.InventorySlotReceiver __instance, Il2CppSLZ.Interaction.IGrippable host)
            {
                if (__instance == null) return;
                if (__instance.isInUIMode) return;
                if (host == null) return;
                Il2CppSLZ.Interaction.Hand hand = host.GetLastHand();
                if (hand == null) return;
                bool rightHand = (hand.handedness == Il2CppSLZ.Handedness.RIGHT);
                ForceTubeVRChannel myChannel = ForceTubeVRChannel.pistol1;
                if (!rightHand) myChannel = ForceTubeVRChannel.pistol2;
                ForceTubeVRInterface.Rumble(200, 100f, myChannel);
            }
        }
        
    }
}
