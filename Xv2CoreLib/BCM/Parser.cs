﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using YAXLib;
using System.Collections;

namespace Xv2CoreLib.BCM
{
    public class Parser
    {
        string saveLocation { get; set; }
        public BCM_File bcmFile { get; private set; } = new BCM_File();
        byte[] rawBytes { get; set; }

        int totalTestCount = 0;


        public Parser(byte[] _rawBytes)
        {
            rawBytes = _rawBytes;

            ParseBcm();
        }

        public Parser (string location, bool writeXml)
        {
            saveLocation = location;
            rawBytes = File.ReadAllBytes(saveLocation);
            
            ParseBcm();

            if (writeXml)
            {
                YAXSerializer serializer = new YAXSerializer(typeof(BCM_File));
                serializer.SerializeToFile(bcmFile, saveLocation + ".xml");
            }

            
        }

        public BCM_File GetBcmFile()
        {
            return bcmFile;
        }

        private void ParseBcm()
        {
            int count = BitConverter.ToInt32(rawBytes, 8);
            int offset = BitConverter.ToInt32(rawBytes, 12);

            if(offset > 0)
            {
                bcmFile.BCMEntries = ParseChild(offset, true);
            }
            
            /*
            if(totalTestCount != count)
            {
                Console.WriteLine(String.Format("Count mismatch! (Declared: {0}, Parsed: {1})\n" +
                    "Probably caused by some manual edits done to this file. You can ignore this error, but make sure you have a back up!", count, totalTestCount));
                Console.ReadLine();
            }
            */
        }

        private List<BCM_Entry> ParseChild(int offset, bool ignoreLoop, int parentOffset = 16)
        {
            //This will parse all children of the calling method (so Siblings)
            //If an Entry has children of its own, it will call this method again (recursively)

            List<BCM_Entry> bcmEntries = new List<BCM_Entry>();
            int index = 0;

            while(true)
            {
                bcmEntries.Add(ParseBcmEntry(offset, parentOffset, GetBcmIndex(offset), ignoreLoop));
                
                int ChildOffset = BitConverter.ToInt32(rawBytes, offset + 52);
                if (ChildOffset != 0 && bcmEntries[index].LoopAsChild == null)
                {
                    bcmEntries[index].BCMEntries = ParseChild(ChildOffset, false, offset);

                }

                if(BitConverter.ToInt32(rawBytes, offset + 48) != 0 && bcmEntries[index].LoopAsSibling == null)
                {
                    offset = BitConverter.ToInt32(rawBytes, offset + 48);
                }
                else
                {
                    break;
                }

                index++;
            }

            return bcmEntries;
        }

        private BCM_Entry ParseBcmEntry (int offset, int parentOffset, int index, bool ignoreLoop)
        {
            totalTestCount++;

            BCM_Entry bcmEntry = new BCM_Entry()
            {
                Index = index.ToString(),
                I_00 = BitConverter.ToUInt32(rawBytes, offset + 0),
                DirectionalInput = (DirectionalInput)BitConverter.ToUInt32(rawBytes, offset + 4),
                ButtonInput = (ButtonInput)BitConverter.ToUInt32(rawBytes, offset + 8),
                HoldDownConditions = BitConverter.ToUInt32(rawBytes, offset + 12),
                OpponentSizeConditions = BitConverter.ToUInt32(rawBytes, offset + 16),
                MinimumLoopDuration = BitConverter.ToUInt16(rawBytes, offset + 20),
                MaximumLoopDuration = BitConverter.ToUInt16(rawBytes, offset + 22),
                PrimaryActivatorConditions = (PrimaryActivatorConditions)BitConverter.ToUInt32(rawBytes, offset + 24),
                ActivatorState = (ActivatorState)BitConverter.ToUInt32(rawBytes, offset + 28),
                BacEntryPrimary = BitConverter.ToInt16(rawBytes, offset + 32),
                BacEntryCharge = BitConverter.ToInt16(rawBytes, offset + 34),
                I_36 = BitConverter.ToInt16(rawBytes, offset + 36),
                BacEntryUserConnect = BitConverter.ToInt16(rawBytes, offset + 38),
                BacEntryVictimConnect = BitConverter.ToInt16(rawBytes, offset + 40),
                BacEntryAirborne = BitConverter.ToInt16(rawBytes, offset + 42),
                BacEntryTargetingOverride = BitConverter.ToUInt16(rawBytes, offset + 44),
                RandomFlag = BitConverter.ToUInt16(rawBytes, offset + 46),
                I_64 = BitConverter.ToUInt32(rawBytes, offset + 64),
                I_68 = BitConverter.ToUInt32(rawBytes, offset + 68),
                I_72 = BitConverter.ToUInt32(rawBytes, offset + 72),
                ReceiverLinkID = BitConverter.ToUInt32(rawBytes, offset + 76),
                I_80 = BitConverter.ToUInt32(rawBytes, offset + 80),
                StaminaCost = BitConverter.ToUInt32(rawBytes, offset + 84),
                I_88 = BitConverter.ToUInt32(rawBytes, offset + 88),
                KiRequired = BitConverter.ToUInt32(rawBytes, offset + 92),
                HealthRequired = BitConverter.ToSingle(rawBytes, offset + 96),
                TransStage = BitConverter.ToInt16(rawBytes, offset + 100),
                CusAura = BitConverter.ToInt16(rawBytes, offset + 102),
                I_104 = BitConverter.ToUInt32(rawBytes, offset + 104),
                I_108 = BitConverter.ToUInt32(rawBytes, offset + 108),

            };

            //Loop Check
            int sibling = (BitConverter.ToInt32(rawBytes, offset + 48) != 0) ? GetBcmIndex(BitConverter.ToInt32(rawBytes, offset + 48)) : -1;
            int child = (BitConverter.ToInt32(rawBytes, offset + 52) != 0) ? GetBcmIndex(BitConverter.ToInt32(rawBytes, offset + 52)) : -1;

            

            if(SiblingLoop(offset) && sibling != -1)
            {
                bcmEntry.LoopAsSibling = sibling.ToString();
            }
            if (ChildrenLoop(offset) && ignoreLoop == false && child != -1)
            {
                bcmEntry.LoopAsChild = child.ToString();
            }

            return bcmEntry;
        }

        private int GetBcmIndex(int offset)
        {
            if(offset != 0)
            {
                offset -= -16;
                return offset / 112;
            }
            else
            {
                return -1;
            }
        }
        

        //Helpers
        private bool ChildrenLoop(int offset)
        {
            int parentID = GetBcmIndex(offset);
            int childOffset = BitConverter.ToInt32(rawBytes, offset + 52);

            if(GetBcmIndex(BitConverter.ToInt32(rawBytes, childOffset + 56)) == parentID)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool SiblingLoop(int offset)
        {
            int parentID = GetBcmIndex(BitConverter.ToInt32(rawBytes, offset + 56));
            int siblingOffset = BitConverter.ToInt32(rawBytes, offset + 48);

            if (GetBcmIndex(BitConverter.ToInt32(rawBytes, siblingOffset + 56)) == parentID)
            {
                return false;
            }
            else
            {
                return true;
            }
        }



        

    }
}
