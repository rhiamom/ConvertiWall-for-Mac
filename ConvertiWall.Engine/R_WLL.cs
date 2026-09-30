/***************************************************************************
 *   Copyright (C) 2008 by Mootilda                                        *
 *   http://Mootilda.ModTheSims2.com                                       *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 *   This program is distributed in the hope that it will be useful,       *
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         *
 *   GNU General Public License for more details.                          *
 *                                                                         *
 *   You should have received a copy of the GNU General Public License     *
 *   along with this program; if not, write to the                         *
 *   Free Software Foundation, Inc.,                                       *
 *   59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.             *
 ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using SimPe.Interfaces.Files;


namespace LotExpander
{
    public class R_WLL
    {
        bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information

        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private const int iLeadingZeros = 64;
        private int iBlockVersion;
        private int iHeaderSize = 83;
        private int iCount;
        private const int iLotTilesPerNeighborhoodTile = 10;

        public R_WLL(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            for (int i = 0; i < iLeadingZeros; i++)
            {
                byte bDummy = BR.ReadByte();
                Debug.Assert(bDummy == 0);
            }
            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x8A84D7B0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WLL: Block ID");

            iBlockVersion = BR.ReadInt32();
            Debug.Assert(iBlockVersion == 1);   // ToDo: Determine whether other versions are known and handled correctly

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WLL: Block Name Length");
            string sBlockName = BR.ReadString();
            if (sBlockName != "cWallLayer")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WLL: Block Name");
            int iIndex = iLeadingZeros + 4 /* uBlockID */ + 4 /* iBlockVersion */ + 1 /* bBlockNameLen */ + bBlockNameLen;

            Debug.Assert(iIndex == iHeaderSize);

            iCount = BR.ReadInt32();
        }

        public int WallCount
        {
            get
            {
                return iCount;
            }
        }

        private bool KnownWallID(uint uWallID)
        {
            if ((uWallID == 1)      // normal wall
             || (uWallID == 2)      // picket rail fence
             || (uWallID == 3)      // attic wall
             || (uWallID == 4)      // non-rendered deck skirt
             || (uWallID == 16)     // deck skirt (redwood)
             || (uWallID == 23)     // foundation wall (brick)
             || (uWallID == 24)     // deck skirt (minimal)
             || (uWallID == 26)     // deck aged wood fence arch
             || (uWallID == 29)     // pool wall
             || (uWallID == 300)    // normal wall (OFB or later)
             || (uWallID == 301)    // screen wood (OFB or later)
            )
                return true;
            return false;
        }

        // Find any unknown Wall IDs and add them to the list.
        public void AddWallIDs(ref uint[] uaWallIDs)
        {
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BR.ReadBytes(iHeaderSize);
            int iItemCount = BR.ReadInt32();

            for (int i = 0; i < iItemCount; i++)
            {
                // Matches Wall Reference Number in WGRA List of Walls
                uint uWallRef = BR.ReadUInt32();

                // Either matches Wall ID in Walls.txt
                // or GUID (no idea what the GUID means at this time)
                uint uWallID = BR.ReadUInt32();
                // if (!KnownWallID(uWallID))
                {
                    int j = 0;
                    for (; j < uaWallIDs.Length; j++)
                    {
                        if (uaWallIDs[j] == uWallID)
                            break;
                    }
                    if (j == uaWallIDs.Length)
                    {
                        Array.Resize<uint>(ref uaWallIDs, uaWallIDs.Length + 1);
                        uaWallIDs[j] = uWallID;
                        // Debug.Print("New WLL({0}): Ref={1:X8} ID={2:X8}", i, uWallRef, uWallID);
                    }
                }

                short iPatternID1 = BR.ReadInt16(); // Wall pattern on one side of the wall; corresponds to SMAP
                short iPatternID2 = BR.ReadInt16(); // Wall pattern on the other side of the wall; corresponds to SMAP

                if (Test_PrintDebugInfo)
                    Debug.Print("WLL({0}): Ref={1:X8} ID={2:X8} {3} {4}", i, uWallRef, uWallID, iPatternID1, iPatternID2);
            }
        }

        // Results of testing:
        // iWallNumber = 1;     // Works:   foundation walls  -> normal walls
                                // Crashes! redwood deck      -> foundation walls
        // iWallNumber = 3;     // Crashes! normal walls      -> attic walls; requires roof?
        // iWallNumber = 4;     // Works:   normal walls      -> non-rendered deck skirt
        // iWallNumber = 16;    // Crashes! normal walls      -> redwood walls
        // iWallNumber = 23;    // Works:   normal walls      -> foundation walls
                                // Crashes! redwood deck      -> foundation walls
        // iWallNumber = 24;    // Crashes! non-rendered deck -> deck skirt (minimal)

        // Change Wall ID from uFromWall to uToWall within range
        public int Change(R_WGRA ResWGRA, uint uFromWall, uint uToWall,
            int iFromLevel, int iToLevel, float fFromX, float fToX, float fFromY, float fToY)
        {
            int iChangedCount = 0;

            byte[] DataNew = new byte[Data.Length];
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BW.Write(BR.ReadBytes(iHeaderSize));
            int iTotalSize = iHeaderSize;

            int iItemCount = BR.ReadInt32();
            BW.Write(iItemCount);
            iTotalSize += 4;

            int iKnown = 0;
            int iUnknown = 0;
            for (int i = 0; i < iItemCount; i++)
            {
                int iStructureSize = 0;

                // Matches Wall Reference Number in WGRA List of Walls
                uint uWallRef = BR.ReadUInt32();
                iStructureSize += 4;

                // Either matches Wall ID in Walls.txt
                // or GUID (no idea what the GUID means at this time)
                uint uWallID = BR.ReadUInt32();
                if (KnownWallID(uWallID))
                    iKnown++;
                else
                {
                    iUnknown++;
                    if (Test_PrintDebugInfo)
                        Debug.Print("WLL({0}): Ref={1:X8} ID={2:X8}", i, uWallRef, uWallID);
                }

                if (uWallID == uFromWall)
                {
                    int iLevel = ResWGRA.WallLevel(uWallRef);
                    if ((iLevel >= iFromLevel) && (iLevel <= iToLevel))
                    {
                        if (ResWGRA.WallInRange(uWallRef, fFromX, fToX, fFromY, fToY))
                        {
                            uWallID = uToWall;
                            iChangedCount++;
                        }
                    }
                }
                iStructureSize += 4;

                short iPatternID1 = BR.ReadInt16(); // Wall pattern on one side of the wall; corresponds to SMAP
                iStructureSize += 2;

                short iPatternID2 = BR.ReadInt16(); // Wall pattern on the other side of the wall; corresponds to SMAP
                iStructureSize += 2;

                if (Test_PrintDebugInfo)
                    Debug.Print("WLL({0}): Ref={1:X8} ID={2:X8} {3} {4}", i, uWallRef, uWallID, iPatternID1, iPatternID2);

                BW.Write(uWallRef);
                BW.Write(uWallID);
                BW.Write(iPatternID1);
                BW.Write(iPatternID2);

                iTotalSize += iStructureSize;
            }
            Debug.Assert(iTotalSize == Data.Length);
            if (Test_PrintDebugInfo)
                Debug.Print("Walls: known={0} unknown={1}", iKnown, iUnknown);
            if (iChangedCount > 0)
            {
                Data = DataNew;
                PFD.SetUserData(Data, true);
            }
            return iChangedCount;
        }
    }
}
