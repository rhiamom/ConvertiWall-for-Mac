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
using System.Diagnostics;
using System.IO;
using SimPe.Interfaces.Files;


namespace LotExpander
{
    public class R_LOT : R_LotDescription
    {
        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private const int iLeadingZeros = 64;
        private int iWidthIndex = 66;
        private int iWidth = 0;
        private int iHeightIndex = 70;
        private int iHeight = 0;
        private byte bType = 0xFF;
        private byte bU10 = 0xFF;
        private byte bU11 = 0xFF;
        private string sLotName = null;
        private string sLotDesc = null;

        public R_LOT(IPackageFile LotPackage, IPackedFileDescriptor Descriptor)
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

            // ToDo: Is this a version number?
            ushort uVersion = BR.ReadUInt16();
            Debug.Assert((uVersion == 6)    // ToDo: Determine whether other values are known and handled correctly
                      || (uVersion == 7)    // Bon Voyage
                      || (uVersion == 8)    // Free Time
                      || (uVersion == 11)   // Apartment Life
            );
            int iIndex = iLeadingZeros + 2 /* uDumm */;

            Debug.Assert(iWidthIndex == iIndex);
            iWidthIndex = iIndex;
            iWidth = BR.ReadInt32();
            iIndex += 4;
            if ((iWidth < 1) || (iWidth > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid LOT: Width");

            Debug.Assert(iHeightIndex == iIndex);
            iHeightIndex = iIndex;
            iHeight = BR.ReadInt32();
            iIndex += 4;
            if ((iHeight < 1) || (iHeight > LETools.MaxLotSize))
                if (LETools.ErrorChecking)
                    throw new InvalidDataException("Invalid LOT: Height");

            bType = BR.ReadByte();
            Debug.Assert((bType == 0)   // Residential
                      || (bType == 1)   // Community
                      || (bType == 2)   // University: Dorm
                      || (bType == 3)   // University: Greek House
                      || (bType == 4)   // University: Secret Society
                      || (bType == 5)   // Bon Voyage: Hotel
                      || (bType == 6)   // Bon Voyage: Hidden Vacation Lot
                      || (bType == 7)   // FreeTime: Hidden Hobby Lot
                      || (bType == 8)   // Apartment Life: Apartment Building
                      || (bType == 9)   // Apartment Life: Occupied Apartment
                      || (bType == 10)  // Apartment Life: Hidden Lot (Witches)
            );
            iIndex++;

            bU10 = BR.ReadByte();
            Debug.Assert(bU10 < 0x10);
            iIndex++;

            bU11 = BR.ReadByte();
            Debug.Assert(bU11 < 4);
            iIndex++;

            uint uDummy = BR.ReadUInt32();
            iIndex += 4;

            sLotName = BR.ReadString();
            iIndex += sLotName.Length + 1;

            sLotDesc = BR.ReadString();
            iIndex += sLotDesc.Length + 1;

            /* Still haven't parsed the rest of this record
            int iCount = BR.ReadInt32();
            iIndex += 4;

            byte b = BR.ReadByte();
            iIndex++;
            for (int i = 0; i < iCount; i++)
            {
                float f = BR.ReadUInt32();  // Unknown
                iIndex += 4;
            }

            if ((uVersion >= 7))
            {
                // LETools.PrintDataTypes(Data, iIndex);
                float fExtra = BR.ReadSingle();
                iIndex += 4;
                if (uVersion >= 8)
                {
                    // LETools.PrintDataTypes(Data, iIndex);
                    uDummy = BR.ReadUInt32();
                    iIndex += 4;
                }
            }
            Debug.Assert(Data.Length == iIndex);
             */
        }

        private void ReplaceInt(int i, int iIndex)
        {
            byte[] BA = new byte[4];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(i);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);
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

        public override int Width
        {
            get
            {
                return iWidth;
            }
            set
            {
                if (this.Width != value)
                {
                    ReplaceWidth(value);
                }
            }
        }

        public override int Height
        {
            get
            {
                return iHeight;
            }
            set
            {
                if (this.Height != value)
                {
                    ReplaceHeight(value);
                }
            }
        }

        public override byte LotType
        {
            get
            {
                return bType;
            }
        }

        public override bool Occupied
        {
            get
            {
                return false;
            }
        }

        public override byte U10
        {
            get
            {
                return bU10;
            }
        }

        public override byte U11
        {
            get
            {
                return bU11;
            }
        }

        public override string LotName
        {
            get
            {
                return sLotName;
            }
        }

        public override string LotDesc
        {
            get
            {
                return sLotDesc;
            }
            set
            {
                Debug.Fail("You have reached unreachable code!");
                return;
/*
                // Unused
                int iLenNew = value.Length;
                Debug.Assert(iLenNew < 128);

                // Pre-read, so that we know how long to make the new record
                BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                BR.ReadBytes(iLeadingZeros);
                BR.ReadUInt16();    // Version
                BR.ReadInt32();     // Width
                BR.ReadInt32();     // Height
                BR.ReadByte();      // Type
                BR.ReadByte();      // U10
                BR.ReadByte();      // U11
                BR.ReadUInt32();    // Unknown
                BR.ReadString();    // Lot Name

                // Woe be to anyone with long strings or multi-byte characters!
                string sLotDesc = BR.ReadString();
                int iDescLen = sLotDesc.Length;
                int iDescLenBytes = ((iDescLen < 128) ? 1 : ((iDescLen < 256) ? 2 : ((iDescLen < (128+256)) ? 3 : 4)));
                Debug.Assert(iDescLen < (128+256));
                Debug.Print("Replace LOT \"{0}\"", sLotDesc);

                // Now that we know the difference in length between the old and new lot descriptions,
                // we can allocate the correct length data.
                byte[] DataNew = new byte[Data.Length - iDescLen - iDescLenBytes + iLenNew + 1];
                BR = SimPe.Helper.GetBinaryReader(Data);
                BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                BW.Write(BR.ReadBytes(iLeadingZeros));
                BW.Write(BR.ReadUInt16());  // Version
                BW.Write(BR.ReadInt32());   // Width
                BW.Write(BR.ReadInt32());   // Height
                BW.Write(BR.ReadByte());    // Type
                BW.Write(BR.ReadByte());    // U10
                BW.Write(BR.ReadByte());    // U11
                BW.Write(BR.ReadUInt32());  // Unknown
                BW.Write(BR.ReadString());  // Lot Name

                string s = BR.ReadString();
                BW.Write(value);

                // Write the rest of the record
                // Very inefficient, but I can't seem to find a "current position in the stream".
                for (; ; )
                {
                    try
                    {
                        byte B = BR.ReadByte();
                        BW.Write(B);
                    }
                    catch
                    {
                        break;
                    }
                }

                // Determine how many bytes we need to remove from record.
                // These additional bytes occur when sLotDesc.Length does not reflect the number of bytes
                // because the string contains multi-byte characters.
                int iExcess = 0;
                for (; ; iExcess++)
                {
                    try
                    {
                        byte B = 0;
                        BW.Write(B);
                    }
                    catch
                    {
                        break;
                    }
                }
                // At this point, we don't know whether it's a problem to have additional bytes at the end.
                // Debug.Assert(iExcess == 0);

                Data = DataNew;
                PFD.SetUserData(Data, true);
 */
            }
        }
    }
}

