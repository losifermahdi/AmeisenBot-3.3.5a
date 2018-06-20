﻿using Binarysharp.MemoryManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using AmeisenCore.Objects;


namespace AmeisenCore
{
    public class AmeisenCore
    {
        public static List<WoWExe> GetRunningWoWs()
        {
            List<WoWExe> wows = new List<WoWExe>();
            List<Process> processList = new List<Process>(Process.GetProcessesByName("Wow"));

            foreach (Process p in processList)
            {
                Console.WriteLine("Found WoW Process! PID: " + p.Id);

                WoWExe wow = new WoWExe();
                MemorySharp memorySharp = new MemorySharp(p);

                wow.characterName = memorySharp.Modules.MainModule.ReadString(AmeisenOffsets.WoWOffsets.playerName, Encoding.ASCII, 12); ;
                wow.process = p;
                wows.Add(wow);
            }

            return wows;
        }

        public static void MovePlayerToXYZ(float x, float y, float z)
        {
            if (GetMe().posX != x && GetMe().posY != y && GetMe().posZ != z)
            {
                AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Write<float>(AmeisenOffsets.WoWOffsets.ctmX, x);
                AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Write<float>(AmeisenOffsets.WoWOffsets.ctmY, y);
                AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Write<float>(AmeisenOffsets.WoWOffsets.ctmZ, z);
                AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Write<int>(AmeisenOffsets.WoWOffsets.ctmAction, 4);
            }
        }

        private static int GetMemLocByGUID(int guid)
        {
            int currentObjectManager = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.currentClientConnection);
            currentObjectManager = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((currentObjectManager + AmeisenOffsets.WoWOffsets.currentManagerOffset) - 0x400000);

            int activeObj = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((currentObjectManager + AmeisenOffsets.WoWOffsets.firstObjectOffset) - 0x400000);
            int objType = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((activeObj + AmeisenOffsets.WoWOffsets.gameobjectTypeOffset) - 0x400000);
            int objGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((activeObj + AmeisenOffsets.WoWOffsets.gameobjectGUIDOffset) - 0x400000);

            int count = 1;

            while (objType <= 7 && objType > 0)
            {
                objGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((activeObj + AmeisenOffsets.WoWOffsets.gameobjectGUIDOffset) - 0x400000);

                if (objGUID == guid)
                    return activeObj;

                activeObj = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((activeObj + AmeisenOffsets.WoWOffsets.nextObjectOffset) - 0x400000);
                objType = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((activeObj + AmeisenOffsets.WoWOffsets.gameobjectTypeOffset) - 0x400000);
                count++;
            }

            return 0;
        }

        private static string GetMobNameFromBase(int objBase)
        {
            int objName = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((objBase + 0x964) - 0x400000);
            objName = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((objName + 0x05C) - 0x400000);

            return AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.ReadString((objName) - 0x400000, Encoding.ASCII);
        }

        private static string GetPlayerNameFromGuid(int guid)
        {
            int mask, base_, shortGUID, testGUID, offset, current;

            mask = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((AmeisenOffsets.WoWOffsets.nameStore + AmeisenOffsets.WoWOffsets.nameMask) - 0x400000);
            base_ = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((AmeisenOffsets.WoWOffsets.nameStore + AmeisenOffsets.WoWOffsets.nameBase) - 0x400000);

            shortGUID = guid & 0xfffffff;
            offset = 12 * (mask & shortGUID);

            current = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((base_ + offset + 8) - 0x400000);
            offset = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((base_ + offset) - 0x400000);

            if ((current & 0x1) == 0x1) { return ""; }

            testGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((current) - 0x400000);

            while (testGUID != shortGUID)
            {
                current = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((current + offset + 4) - 0x400000);

                if ((current & 0x1) == 0x1) { return ""; }
                testGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((current) - 0x400000);
            }

            return AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.ReadString((current + AmeisenOffsets.WoWOffsets.nameString) - 0x400000, Encoding.ASCII);
        }

        public static Me GetMe()
        {
            Me me = (Me)ReadWoWObjectFromGUID(true, GetPlayerGUID());

            int targetGUID = GetTargetGUID();
            if (targetGUID != 0)
                me.target = (Target)ReadWoWObjectFromGUID(false, targetGUID);

            me.target.distance = Math.Sqrt((me.posX - me.target.posX) * (me.posX - me.target.posX) +
                                           (me.posY - me.target.posY) * (me.posY - me.target.posY) +
                                           (me.posZ - me.target.posZ) * (me.posZ - me.target.posZ));

            return me;
        }

