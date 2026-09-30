/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2007-2009 by Mootilda                                   *
 *   http://Mootilda.ModTheSims.info                                       *
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SimPe.Interfaces.Files;


namespace LotExpander
{
    public class R_WGRA
    {
        private bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information
        private bool Test_PrintWallGraph = false;   // Enable (T) or disable (F) printing of pictures
        private string sWallList = "Wall, fence, foundation, attic wall, swimming pool, or other vertical surface";

        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private int iBlockVersion;
        private const int iLeadingZeros = 64;
        private int iWidthIndex = 95;
        private int iWidth = 0;
        private int iHeightIndex = 99;
        private int iHeight = 0;
        private int iMinLevel = 0;
        private int iMaxLevel = 0;
        private int iVertexCountIndex = 115;
        private int iVertexCount = 0;
        private int iHeaderSize = 119;
        private const int iLotTilesPerNeighborhoodTile = 10;
        private const int iBytesPerObject = 16;
        private int iMysteryMultiplier = 2;         // ToDo: What exactly is this?
        private int iConvertTilesToVertices = 1;    // 0 = leave as number of tiles, 1 = convert to / from number of vertices

        struct Vertex
        {
            public uint uItem;
            public float fX;
            public float fY;
            public int iLevel;
            public int iWalls;        // Number of walls associated with this vertex
            public byte bDirection;    // Directions of walls associated with this vertex

            public Vertex(uint uItemIn, float fXIn, float fYIn, int iLevelIn)
            {
                uItem = uItemIn;
                fX = fXIn;
                fY = fYIn;
                iLevel = iLevelIn;
                iWalls = 0;
                bDirection = 0;
            }
        }
        private IDictionary<uint, Vertex> vaVertices = new Dictionary<uint, Vertex>();

        // Values for bDirection:
        // Flat walls:
        const byte DIRECT_LEFT = 0x01;
        const byte DIRECT_RIGHT = 0x02;
        const byte DIRECT_UP = 0x04;
        const byte DIRECT_DOWN = 0x08;
        // Diagonal Walls:
        const byte DIRECT_UP_LEFT = 0x10;
        const byte DIRECT_UP_RIGHT = 0x20;
        const byte DIRECT_DOWN_LEFT = 0x40;
        const byte DIRECT_DOWN_RIGHT = 0x80;

        struct Wall
        {
            public uint uWallRef;
            public Vertex vVertex1;
            public Vertex vVertex2;

            public Wall(uint uWallIn, Vertex vVertex1In, Vertex vVertex2In)
            {
                uWallRef = uWallIn;
                vVertex1 = vVertex1In;
                vVertex2 = vVertex2In;
            }
        }
        private static IDictionary<uint, Wall> waWalls = new Dictionary<uint, Wall>();

        public R_WGRA(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = LotPackage.Read(PFD);
            Data = PF.UncompressedData;

            uint uInst = PFD.Instance;
            if (Test_PrintDebugInfo)
                Debug.Print("WGRA Instance {0:X8}:", uInst);
            // ToDo: determine what each instance contains:
            // Since walls appear multiple times in different WGRA instances,
            // and there seem to be a fixed set of WGRA instances (no matter how complex the building),
            // these must reflect properties of walls, such as:
            // light blocking, load bearing, half-wall, ...
            Debug.Assert((uInst == 0x05)    // Pool, Walls, Fences, Foundation, Attic
                      || (uInst == 0x06)    // Pool, Walls,         Foundation
                      || (uInst == 0x07)    //       Walls,         Foundation, Attic
                      || (uInst == 0x08)
                      || (uInst == 0x13)    // empty?
                      || (uInst == 0x16)    // Pool, Walls,         Foundation
                      || (uInst == 0x17)    //                                  Attic?
                      || (uInst == 0x18)    // Pool, Walls, Fences, Foundation, Attic
                      || (uInst == 0x19)    // empty?
                      || (uInst == 0x1A)    // empty
                      || (uInst == 0x1C)
                      || (uInst == 0x1F)
            );

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            for (int i = 0; i < iLeadingZeros; i++)
            {
                byte bDummy = BR.ReadByte();
                Debug.Assert(bDummy == 0);
            }
            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x0A284D0B)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WGRA: Block ID");

