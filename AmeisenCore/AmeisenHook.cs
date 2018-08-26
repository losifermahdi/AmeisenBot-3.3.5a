﻿using AmeisenLogging;
using AmeisenUtilities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AmeisenCore
{
    /// <summary>
    /// Class that manages the hooking of WoW's EndScene
    ///
    /// !!! W.I.P !!!
    /// </summary>
    public class AmeisenHook
    {
        private static AmeisenHook instance;
        private static readonly object padlock = new object();

        public static AmeisenHook Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                        instance = new AmeisenHook();
                    return instance;
                }
            }
        }

        public bool isHooked = false;
        public bool isInjectionUsed = false;

        private uint codeCave;
        private uint codeCaveForInjection;
        private uint codeToExecute;
        private uint returnAdress;

        private uint endsceneReturnAddress;

        private byte[] originalEndscene = new byte[] { 0xB8, 0x51, 0xD7, 0xCA, 0x64 };

        private AmeisenHook()
        {
            Hook();
        }

        public void Hook()
        {
            if (AmeisenCore.Blackmagic.IsProcessOpen)
            {
                // Get D3D9 Endscene Pointer
                uint endscene = GetEndScene();

                // If WoW is already hooked, unhook it
                if (AmeisenCore.Blackmagic.ReadByte(endscene) == 0xE9)
                {
                    originalEndscene = new byte[] { 0xB8, 0x51, 0xD7, 0xCA, 0x64 };
                    DisposeHooking();
                }

                // If WoW is now/was unhooked, hook it
                if (AmeisenCore.Blackmagic.ReadByte(endscene) != 0xE9)
                {
                    uint endsceneHookOffset = 0x2;
                    endscene += endsceneHookOffset;

                    endsceneReturnAddress = endscene + 0x5;

                    //originalEndscene = AmeisenManager.Instance().GetBlackMagic().ReadBytes(endscene, 5);

                    codeToExecute = AmeisenCore.Blackmagic.AllocateMemory(4);
                    AmeisenCore.Blackmagic.WriteInt(codeToExecute, 0);

                    returnAdress = AmeisenCore.Blackmagic.AllocateMemory(4);
                    AmeisenCore.Blackmagic.WriteInt(returnAdress, 0);

                    codeCave = AmeisenCore.Blackmagic.AllocateMemory(32);
                    codeCaveForInjection = AmeisenCore.Blackmagic.AllocateMemory(64);

                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "EndScene at: " + endscene.ToString("X"), this);
                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "EndScene returning at: " + (endsceneReturnAddress).ToString("X"), this);
                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "CodeCave at:" + codeCave.ToString("X"), this);
                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "CodeCaveForInjection at:" + codeCaveForInjection.ToString("X"), this);
                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "CodeToExecute at:" + codeToExecute.ToString("X"), this);
                    AmeisenLogger.Instance.Log(LogLevel.DEBUG, "Original Endscene bytes: " + Utils.ByteArrayToString(originalEndscene), this);

                    AmeisenCore.Blackmagic.WriteBytes(codeCave, originalEndscene);

                    AmeisenCore.Blackmagic.Asm.Clear();
                    AmeisenCore.Blackmagic.Asm.AddLine("PUSHFD");
                    AmeisenCore.Blackmagic.Asm.AddLine("PUSHAD");
                    AmeisenCore.Blackmagic.Asm.AddLine("MOV EAX, [" + (codeToExecute) + "]");
                    AmeisenCore.Blackmagic.Asm.AddLine("TEST EAX, 1");
                    AmeisenCore.Blackmagic.Asm.AddLine("JE @out");

                    AmeisenCore.Blackmagic.Asm.AddLine("MOV EAX, " + (codeCaveForInjection));
                    AmeisenCore.Blackmagic.Asm.AddLine("CALL EAX");
                    AmeisenCore.Blackmagic.Asm.AddLine("MOV [" + (returnAdress) + "], EAX");

                    AmeisenCore.Blackmagic.Asm.AddLine("@out:");
                    AmeisenCore.Blackmagic.Asm.AddLine("MOV EDX, 0");
                    AmeisenCore.Blackmagic.Asm.AddLine("MOV [" + (codeToExecute) + "], EDX");

                    AmeisenCore.Blackmagic.Asm.AddLine("POPAD");
                    AmeisenCore.Blackmagic.Asm.AddLine("POPFD");
                    int asmLenght = AmeisenCore.Blackmagic.Asm.Assemble().Length;
                    AmeisenCore.Blackmagic.Asm.Inject(codeCave + 5);

                    AmeisenCore.Blackmagic.Asm.Clear();
                    AmeisenCore.Blackmagic.Asm.AddLine("JMP " + (endsceneReturnAddress));
                    AmeisenCore.Blackmagic.Asm.Inject((codeCave + (uint)asmLenght) + 5);

                    AmeisenCore.Blackmagic.Asm.Clear();
                    AmeisenCore.Blackmagic.Asm.AddLine("JMP " + (codeCave));
                    AmeisenCore.Blackmagic.Asm.Inject(endscene);
                }
                isHooked = true;
            }
        }

        public void DisposeHooking()
        {
            // Get D3D9 Endscene Pointer
            uint endscene = GetEndScene();

            uint endsceneHookOffset = 0x2;
            endscene += endsceneHookOffset;

            // Check if WoW is hooked
            if (AmeisenCore.Blackmagic.ReadByte(endscene) == 0xE9)
            {
                AmeisenCore.Blackmagic.WriteBytes(endscene, originalEndscene);

                AmeisenCore.Blackmagic.FreeMemory(codeCave);
                AmeisenCore.Blackmagic.FreeMemory(codeToExecute);
                AmeisenCore.Blackmagic.FreeMemory(codeCaveForInjection);
            }

            isHooked = false;
        }

        public byte[] InjectAndExecute(string[] asm)
        {
            while (isInjectionUsed)
                Thread.Sleep(50);

            isInjectionUsed = true;

            AmeisenCore.Blackmagic.WriteInt(codeToExecute, 1);
            AmeisenCore.Blackmagic.Asm.Clear();

            if (asm != null)
                foreach (string s in asm)
                    AmeisenCore.Blackmagic.Asm.AddLine(s);

            //AmeisenManager.Instance().GetBlackMagic().Asm.AddLine("JMP " + (endsceneReturnAddress));

            int asmLenght = AmeisenCore.Blackmagic.Asm.Assemble().Length;
            AmeisenCore.Blackmagic.Asm.Inject(codeCaveForInjection);

            while (AmeisenCore.Blackmagic.ReadInt(codeToExecute) > 0)
                Thread.Sleep(50);

            byte buffer = new Byte();
            List<byte> returnBytes = new List<byte>();

            try
            {
                uint dwAddress = AmeisenCore.Blackmagic.ReadUInt(returnAdress);

                buffer = AmeisenCore.Blackmagic.ReadByte(dwAddress);
                while (buffer != 0)
                {
                    returnBytes.Add(buffer);
                    dwAddress = dwAddress + 1;
                    buffer = AmeisenCore.Blackmagic.ReadByte(dwAddress);
                }
            }
            catch (Exception e) { AmeisenLogger.Instance.Log(LogLevel.DEBUG, "Crash at reading returnAddress: " + e.ToString(), this); }

            isInjectionUsed = false;
            return returnBytes.ToArray();
        }

        private uint GetEndScene()
        {
            uint pDevice = AmeisenCore.Blackmagic.ReadUInt(WoWOffsets.devicePtr1);
            uint pEnd = AmeisenCore.Blackmagic.ReadUInt(pDevice + WoWOffsets.devicePtr2);
            uint pScene = AmeisenCore.Blackmagic.ReadUInt(pEnd);
            uint endscene = AmeisenCore.Blackmagic.ReadUInt(pScene + WoWOffsets.endScene);
            return endscene;
        }
    }
}