        private static WoWObject ReadWoWObjectFromGUID(bool isMyself, int guid)
        {
            WoWObject wowObject;

            if (isMyself)
                wowObject = new Me();
            else
                wowObject = new Target();

            int targetBase = GetMemLocByGUID(guid);
            int targetBaseUnitFields = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBase + 0x8) - 0x400000);

            wowObject.guid = guid;
            wowObject.objectType = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBase + 0x14) - 0x400000);

            if (isMyself)
                wowObject.name = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.ReadString(AmeisenOffsets.WoWOffsets.playerName, Encoding.ASCII, 12);
            else
            {
                if (wowObject.objectType == 3)
                    wowObject.name = GetMobNameFromBase(targetBase);
                else if (wowObject.objectType == 4)
                    wowObject.name = GetPlayerNameFromGuid(guid);
                else
                    wowObject.name = "Unknown";
            }

            if (isMyself)
            {
                int playerbasex = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.playerBase);
                playerbasex = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((playerbasex + 0x34) - 0x400000);
                playerbasex = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((playerbasex + 0x24) - 0x400000);

                ((Me)wowObject).exp = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((playerbasex + 0x3794) - 0x400000);
                ((Me)wowObject).maxExp = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((playerbasex + 0x3798) - 0x400000);

                ((Me)wowObject).partymembers = new List<Target>();

                int leaderGUID = 0;

                try
                {
                    leaderGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((AmeisenOffsets.WoWOffsets.partyLeader) - 0x400000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                if (leaderGUID != 0)
                {
                    ((Me)wowObject).partymembers.Add(TryReadPartymember(leaderGUID, AmeisenOffsets.WoWOffsets.partyplayer1));
                    ((Me)wowObject).partymembers.Add(TryReadPartymember(leaderGUID, AmeisenOffsets.WoWOffsets.partyplayer2));
                    ((Me)wowObject).partymembers.Add(TryReadPartymember(leaderGUID, AmeisenOffsets.WoWOffsets.partyplayer3));
                    ((Me)wowObject).partymembers.Add(TryReadPartymember(leaderGUID, AmeisenOffsets.WoWOffsets.partyplayer4));
                }
            }

            wowObject.level = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x36 * 4)) - 0x400000);
            wowObject.health = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x18 * 4)) - 0x400000);
            wowObject.maxHealth = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x20 * 4)) - 0x400000);
            wowObject.energy = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x19 * 4)) - 0x400000);
            wowObject.maxEnergy = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x21 * 4)) - 0x400000);
            wowObject.summonedBy = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0xE * 4)) - 0x400000);

            wowObject.targetGUID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x12 * 4)) - 0x400000);
            wowObject.combatReach = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x42 * 4)) - 0x400000);
            wowObject.factionTemplate = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x37 * 4)) - 0x400000);
            wowObject.channelSpell = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((targetBaseUnitFields + (0x16 * 4)) - 0x400000);

            wowObject.posX = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<float>((targetBase + 0x798) - 0x400000);
            wowObject.posY = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<float>((targetBase + 0x79C) - 0x400000);
            wowObject.posZ = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<float>((targetBase + 0x7A0) - 0x400000);
            wowObject.rotation = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<float>((targetBase + 0x7A8) - 0x400000);

            wowObject.mapID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.mapID);
            wowObject.zoneID = AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.zoneID);

            return wowObject;
        }

        private static Target TryReadPartymember(int leaderGUID, int offset)
        {
            try
            {
                Target t = (Target)ReadWoWObjectFromGUID(false, AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>((offset) - 0x400000));

                t.distance = Math.Sqrt((GetMe().posX - t.posX) * (GetMe().posX - t.posX) +
                                       (GetMe().posY - t.posY) * (GetMe().posY - t.posY) +
                                       (GetMe().posZ - t.posZ) * (GetMe().posZ - t.posZ));

                if (t.guid == leaderGUID)
                    t.isPartyLeader = true;
                return t;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return new Target();
        }

        public static int GetPlayerGUID()
        {
            return AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.localPlayerGUID);
        }

        public static int GetTargetGUID()
        {
            return AmeisenManager.GetInstance().GetMemorySharp().Modules.MainModule.Read<int>(AmeisenOffsets.WoWOffsets.localTargetGUID);
        }
    }
}