            iBlockVersion = BR.ReadInt32();
            Debug.Assert((iBlockVersion == 2) || (iBlockVersion == 3));   // ToDo: Determine whether other versions are known and handled correctly

            byte bBlockNameLen = (byte)BR.PeekChar();
            if ((bBlockNameLen & 0x80) != 0)
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WGRA: Block Name Length");
            string sBlockName = BR.ReadString();
            if (sBlockName != "cWallGraph")
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid WGRA: Block Name");
            int iIndex = iLeadingZeros + 4 /* uBlockID */ + 4 /* iBlockVersion */ + 1 /* bBlockNameLen */ + bBlockNameLen;

            uint uDummy = BR.ReadUInt32();
            Debug.Assert(0 == uDummy);
            iIndex += 4;

            uDummy = BR.ReadUInt32();
            Debug.Assert(0 == uDummy);
            iIndex += 4;

            iMinLevel = BR.ReadInt32();
            Debug.Assert((0 == iMinLevel) || (-1 == iMinLevel));
            iIndex += 4;

            Debug.Assert(iWidthIndex == iIndex);
            iWidthIndex = iIndex;
            iWidth = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeightIndex == iIndex);
            iHeightIndex = iIndex;
            iHeight = BR.ReadInt32();
            iIndex += 4;

            iMaxLevel = BR.ReadInt32();
            iIndex += 4;

            uDummy = BR.ReadUInt32();
            iIndex += 4;

            uDummy = BR.ReadUInt32();
            iIndex += 4;

            Debug.Assert(iVertexCountIndex == iIndex);
            iVertexCountIndex = iIndex;
            iVertexCount = BR.ReadInt32();
            iIndex += 4;

            Debug.Assert(iHeaderSize == iIndex);
            iHeaderSize = iIndex;

            switch (iBlockVersion)
            {
                case 2:
                    {
                        iMysteryMultiplier = 2;
                        iConvertTilesToVertices = 0;
                        break;
                    }
                case 3:
                    {
                        iMysteryMultiplier = 1;
                        iConvertTilesToVertices = 1;
                        break;
                    }
                default:
                    {
                        Debug.Fail("Unknown Wall Graph Version - may not be handled correctly");
                        iMysteryMultiplier = 1;
                        iConvertTilesToVertices = 1;
                        break;
                    }
            }
            Debug.Assert(Width > 0);
            // Debug.Assert(Width < 7);    // Capp Manor = 7  13 Dead End Lane = 8  165 Sim Lane = 9
            Debug.Assert(Height > 0);
            // Debug.Assert(Height < 7);   //                 13 Dead End Lane = 8

            // ToDo: Is there any way to check length?
        }

#if ADJUST
        private void ReplaceInt(int i, int iIndex)
        {
            byte[] BA = new byte[4];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(i);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);

            // ToDo: Is there any way to check length?
        }

        private void ReplaceWidth(int i)
        {
            iWidth = i;
            ReplaceInt(iWidth, iWidthIndex);
        }

        private void ReplaceHeight(int i)
        {
            iHeight = i;
            ReplaceInt(iHeight, iHeightIndex);
        }
#endif

        public int MinimumLevel
        {
            get
            {
                return iMinLevel;
            }
        }

        public int MaximumLevel
        {
            get
            {
                return iMaxLevel;
            }
        }

        public int Width
        {
            get
            {
                return (iWidth - iConvertTilesToVertices) / (iLotTilesPerNeighborhoodTile * iMysteryMultiplier);
            }
#if ADJUST
            set
            {
                if (this.Width != value)
                {
                    ReplaceWidth(value * (iLotTilesPerNeighborhoodTile * iMysteryMultiplier) + iConvertTilesToVertices);
                }
            }
#endif
        }

        public int Height
        {
            get
            {
                return (iHeight - iConvertTilesToVertices) / (iLotTilesPerNeighborhoodTile * iMysteryMultiplier);
            }
#if ADJUST
            set
            {
                if (this.Height != value)
                {
                    ReplaceHeight(value * (iLotTilesPerNeighborhoodTile * iMysteryMultiplier) + iConvertTilesToVertices);
                }
            }
#endif
        }

