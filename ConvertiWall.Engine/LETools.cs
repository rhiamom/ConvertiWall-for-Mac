/***************************************************************************
 *   Copyright (C) 2008-2009 by Mootilda                                   *
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
    static public class LETools
    {
        public static bool Corrupt
        {
            get
            {
#if CORRUPT
                return true;
#else
                return false;
#endif
            }
        }

#if ADJUST
        public static byte[] CorruptString
        {
            get
            {
                string s = "Lot may have been corrupted using the LotCorrupter.  DO NOT SHARE!";
                byte[] ba = SimPe.Helper.ToBytes(s);
                return ba;
            }
        }
#endif

        public static bool ErrorChecking
        {
            get
            {
                if (Corrupt)
                    return false;
                return true;
            }
        }

#if ADJUST
        public static bool RestrictShrink
        {
            get
            {
                if (Corrupt)
                    return false;
                return true;
            }
        }
#endif

        public static int MaxLotSize
        {
            get
            {
                if (Corrupt)
                    return 10;
                return 6;
            }
        }

        public static bool FloatEqual(float f1, float f2)
        {
            const float fEpsilon = 5000;  // compare floats within epsilon
            return ((int)(fEpsilon * f1 + 0.5) - (int)(fEpsilon * f2 + 0.5)) == 0;
        }

        public static string Copy7BitStr(BinaryReader BR, BinaryWriter BW, bool bPrintDebugInfo)
        {
            // ReadString and Write seem to work for the average 7BitStr
            string s = BR.ReadString();
            BW.Write(s);
            return s;
        }

#if ADJUST
        // To allow something to exist up to the edge of the lot (no 1-tile restriction), pass:
        // (fOld + fAddLow, 0, 0, iSizeNew, fFix)
        public static float KeepOnLot(float fOld, float fAddLow, float fAddHigh, int iSizeNew, float fFix)
        {
            int iTileLimit = 1;   // 0 => Allow building right to edge; 1 => Allow building 1 tile from edge.
            int iMin = (!RestrictShrink) ? 0 : ((fAddLow < 0) ? iTileLimit : 0);
            int iMax = iSizeNew - ((!RestrictShrink) ? 0 : ((fAddHigh < 0) ? iTileLimit : 0));

            float XNew = fOld;
            XNew += fAddLow;

            // If an object is moved off-world, move it back.
            if (XNew < iMin)
            {
                // Debug.Print("WARNING: Coordinate < Minimum Value");
                XNew = iMin + fFix;
            }
            if (XNew > iMax)
            {
                // Debug.Print("WARNING: Coordinate > Maximum Value");
                XNew = iMax - fFix;
            }
            return XNew;
        }

        public static void PrintDataType(byte[] Data, int iIndex)
        {
            // Try to determine what's here: byte, short, integer, float?
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            byte bByte = BR.ReadByte();

            BR = SimPe.Helper.GetBinaryReader(Data);
            short sShort = BR.ReadInt16();

            BR = SimPe.Helper.GetBinaryReader(Data);
            int iInt = BR.ReadInt32();

            BR = SimPe.Helper.GetBinaryReader(Data);
            uint uInt = BR.ReadUInt32();

            BR = SimPe.Helper.GetBinaryReader(Data);
            float fFloat = BR.ReadSingle();

            Debug.Print("{0,4}  {1:X2}  {2,6}  {3,12}  {4:X8}  {5}", iIndex, bByte, sShort, iInt, uInt, fFloat);
        }

        public static void PrintDataTypes(byte[] Data, int iIndex)
        {
            // Try to determine what's here: byte, short, integer, float?
            for (int i = iIndex; i < Data.Length - 4; i++)
            {
                byte[] BA = new byte[4];
                Array.Copy(Data, i, BA, 0, 4);
                PrintDataType(BA, i);
            }
            Debug.Flush();
        }

        public static void PrintData(byte[] Data)
        {
            Debug.Print(" ");
            Debug.Print("  {0:X8} 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F   0123456789ABCDEF", Data.Length);
            for (int i = 0; i < Data.Length; i+=16)
            {
                Debug.Print("  {0:X8} {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2} {10:X2} {11:X2} {12:X2} {13:X2} {14:X2} {15:X2} {16:X2}   {17:C}{18:C}{19:C}{20:C}{21:C}{22:C}{23:C}{24:C}{25:C}{26:C}{27:C}{28:C}{29:C}{30:C}{31:C}{32:C}",
                    i,
                    ((i    < Data.Length) ? Data[i]    : (byte)0xFF),
                    ((i+ 1 < Data.Length) ? Data[i+ 1] : (byte)0xFF),
                    ((i+ 2 < Data.Length) ? Data[i+ 2] : (byte)0xFF),
                    ((i+ 3 < Data.Length) ? Data[i+ 3] : (byte)0xFF),
                    ((i+ 4 < Data.Length) ? Data[i+ 4] : (byte)0xFF),
                    ((i+ 5 < Data.Length) ? Data[i+ 5] : (byte)0xFF),
                    ((i+ 6 < Data.Length) ? Data[i+ 6] : (byte)0xFF),
                    ((i+ 7 < Data.Length) ? Data[i+ 7] : (byte)0xFF),
                    ((i+ 8 < Data.Length) ? Data[i+ 8] : (byte)0xFF),
                    ((i+ 9 < Data.Length) ? Data[i+ 9] : (byte)0xFF),
                    ((i+10 < Data.Length) ? Data[i+10] : (byte)0xFF),
                    ((i+11 < Data.Length) ? Data[i+11] : (byte)0xFF),
                    ((i+12 < Data.Length) ? Data[i+12] : (byte)0xFF),
                    ((i+13 < Data.Length) ? Data[i+13] : (byte)0xFF),
                    ((i+14 < Data.Length) ? Data[i+14] : (byte)0xFF),
                    ((i+15 < Data.Length) ? Data[i+15] : (byte)0xFF),
                    ((i    < Data.Length) ? (char)Data[i]    : ' '),
                    ((i+ 1 < Data.Length) ? (char)Data[i+ 1] : ' '),
                    ((i+ 2 < Data.Length) ? (char)Data[i+ 2] : ' '),
                    ((i+ 3 < Data.Length) ? (char)Data[i+ 3] : ' '),
                    ((i+ 4 < Data.Length) ? (char)Data[i+ 4] : ' '),
                    ((i+ 5 < Data.Length) ? (char)Data[i+ 5] : ' '),
                    ((i+ 6 < Data.Length) ? (char)Data[i+ 6] : ' '),
                    ((i+ 7 < Data.Length) ? (char)Data[i+ 7] : ' '),
                    ((i+ 8 < Data.Length) ? (char)Data[i+ 8] : ' '),
                    ((i+ 9 < Data.Length) ? (char)Data[i+ 9] : ' '),
                    ((i+10 < Data.Length) ? (char)Data[i+10] : ' '),
                    ((i+11 < Data.Length) ? (char)Data[i+11] : ' '),
                    ((i+12 < Data.Length) ? (char)Data[i+12] : ' '),
                    ((i+13 < Data.Length) ? (char)Data[i+13] : ' '),
                    ((i+14 < Data.Length) ? (char)Data[i+14] : ' '),
                    ((i+15 < Data.Length) ? (char)Data[i+15] : ' ')
                );
            }
            Debug.Print(" ");
            Debug.Flush();
        }
    }

    public class ShrinkException : Exception
    {
        public ShrinkException()
        {
        }
        public ShrinkException(string message)
            : base(message)
        {
        }
        public ShrinkException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class EmptyLotException : Exception
    {
        public EmptyLotException()
        {
        }
        public EmptyLotException(string message)
            : base(message)
        {
        }
        public EmptyLotException(string message, Exception inner)
            : base(message, inner)
        {
        }
#endif
    }
}