#if ADJUST
        private bool BitsSet(byte bValue, byte bFind)
        {
            return (bFind == (bValue & bFind));
        }
#endif

#if ADJUST || CONVERT
        public static void Clear()
        {
            waWalls.Clear();
        }
#endif

#if ADJUST
        public void AddRooms(ref uint[] uRooms)
        {
            if (Data.Length <= iHeaderSize + iBytesPerObject)
            {
                Debug.Assert(iVertexCount == 0);
                return;
            }

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BR.ReadBytes(iHeaderSize);

            for (int i = 0; i < iVertexCount; i++)
            {
                uint uItem = BR.ReadUInt32();
                float fX = BR.ReadSingle();
                float fY = BR.ReadSingle();
                int iLevel = BR.ReadInt32();
            }

            int iCount = BR.ReadInt32();
            for (int i = 0; i < iCount; i++)
            {
                uint u = BR.ReadUInt32();
                int j = 0;
                for (; j < uRooms.Length; j++)
                {
                    if (uRooms[j] == u)
                        break;
                }
                if (j == uRooms.Length)
                {
                    Array.Resize<uint>(ref uRooms, uRooms.Length + 1);
                    uRooms[j] = u;
                }
            }
            return;
        }

        public void Change(int XAddLow, int XAddHigh, int YAddLow, int YAddHigh, int iWidthNew, int iHeightNew)
        {
            if (Data.Length <= iHeaderSize + iBytesPerObject)
            {
                Debug.Assert(iVertexCount == 0);
                return;
            }

            // ToDo: Why not use iMysteryMultiplier & iConvertTilesToVertices for these values?
            XAddLow *= iLotTilesPerNeighborhoodTile;
            XAddHigh *= iLotTilesPerNeighborhoodTile;
            YAddLow *= iLotTilesPerNeighborhoodTile;
            YAddHigh *= iLotTilesPerNeighborhoodTile;
            iWidthNew *= iLotTilesPerNeighborhoodTile;
            iHeightNew *= iLotTilesPerNeighborhoodTile;

            byte[] DataNew = new byte[Data.Length];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);

            //Header
            BW.Write(BR.ReadBytes(iHeaderSize));
            int iLengthOld = iHeaderSize;
            int iLengthNew = iHeaderSize;

            // int iMaxLevel = 1;

            //Daten manipulieren
            if (Test_PrintDebugInfo)
                Debug.Print("WGRA Vertex Structure has {0} items:", iVertexCount);
            for (int i = 0; i < iVertexCount; i++)
            {
                uint uItem = BR.ReadUInt32();

                float XOld = BR.ReadSingle();
                Debug.Assert(XOld >= 0);
                Debug.Assert((XOld % 1.0) == 0.0);
                float XNew = XOld + XAddLow;
                // Reduce restrictions to allow perpendicular walls:
                // float XFix = LETools.KeepOnLot(XOld, XAddLow, XAddHigh, iWidthNew, 0);
                float XFix = LETools.KeepOnLot(XOld + XAddLow, 0, 0, iWidthNew, 0);

                float YOld = BR.ReadSingle();
                Debug.Assert(YOld >= 0);
                Debug.Assert((YOld % 1.0) == 0.0);
                float YNew = YOld + YAddLow;
                // Reduce restrictions to allow perpendicular walls:
                // float YFix = LETools.KeepOnLot(YOld, YAddLow, YAddHigh, iHeightNew, 0);
                float YFix = LETools.KeepOnLot(YOld + YAddLow, 0, 0, iHeightNew, 0);

                int iLevel = BR.ReadInt32();
                if (iLevel > iMaxLevel)
                    iMaxLevel = iLevel;
                else if (iLevel < iMinLevel)
                    Debug.Fail("Level less than minimum level");

                Vertex vVertex = new Vertex(uItem, XNew, YNew, iLevel);
                vaVertices.Add(uItem, vVertex);

                iLengthOld += iBytesPerObject;
                if ((XNew == XFix) && (YNew == YFix))
                {
                    // Keep wall vertex if on-world
                    BW.Write(uItem);
                    BW.Write(XNew);
                    BW.Write(YNew);
                    BW.Write(iLevel);
                    iLengthNew += iBytesPerObject;
                    if (Test_PrintDebugInfo)
                        Debug.Print("  WGRA({0}) Item={1:X8} X={2}->{3} Y={4}->{5} Level={6}",
                            i, uItem, XOld, XNew, YOld, YNew, iLevel);
                }
                else
                {
                    // Reject wall vertex if off-world
                    // ToDo: Handle off-world coordinates?
                    PrimaryForm.ThrowErrorOffLot("WGRA", sWallList);
                }
            }

            // Wall List:
            // If the lot is being shrunken, then allow some, but not all, walls which touch the edge of the lot
            // In order to do this, we need to examine the List of Walls, rather than just the List of Vertices
            // Will also want to examine the List of Vertices for debugging purposes.
            if (LETools.RestrictShrink && ((XAddLow < 0) || (XAddHigh < 0) || (YAddLow < 0) || (YAddHigh < 0)
             || Test_PrintDebugInfo || Test_PrintWallGraph))
            {
                int iCount = BR.ReadInt32();
                if (Test_PrintDebugInfo)
                    Debug.Print("Structure 2 has {0:D} items:", iCount);
                for (int i = 0; i < iCount; i++)
                {
                    uint u = BR.ReadUInt32();
                    if (Test_PrintDebugInfo)
                        Debug.Print("  {0:X8}", u);
                }

                int iWallCount = BR.ReadInt32();
                if (Test_PrintDebugInfo)
                    Debug.Print("WGRA Wall Structure has {0:D} items:", iWallCount);
                for (int i = 0; i < iWallCount; i++)
                {
                    uint u0 = BR.ReadUInt32();              // Unknown: perhaps type of wall?

                    uint uItem1 = BR.ReadUInt32();          // Wall vertex key
                    uint u1 = BR.ReadUInt32();              // Unknown Data

                    uint uItem2 = BR.ReadUInt32();          // Wall vertex key
                    uint u2 = BR.ReadUInt32();              // Unknown Data

                    // Find associated wall vertices:
                    Vertex w1 = vaVertices[uItem1];
                    Vertex w2 = vaVertices[uItem2];

                    Wall wWall = new Wall(u0, w1, w2);
                    waWalls.Add(u0, wWall);

                    // Debug.Print("  {0:X8} {1:X8} {2:X8} {3:X8} {4:X8}", u0, uItem1, u1, uItem2, u2);
                    if (Test_PrintDebugInfo)
                        Debug.Print("  {0:X8} {1:X8}: X={2,2},Y={3,2},L={4,2} -> {5:X8} X={6,2},Y={7,2},L={8,2} Data: {9,2} {10,2}",
                            u0, w1.uItem, w1.fX, w1.fY, w1.iLevel, w2.uItem, w2.fX, w2.fY, w2.iLevel, u1, u2);

                    w1.iWalls++;
                    vaVertices[uItem1] = w1;
                    w2.iWalls++;
                    vaVertices[uItem2] = w2;
                    // Reject walls which run along the edge of the lot
                    if (((XAddLow < 0) && (0 == w1.fX) && (w1.fX == w2.fX))
                     || ((YAddLow < 0) && (0 == w1.fY) && (w1.fY == w2.fY))
                     || ((XAddHigh < 0) && (iWidthNew == w1.fX) && (w1.fX == w2.fX))
                     || ((YAddHigh < 0) && (iHeightNew == w1.fY) && (w1.fY == w2.fY))
                    // Reject wall vertex on edge if associated with multiple walls,
                    // since it may create an enclosed space.
                     || ((XAddLow < 0) && (0 == w1.fX) && (w1.iWalls > 1))
                     || ((XAddLow < 0) && (0 == w2.fX) && (w2.iWalls > 1))
                     || ((YAddLow < 0) && (0 == w1.fY) && (w1.iWalls > 1))
                     || ((YAddLow < 0) && (0 == w2.fY) && (w2.iWalls > 1))
                     || ((XAddHigh < 0) && (iWidthNew == w1.fX) && (w1.iWalls > 1))
                     || ((XAddHigh < 0) && (iWidthNew == w2.fX) && (w2.iWalls > 1))
                     || ((YAddHigh < 0) && (iHeightNew == w1.fY) && (w1.iWalls > 1))
                     || ((YAddHigh < 0) && (iHeightNew == w2.fY) && (w2.iWalls > 1))
                       )
                    {
                        PrimaryForm.ThrowErrorOffLot("WGRA", sWallList);
                    }

                    // Determine relationship between the two vertices:
                    if ((w1.fX == w2.fX + 1) && (w1.fY == w2.fY))
                    {
                        w1.bDirection |= DIRECT_DOWN;
                        w2.bDirection |= DIRECT_UP;
                    }
                    else if ((w1.fX == w2.fX - 1) && (w1.fY == w2.fY))
                    {
                        w1.bDirection |= DIRECT_UP;
                        w2.bDirection |= DIRECT_DOWN;
                    }
                    else if ((w1.fX == w2.fX) && (w1.fY == w2.fY + 1))
                    {
                        w1.bDirection |= DIRECT_RIGHT;
                        w2.bDirection |= DIRECT_LEFT;
                    }
                    else if ((w1.fX == w2.fX) && (w1.fY == w2.fY - 1))
                    {
                        w1.bDirection |= DIRECT_LEFT;
                        w2.bDirection |= DIRECT_RIGHT;
                    }
                    else if ((w1.fX == w2.fX + 1) && (w1.fY == w2.fY + 1))
                    {
                        w1.bDirection |= DIRECT_DOWN_RIGHT;
                        w2.bDirection |= DIRECT_UP_LEFT;
                    }
                    else if ((w1.fX == w2.fX + 1) && (w1.fY == w2.fY - 1))
                    {
                        w1.bDirection |= DIRECT_DOWN_LEFT;
                        w2.bDirection |= DIRECT_UP_RIGHT;
                    }
                    else if ((w1.fX == w2.fX - 1) && (w1.fY == w2.fY + 1))
                    {
                        w1.bDirection |= DIRECT_UP_RIGHT;
                        w2.bDirection |= DIRECT_DOWN_LEFT;
                    }
                    else if ((w1.fX == w2.fX - 1) && (w1.fY == w2.fY - 1))
                    {
                        w1.bDirection |= DIRECT_UP_LEFT;
                        w2.bDirection |= DIRECT_DOWN_RIGHT;
                    }
                    else
                        Debug.Fail("Unknown direction");

                    bool bDiagonal =
                        (0 != (w1.bDirection & ~(DIRECT_UP | DIRECT_DOWN | DIRECT_LEFT | DIRECT_RIGHT)));
                    if (bDiagonal)
                    {
                        // Reject diagonal walls which touch the edge of the lot
                        if (((XAddLow < 0) && (0 == w1.fX))
                         || ((YAddLow < 0) && (0 == w1.fY))
                         || ((XAddHigh < 0) && (iWidthNew == w1.fX))
                         || ((YAddHigh < 0) && (iHeightNew == w1.fY))
                           )
                        {
                            PrimaryForm.ThrowErrorOffLot("WGRA", sWallList);
                        }
                    }

                    vaVertices[uItem1] = w1;
                    vaVertices[uItem2] = w2;
                }

                if (Test_PrintWallGraph)
                {
                    // Print a picture of the walls, to help debug logic:
                    int iPrintLevel = iMaxLevel + 2 - iMinLevel; // Pool = -1; Ground = 0; plus all above-ground levels.
                    byte[, ,] uaWalls = new byte[iPrintLevel, iWidthNew + 1, iHeightNew + 1];
                    foreach (KeyValuePair<uint, Vertex> kvp in vaVertices)
                    {
                        Vertex vVertex = kvp.Value;
                        uaWalls[vVertex.iLevel + 1, (int)(vVertex.fX), (int)(vVertex.fY)] = vVertex.bDirection;
                    }
                    for (int l = 0; l < iPrintLevel; l++)
                    {
                        Debug.Print("Instance={0} Level={1}", PFD.Instance, l);
                        for (int j = 0; j <= iWidthNew; j++)
                        {
                            string s = "";
                            for (int k = 0; k <= iHeightNew; k++)
                            {
                                byte b = uaWalls[l, j, k];
                                string sAdd = " ";
                                if (0 != b)
                                {
                                    if (BitsSet(b, DIRECT_DOWN_LEFT) || BitsSet(b, DIRECT_UP_RIGHT))
                                    {
                                        if (BitsSet(b, DIRECT_UP_LEFT) || BitsSet(b, DIRECT_DOWN_RIGHT))
                                            sAdd = "X";
                                        else
                                            sAdd = "/";
                                    }
                                    else if (BitsSet(b, DIRECT_UP_LEFT) || BitsSet(b, DIRECT_DOWN_RIGHT))
                                    {
                                        sAdd = "\\";
                                    }
                                    else if (BitsSet(b, DIRECT_DOWN) || BitsSet(b, DIRECT_UP))
                                    {
                                        if (BitsSet(b, DIRECT_LEFT) || BitsSet(b, DIRECT_RIGHT))
                                            sAdd = "+";
                                        else
                                            sAdd = "|";
                                    }
                                    else if (BitsSet(b, DIRECT_LEFT) || BitsSet(b, DIRECT_RIGHT))
                                    {
                                        sAdd = "-";
                                    }
                                }
                                s = string.Concat(s, sAdd);
                            }
                            Debug.Print(s);
                        }
                    }
                }
            }

            //Rest bleibt unverändert
            Array.Copy(Data, iLengthOld, DataNew, iLengthNew, Data.Length - iLengthOld);
            Data = DataNew;
            PFD.SetUserData(Data, true);
        }
#endif

#if CONVERT
        public void Parse()
        {
            if (Data.Length <= iHeaderSize + iBytesPerObject)
            {
                Debug.Assert(iVertexCount == 0);
                return;
            }

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);

            //Header
            BR.ReadBytes(iHeaderSize);
            int iLengthOld = iHeaderSize;
            int iLengthNew = iHeaderSize;

            // int iMaxLevel = 1;

            //Daten manipulieren
            if (Test_PrintDebugInfo)
                Debug.Print("WGRA Vertex Structure has {0} items:", iVertexCount);
            for (int i = 0; i < iVertexCount; i++)
            {
                uint uItem = BR.ReadUInt32();
                float XOld = BR.ReadSingle();
                float YOld = BR.ReadSingle();
                int iLevel = BR.ReadInt32();
                Vertex vVertex = new Vertex(uItem, XOld, YOld, iLevel);
                vaVertices.Add(uItem, vVertex);
                iLengthOld += iBytesPerObject;
                if (Test_PrintDebugInfo)
                    Debug.Print("  WGRA({0}) Item={1:X8} X={2} Y={3} Level={4}",
                        i, uItem, XOld, YOld, iLevel);
            }

            // Wall List:
            int iCount = BR.ReadInt32();
            if (Test_PrintDebugInfo)
                Debug.Print("Structure 2 has {0:D} items:", iCount);
            for (int i = 0; i < iCount; i++)
            {
                uint u = BR.ReadUInt32();
                if (Test_PrintDebugInfo)
                    Debug.Print("  {0:X8}", u);
            }

            int iV1Found = 0;
            int iV1Mismatch = 0;
            int iV2Found = 0;
            int iV2Mismatch = 0;
            int iNew = 0;
            int iWallCount = BR.ReadInt32();
            if (Test_PrintDebugInfo)
                Debug.Print("WGRA Wall Structure has {0:D} items:", iWallCount);
            for (int i = 0; i < iWallCount; i++)
            {
                uint u0 = BR.ReadUInt32();              // Wall Reference Number matches WLL

                uint uItem1 = BR.ReadUInt32();          // Wall vertex key
                uint u1 = BR.ReadUInt32();              // Unknown Data

                uint uItem2 = BR.ReadUInt32();          // Wall vertex key
                uint u2 = BR.ReadUInt32();              // Unknown Data

                // Find associated wall vertices:
                Vertex w1 = vaVertices[uItem1];
                Vertex w2 = vaVertices[uItem2];
                Debug.Assert(w1.iLevel == w2.iLevel);

                try
                {
                    Wall wWall = waWalls[u0];

                    // vertex 1 should be identical, but aren't
                    if ((w1.fX == wWall.vVertex1.fX)
                     && (w1.fY == wWall.vVertex1.fY)
                     && (w1.iLevel == wWall.vVertex1.iLevel))
                        iV1Found++;
                    else
                    {
                        Debug.Assert(2 == iBlockVersion);
                        iV1Mismatch++;
                    }


                    // vertex 2 should be identical, but aren't
                    if ((w2.fX == wWall.vVertex2.fX)
                     && (w2.fY == wWall.vVertex2.fY)
                     && (w2.iLevel == wWall.vVertex2.iLevel))
                        iV2Found++;
                    else
                    {
                        Debug.Assert(2 == iBlockVersion);
                        iV2Mismatch++;
                    }
                }
                catch
                {
                    Debug.Assert(PFD.Instance == 5);
                    Wall wWall = new Wall(u0, w1, w2);
                    waWalls.Add(u0, wWall);
                    iNew++;
                }

                // Debug.Print("  {0:X8} {1:X8} {2:X8} {3:X8} {4:X8}", u0, uItem1, u1, uItem2, u2);
                if (Test_PrintDebugInfo)
                    Debug.Print("  {0:X8} {1:X8}: X={2,2},Y={3,2},L={4,2} -> {5:X8} X={6,2},Y={7,2},L={8,2} Data: {9,2} {10,2}",
                        u0, w1.uItem, w1.fX, w1.fY, w1.iLevel, w2.uItem, w2.fX, w2.fY, w2.iLevel, u1, u2);

                w1.iWalls++;
                vaVertices[uItem1] = w1;
                w2.iWalls++;
                vaVertices[uItem2] = w2;
            }
            Debug.Print("WGRA Instance {0} new={1} V1: found={2} mismatch={3} V2: found={4} mismatch={5}",
                PFD.Instance, iNew, iV1Found, iV1Mismatch, iV2Found, iV2Mismatch);
        }

        public bool WallInRange(uint uWallRef, float fXStart, float fXEnd, float fYStart, float fYEnd)
        {
            Wall wWall = waWalls[uWallRef];
            Vertex vVertex1 = wWall.vVertex1;
            Vertex vVertex2 = wWall.vVertex2;
            float fX1 = vVertex1.fX;
            float fY1 = vVertex1.fY;
            float fX2 = vVertex2.fX;
            float fY2 = vVertex2.fY;
            if (fX1 < fX2)
            {
                if ((fX1 < fXStart) || (fX2 > fXEnd))
                    return false;
            }
            else if ((fX2 < fXStart) || (fX1 > fXEnd))
                return false;
            if (fY1 < fY2)
            {
                if ((fY1 < fYStart) || (fY2 > fYEnd))
                    return false;
            }
            else if ((fY2 < fYStart) || (fY1 > fYEnd))
                return false;
            return true;
        }

        public int WallLevel(uint uWallRef)
        {
            int iLevel = -2;    // not a valid level
            try
            {
                Wall wWall = waWalls[uWallRef];
                Vertex vVertex = wWall.vVertex1;    // Both vertices should be on the same level, so just pick one.
                // ToDo: determine whether the two vertices can be on different levels.
                iLevel = vVertex.iLevel;
            }
            catch
            {
                Debug.Fail("What happened to the Wall Reference Number?");
            }
            return iLevel;
        }
#endif
    }
}
