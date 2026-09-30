/***************************************************************************
 *   Copyright (C) 2009-2010 by Mootilda                                   *
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
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SimPe.Interfaces.Files;
using SimPe.Packages;
using System.Resources;

namespace LotExpander
{
    public partial class PrimaryForm : Form
    {
        private bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information
        private bool Test_AlwaysAbort = false;      // Enable (T) or disable (F) abort for all lots

        private const uint uVersionNumber = 17;
        // The version strings vary by language, so it's better to check version number
        private string[] sVersionStrings = 
        {
            /*  0 */ "The Sims 2",
            /*  1 */ "The Sims 2 University",
            /*  2 */ "The Sims 2 Nightlife",
            /*  3 */ "The Sims 2 Open For Business",
            /*  4 */ "The Sims 2 Family Fun Stuff",
            /*  5 */ "The Sims 2 Glamour Life Stuff",
            /*  6 */ "The Sims 2 Pets",
            /*  7 */ "The Sims 2 Seasons",
            /*  8 */ "The Sims 2 Celebration! Stuff",
            /*  9 */ "The Sims™ 2 H&M® Fashion Stuff",
            /* 10 */ "The Sims™ 2 Bon Voyage",
            /* 11 */ "The Sims™ 2 Teen Style Stuff",
            /* 12 */ "The Sims™ 2 Store",                   // ToDo: Check this...
            /* 13 */ "The Sims™ 2 FreeTime",
            /* 14 */ "The Sims 2 Kitchen and Bath Stuff",   // ToDo: Check this...
            /* 15 */ "The Sims 2 Ikea Stuff",               // ToDo: Check this...
            /* 16 */ "The Sims™ 2 Apartment Life",
            /* 17 */ "The Sims™ 2 Mansion and Garden Stuff"
        };

        private OpenFileDialog openFileDialog1 = new OpenFileDialog();
        private bool bBackupVersioning = false;     // Keep multiple versions of backup files
        private string sBackupConfigFile = null;

        public PrimaryForm()
        {
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            // this.CenterToScreen();
            RMF = new ResourceManager("LotExpander.PrimaryForm", typeof(PrimaryForm).Assembly);
            RME = new ResourceManager("LotExpander.LEStrings", typeof(PrimaryForm).Assembly);
        }

        ResourceManager RMF;
        ResourceManager RME;
        IPackedFileDescriptor[] LotPFDs;
        GeneratableFile NBPack = null;

        // U11 tells us the rotation of the lot in the lot file, ie. where the front of lot is:
        // To better visualize, use SimPE to open lot and look at Texture Image (TXTR) terrain pictures
        //    U11=0         U11=1         U11=2         U11=3
        // Faces Left     Faces Top    Faces Right   Faces Bottom
        // -----------   -----------   -----------   -----------
        // |F        |   |  FRONT  |   |        F|   |         |
        // |R        |   |         |   |        R|   |         |
        // |O        |   |         |   |        O|   |         |
        // |N        |   |         |   |        N|   |         |
        // |T        |   |         |   |        T|   |  FRONT  |
        // -----------   -----------   -----------   -----------
        private const byte U11_Left   = 0x00;
        private const byte U11_Top    = 0x01;
        private const byte U11_Right  = 0x02;
        private const byte U11_Bottom = 0x03;
        private byte U11;

        private const int Screen_Initial = 0;
        private const int Screen_Neighborhood = 1;
        private const int Screen_Lot = 2;
        private const int Screen_Advanced = 3;
        private const int Screen_Specification = 4;
        private const int Screen_Final = 5;
        private int Screen = Screen_Initial;

        private const int iMaxGrid = 128;

        private void NextButton_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            MultiBackup.Visible = false;
            UseLotCatalog.Visible = false;
            switch (Screen)
            {
                case Screen_Initial:
                    {
                        NeighborhoodScreen();
                        break;
                    }
                case Screen_Neighborhood:
                case Screen_Lot:
                    {
                        Liste_DoubleClick(sender, e);
                        break;
                    }
                case Screen_Advanced:
                case Screen_Specification:
                    {
                        MultiBackup.Visible = true;
                        FinalScreen();
                        break;
                    }
                case Screen_Final:
                    {
                        Explanation.Visible = false;
                        LotScreen();
                        break;
                    }
            }
            this.Cursor = Cursors.Default;
        }

        private bool bArrowUp = false;

        private void Liste_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyValue == 0x26) && (Liste.SelectedIndex > 1))
                bArrowUp = true;
        }

        private void Liste_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Liste.SelectedIndex != -1)
            {
                NextButton.Enabled = true;
                if (Screen == Screen_Neighborhood)
                {
                    string s = Liste.SelectedItem.ToString();
                    if ((string.Compare(s, "") == 0) || (s.IndexOf(":", 0) != -1))
                    {
                        if (bArrowUp)
                            Liste.SelectedIndex -= 1;
                        else if ((Liste.SelectedIndex + 1) < Liste.Items.Count)
                            Liste.SelectedIndex += 1;
                    }
                }
            }
            else
            {
                NextButton.Enabled = false;
            }
            bArrowUp = false;
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            MultiBackup.Visible = false;
            UseLotCatalog.Visible = false;
            switch (Screen)
            {
                case Screen_Initial:
                    {
                        this.Close();
                        break;
                    }
                case Screen_Final:
                    {
                        this.Close();
                        break;
                    }
                case Screen_Neighborhood:
                    {
                        InitialScreen();
                        break;
                    }
                case Screen_Lot:
                    {
                        NeighborhoodScreen();
                        break;
                    }
                case Screen_Advanced:
                case Screen_Specification:
                    {
                        LotScreen();
                        break;
                    }
            }
            this.Cursor = Cursors.Default;
        }

        private void Liste_DoubleClick(object sender, EventArgs e)
        {
            if (Liste.SelectedIndex > -1)
            {
                this.Cursor = Cursors.WaitCursor;
                switch (Screen)
                {
                    case Screen_Neighborhood:
                        {
                            LotScreen();
                            break;
                        }
                    case Screen_Lot:
                        {
                            SpecificationScreen();
                            break;
                        }
                }
            }
            this.Cursor = Cursors.Default;
        }

        private void InitialScreen()
        {
            Explanation.Visible = false;
            Liste.Visible = false;
            Title.Text = RMF.GetString("Title.Text");
            Explanation.Text = string.Format(RMF.GetString("Explanation.Text"), sVersionStrings[uVersionNumber]);
            Explanation.Visible = true;
            AdvancedButton.Visible = false;
            NextButton.Text = RMF.GetString("NextButton.Text");
            NextButton.Enabled = true;
            BackButton.Text = RMF.GetString("BackButton.Text");
            BackButton.Enabled = true;
            NextButton.Focus();
            Screen = Screen_Initial;
        }

        private string GetPath(bool bHood)
        {
            string sPath;
            string sMyDocs = null;
            string sEAGames = null;
            string sSims2 = null;
            string sNbhds = null;
            try
            {
                sMyDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Test_PrintDebugInfo)
                    Debug.Print("My Documents:  {0}", sMyDocs);
                sEAGames = Path.Combine(sMyDocs, "EA Games");
                if (Test_PrintDebugInfo)
                    Debug.Print("EA Games:      {0}", sEAGames);
                sSims2 = Path.Combine(sEAGames,
                    Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\EA Games\\The Sims 2").GetValue("DisplayName").ToString());
                if (Test_PrintDebugInfo)
                    Debug.Print("The Sims 2:    {0}", sSims2);
                if (bHood)
                {
                    sNbhds = Path.Combine(sSims2, "Neighborhoods");
                    if (Test_PrintDebugInfo)
                        Debug.Print("Neighborhoods: {0}", sNbhds);
                }
                else
                {
                    sNbhds = Path.Combine(sSims2, "LotCatalog");
                    if (Test_PrintDebugInfo)
                        Debug.Print("LotCatalog: {0}", sNbhds);
                }
            }
            catch
            {
            }
            sPath = sNbhds;
            if ((null == sPath) || !System.IO.Directory.Exists(sPath))
                sPath = sSims2;
            if ((null == sPath) || !System.IO.Directory.Exists(sPath))
                sPath = sEAGames;
            if ((null == sPath) || !System.IO.Directory.Exists(sPath))
                sPath = sMyDocs;
            if ((null == sPath) || !System.IO.Directory.Exists(sPath))
                sPath = "C:\\";

            return sPath;
        }

        private void NeighborhoodScreen()
        {
            LotProperties.Visible = false;
            UseLotCatalog.Checked = false;
            UseLotCatalog.Visible = true;
            // string[] dirs = { "E*", "F*", "G*", "N*" };
            string[] dirs = { "*" };
            if (null != NBPack)
            {
                // Neighborhood should be unchanged, but let's make sure
                NBPack.ForgetUpdate();
                NBPack.Close(true);
                NBPack = null;
            }
            this.Tag = 0;
            string sPath = GetPath(true);
            if( openFileDialog1.InitialDirectory == "")
                openFileDialog1.InitialDirectory = sPath;
            Liste.BeginUpdate();
            Liste.Items.Clear();
            Liste.Sorted = false;
            System.Collections.ArrayList FileList = new System.Collections.ArrayList();
            for (int h = 0; h < dirs.Length; h++)
            {
                string[] NBOrdner = Directory.GetDirectories(sPath, dirs[h]);
                Array.Sort(NBOrdner);
                for (int i = 0; i < NBOrdner.Length; i++)
                {
                    string sHoodName = Path.GetFileName(NBOrdner[i]);
                    if (0 == string.Compare("Tutorial", sHoodName))
                        continue;
                    if (FileList.Count > 0)
                    {
                        Liste.Items.Add("");
                        FileList.Add("");
                    }
                    Liste.Items.Add(sHoodName + ":");
                    FileList.Add("");

                    // List main neighborhood first
                    string[] MainNeighborhood = Directory.GetFiles(NBOrdner[i], Path.GetFileName(NBOrdner[i]) + "_Neighborhood.package");
                    Debug.Assert(MainNeighborhood.Length < 2);
                    if (MainNeighborhood.Length == 1)
                    {
                        try
                        {
                            GeneratableFile Package = SimPe.Packages.File.LoadFromFile(MainNeighborhood[0]);
                            FileList.Add(MainNeighborhood[0]);
                            IPackedFileDescriptor NBDescription = Package.FindFile(0x43545353, 0, 0xFFFFFFFF, 1);
                            IPackedFile PF = Package.Read(NBDescription);
                            int z = 0;
                            while (PF.UncompressedData[69 + z++] != 0)
                            {
                            }
                            byte[] BD = new byte[--z];
                            Array.Copy(PF.UncompressedData, 69, BD, 0, z);
                            string NBName = SimPe.Helper.ToString(BD);
                            NBName.Replace(":", ";");
                            Liste.Items.Add("    " + NBName);
                            // Print a list of neighborhood names for debugging purposes:
                            if (Test_PrintDebugInfo)
                                Debug.Print("{0}: {1}", Path.GetFileName(NBOrdner[i]), NBName);
                        }
                        catch
                        {
                        }
                    }

                    // Then list subneighborhoods
                    string[] AlleNBinOrdner = Directory.GetFiles(NBOrdner[i], "*.package");
                    for (int j = 0; j < AlleNBinOrdner.Length; j++)
                    {
                        // We've already done the main neighborhood...
                        if ((MainNeighborhood.Length == 1) && (MainNeighborhood[0] == AlleNBinOrdner[j]))
                            continue;
                        try
                        {
                            GeneratableFile Package = SimPe.Packages.File.LoadFromFile(AlleNBinOrdner[j]);
                            /* IPackedFileDescriptor[] */ LotPFDs = Package.FindFiles(0x0BF999E7);
                            // Skip hidden neighborhoods, like Pets, Weather (Seasons), and Exotic Destinations (Bon Voyage)
                            if (LotPFDs.Length == 0)
                                continue;
                            FileList.Add(AlleNBinOrdner[j]);
                            IPackedFileDescriptor NBDescription = Package.FindFile(0x43545353, 0, 0xFFFFFFFF, 1);
                            IPackedFile PF = Package.Read(NBDescription);
                            int z = 0;
                            while (PF.UncompressedData[69 + z++] != 0)
                            {
                            }
                            byte[] BD = new byte[--z];
                            Array.Copy(PF.UncompressedData, 69, BD, 0, z);
                            string NBName = SimPe.Helper.ToString(BD);
                            NBName.Replace(":", ";");
                            Liste.Items.Add("    " + NBName);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            Liste.EndUpdate();
            Liste.Tag = FileList;
            Liste.Visible = true;
            Title.Text = RME.GetString("NBTitle");
            Explanation.Visible = false;
            AdvancedButton.Visible = true;
            AdvancedButton.Text = RME.GetString("Browse");
            NextButton.Text = RME.GetString("Next");
            NextButton.Enabled = false;
            BackButton.Text = RME.GetString("Back");
            BackButton.Enabled = true;
            if (Liste.Items.Count == 0)
            {
                Title.Text = RME.GetString("NoNBTitle");
                Explanation.Text = RME.GetString("NoNeighborhood");
                Explanation.Visible = true;
                Liste.Visible = false;
                BackButton.Focus();
            }
            else
            {
                Liste.SelectedIndex = 1;
                Liste.Focus();
            }
            Screen = Screen_Neighborhood;
        }

        string LotPackName = null;

        private void AdvancedButton_Click(object sender, EventArgs e)
        {
            DialogResult res = DialogResult.OK;
            string sLotPath = (NBPack != null)
                ? Path.Combine(Path.GetDirectoryName(NBPack.FileName), "Lots") : GetPath(false);
            GeneratableFile LotPack = null;

            while (res == DialogResult.OK)
            {
                if (Screen == Screen_Neighborhood)
                    openFileDialog1.Filter = 
                        "Sims2 Neighborhood files (*.package))|*_Neighborhood.package;*_Downtown*.package;*_Suburb*.package;*_University*.package;*_Vacation*.package";
                else if (NBPack != null)
                    openFileDialog1.Filter = "Sims2 Lot files (*.package))|" + Path.GetFileName(NBPack.FileName).Substring(0, 4) + "_Lot*.package";
                else // LotCatalog
                    openFileDialog1.Filter = "Sims2 Lot files (*.package))|cx_*.package";
                openFileDialog1.FileName = "";
                openFileDialog1.FilterIndex = 0;
                openFileDialog1.RestoreDirectory = false;
                if (Screen == Screen_Neighborhood)
                {
                    openFileDialog1.Title = RME.GetString("NBTitle");
                    res = openFileDialog1.ShowDialog();
                    if (res == DialogResult.OK)
                    {
                        NBPack = SimPe.Packages.File.LoadFromFile(openFileDialog1.FileName);
                        if (NBPack != null)
                            break;
                        MessageBox.Show("Unable to open neighborhood", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else // if (Screen == Screen_Lot)
                {
                    openFileDialog1.InitialDirectory = sLotPath;
                    openFileDialog1.Title = RME.GetString("LotTitle");
                    res = openFileDialog1.ShowDialog();
                    if (res == DialogResult.OK)
                    {
                        // ToDo: ensure that selected lot belongs to selected neighborhood
                        LotPack = SimPe.Packages.File.LoadFromFile(openFileDialog1.FileName);
                        if (LotPack != null)
                        {
                            if (UseLotCatalog.Checked)
                            {
                                IPackedFileDescriptor LotDescriptor = LotPack.FindFile(0x6C589723, 0, 0xFFFFFFFF, 0);
                                R_LOT lLot = new R_LOT(LotPack, LotDescriptor);
                                // ToDo: Check whether valid lot
                                // Assume lot catalog cannot contain occupied apartment base or sublot?
                                LotPackName = openFileDialog1.FileName;
                                break;
                            }
                            else
                            {
                                try
                                {
                                    string sFilePath = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);
                                    if (!System.IO.Path.Equals(sLotPath, sFilePath))
                                        throw new Exception();
                                    string sFileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog1.FileName);
                                    string sInstance = sFileName.Substring(8); // N001_Lot#
                                    uint uInstance = 0;
                                    uInstance = Convert.ToUInt32(sInstance);
                                    IPackedFileDescriptor LotDescriptor = NBPack.FindFile(0x0BF999E7, 0, 0xFFFFFFFF, uInstance);
                                    R_DESC dLot = new R_DESC(NBPack, LotDescriptor, false);
                                    if (((dLot.LotType == 8) && dLot.Occupied) || (dLot.LotType == 9))
                                    {
                                        // Cannot modify occupied apartment base or sublot
                                        MessageBox.Show(
                                            RME.GetString("MessageOccupiedApartment"), RME.GetString("TitleOccupiedApartment"),
                                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        LotPack = null;
                                    }
                                    else
                                    {
                                        Liste.SelectedItem = dLot.LotName;
                                        LotPackName = openFileDialog1.FileName;
                                    }
                                    break;
                                }
                                catch
                                {
                                    MessageBox.Show("Unable to open lot. Selected lot may not belong to the selected neighborhood.",
                                        "Error: Cannot Open Lot", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                }
            }
            if (Screen == Screen_Neighborhood)
            {
                if ((res == DialogResult.OK) && (NBPack != null))
                    LotScreen();
                else
                    NeighborhoodScreen();
            }
            else
            {
                if ((res == DialogResult.OK) && ((NBPack != null) || UseLotCatalog.Checked) && (LotPack != null))
                {
                    SpecificationScreen();
                }
                else if (UseLotCatalog.Checked)
                {
                    Screen = Screen_Neighborhood;
                    NeighborhoodScreen();
                }
                else
                    LotScreen();
            }
        }

        private void LotScreen()
        {
            LotProperties.Visible = false;
            UseLotCatalog.Visible = false;
            if (UseLotCatalog.Checked)
            {
                if (null != NBPack)
                {
                    // Neighborhood should be unchanged, but let's make sure
                    NBPack.ForgetUpdate();
                    NBPack.Close(true);
                    NBPack = null;
                }
                Screen = Screen_Lot;
                AdvancedButton_Click(null, null);
                return;
            }
            else
            {
                if (null == NBPack)
                {
                    if ((int)this.Tag > 0)
                        Liste.SelectedIndex = (int)this.Tag;
                    else
                        this.Tag = Liste.SelectedIndex;
                    System.Collections.ArrayList Dateinamen = (System.Collections.ArrayList)Liste.Tag;
                    NBPack = SimPe.Packages.File.LoadFromFile(Dateinamen[Liste.SelectedIndex].ToString());
                }
                Liste.BeginUpdate();
                Liste.Items.Clear();
                LotPFDs = NBPack.FindFiles(0x0BF999E7);
                foreach (IPackedFileDescriptor LotDescriptor in LotPFDs)
                {
                    R_DESC dLot = new R_DESC(NBPack, LotDescriptor, false);
                    if (((dLot.LotType == 8) && dLot.Occupied) || (dLot.LotType == 9))
                        continue;   // Cannot modify occupied apartment base or sublot
                    Liste.Items.Add(dLot.LotName);
                    // Print a list of lot names for debugging purposes:
                    if (Test_PrintDebugInfo)
                        Debug.Print("Lot{0:D}:   {1}", LotDescriptor.Instance.ToString(), (string)dLot.LotName);
                }
            }
            Liste.Sorted = true;
            Liste.EndUpdate();
            Liste.Visible = true;
            Explanation.Visible = false;
            Title.Text = RME.GetString("LotTitle");
            AdvancedButton.Visible = true;
            AdvancedButton.Text = RME.GetString("Browse");
            BackButton.Enabled = true;
            BackButton.Text = RME.GetString("Back");
            NextButton.Enabled = false;
            NextButton.Text = RME.GetString("Next");
            if (Liste.Items.Count == 0)
            {
                Title.Text = RME.GetString("NoLotTitle");
                Explanation.Text = RME.GetString("NoLot");
                Explanation.Visible = true;
                Liste.Visible = false;
                AdvancedButton.Visible = false;
                BackButton.Focus();
            }
            else
            {
                Liste.SelectedIndex = 0;
                Liste.Focus();
            }
            Screen = Screen_Lot;
        }

        private void SpecificationScreen()
        {
            R_LotDescription ldLot = null;
            // ToDo: Why not keep LotPack and ldLot across function calls?
            // Already exists if LotCatalog or Browse
            // Need to open it anyway to get the list of walls
            if (!UseLotCatalog.Checked)
            {
                int iIndex = -1;
                foreach (IPackedFileDescriptor LotDescriptor in LotPFDs)
                {
                    ldLot = new R_DESC(NBPack, LotDescriptor, false);
                    iIndex++;
                    if ((string)ldLot.LotName == (string)Liste.SelectedItem)
                        break;
                }

                string sDirectory = Path.Combine(Path.GetDirectoryName(NBPack.FileName), "Lots");
                string sFileName = Path.GetFileName(NBPack.FileName);
                int iUnderscore = sFileName.IndexOf('_');
                string sPrefix = sFileName.Substring(0, iUnderscore);
                LotPackName = Path.Combine(sDirectory,
                     sPrefix + "_Lot" + LotPFDs[iIndex].Instance.ToString() + ".package");
            }
            GeneratableFile LotPack = SimPe.Packages.File.LoadFromFile(LotPackName);
            if (UseLotCatalog.Checked)
            {
                IPackedFileDescriptor LotDescriptor = LotPack.FindFile(0x6C589723, 0, 0xFFFFFFFF, 0);
                ldLot = new R_LOT(LotPack, LotDescriptor);
            }

            FromWall.Items.Clear();
            /*
            FromWall.Items.AddRange(new object[] {
                        RMF.GetString("FromWall.Items"),
                        RMF.GetString("FromWall.Items1"),
                        RMF.GetString("FromWall.Items2"),
                        RMF.GetString("FromWall.Items3"),
                        RMF.GetString("FromWall.Items4"),
                        RMF.GetString("FromWall.Items5"),
                        RMF.GetString("FromWall.Items6"),
                        RMF.GetString("FromWall.Items7"),
                        RMF.GetString("FromWall.Items8"),
                        RMF.GetString("FromWall.Items9"),
                        RMF.GetString("FromWall.Items10")});
             */

            // Find all wall IDs on lot and add to FromWall list (temporarily)
            uint[] uaWallIDs = new uint[0];
            IPackedFileDescriptor[] aPFD = LotPack.FindFiles(0x8A84D7B0);
            foreach (IPackedFileDescriptor IPFD in aPFD)
            {
                R_WLL ResWLL = new R_WLL(LotPack, IPFD);
                if (ResWLL.WallCount > 0)
                    ResWLL.AddWallIDs(ref uaWallIDs);
            }
            Array.Sort(uaWallIDs);
            for (int i = 0; i < uaWallIDs.Length; i++)
            {
                string s = GetWallString(uaWallIDs[i]);
                if (null == s)
                {
                    if (uaWallIDs[i] < 0xFFFF)  // Wall number
                        s = SimPe.Helper.ToString(uaWallIDs[i]);
                    else                        // GUID
                    {
                        s = string.Format("0x{0:X8}", uaWallIDs[i]);
                        continue;               // ToDo: handle GUIDs
                    }
                }
                FromWall.Items.Add(s);
            }
            if (0 == FromWall.Items.Count)
            {
                MessageBox.Show("No convertible walls on the lot.",
                    "Error: Cannot Find Walls", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LotScreen();
                return;
            }

            IPackedFileDescriptor PFD = LotPack.FindFile(0x0A284D0B, 0, 0xFFFFFFFF, 5);
            R_WGRA Res = new R_WGRA(LotPack, PFD);

            Title.Text = ldLot.LotName + ":";
            U11 = ldLot.U11;
            Explanation.Text = RME.GetString("SpecificationExpl");
            Explanation.Visible = true;
            Liste.Visible = false;
            LotProperties.Visible = true;
            LotProperties.BringToFront();

            // ToDo: take roads into consideration:
            FromLevel.Value = FromLevel.Minimum = ToLevel.Minimum = Res.MinimumLevel;
            ToLevel.Value = FromLevel.Maximum = ToLevel.Maximum = Res.MaximumLevel;
            FromFront.Minimum = ToFront.Minimum = 0;
            FromLeft.Value = FromLeft.Minimum = ToLeft.Minimum = 0;
            int ih = 0;
            int iw = 0;
            if ((U11 == 0) || (U11 == 2))
            {
                ih = ldLot.Width * 10;
                iw = ldLot.Height * 10;
            }
            else
            {
                ih = ldLot.Height * 10;
                iw = ldLot.Width * 10;
            }
            ToFront.Value = FromFront.Maximum = ToFront.Maximum = ih;
            FromFront.Value = 10;
            ToLeft.Value = FromLeft.Maximum = ToLeft.Maximum = iw;
            FromWall.SelectedIndex = 0;
            ToWall.Text = "23: foundation wall (brick)";

            AdvancedButton.Visible = false;
            BackButton.Enabled = true;
            BackButton.Visible = true;
            BackButton.Text = RME.GetString("Back");
            NextButton.Enabled = true;
            NextButton.Text = RME.GetString("Finish");
            NextButton.Focus();
            MultiBackup.Checked = bBackupVersioning;
            MultiBackup.Visible = true;
            Screen = Screen_Specification;
        }

        private void LevelChanged(object sender, EventArgs e)
        {
            FromLevel.Maximum = ToLevel.Value;
            ToLevel.Minimum = FromLevel.Value;
        }

        private void DepthChanged(object sender, EventArgs e)
        {
            FromFront.Maximum = ToFront.Value;
            ToFront.Minimum = FromFront.Value;
        }

        private void WidthChanged(object sender, EventArgs e)
        {
            FromLeft.Maximum = ToLeft.Value;
            ToLeft.Minimum = FromLeft.Value;
        }

        private void Swap(ref int X, ref int Y)
        {
            int iTemp = X;
            X = Y;
            Y = iTemp;
        }

        private uint GetWall(string s)
        {
            try
            {
                int i = s.IndexOf(':');
                if (-1 != i)
                    s = s.Substring(0, i);
                uint u = Convert.ToUInt32(s);
                return u;
            }
            catch
            {
            }
            return 0xFFFFFFFF;
        }

        private string GetWallString(uint uWall)
        {
            string s = null;
            foreach (string si in ToWall.Items)
            {
                try
                {
                    int i = si.IndexOf(':');
                    if (-1 != i)
                        s = si.Substring(0, i);
                    uint u = Convert.ToUInt32(s);
                    if( u == uWall)
                        return si;
                }
                catch
                {
                }
            }
            return null;
        }

        private void FinalScreen()
        {
            bool bAbort = false;
            string sAbort = "Unknown error";
            bool bUserAbort = false;
            int iChanged = 0;

            uint uFromWall = GetWall(FromWall.Text);
            uint uToWall = GetWall(ToWall.Text);
            if ((uFromWall == 0xFFFFFFFF) || (uToWall == 0xFFFFFFFF))
            {
                MessageBox.Show("Wall ID must be a number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Progress.Value = 0;
                Progress.Visible = false;
                return;
            }
            if (uFromWall == uToWall)
            {
                MessageBox.Show("Cannot change wall to itself", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Progress.Value = 0;
                Progress.Visible = false;
                return;
            }

            int iFromLevel = (int)FromLevel.Value;
            int iToLevel = (int)ToLevel.Value;
            float fFromX = 0;
            float fToX = 0;
            float fFromY = 0;
            float fToY = 0;

            if (U11_Left == U11)
            {
                fFromX = (float)FromFront.Value;
                fToX = (float)ToFront.Value;

                float fMax = (float)ToLeft.Maximum;
                fFromY = fMax - (float)ToLeft.Value;
                fToY = fMax - (float)FromLeft.Value;
            }
            else if (U11_Top == U11)
            {
                float fMax = (float)ToLeft.Maximum;
                fFromX = fMax - (float)ToLeft.Value;
                fToX = fMax - (float)FromLeft.Value;

                fMax = (float)ToFront.Maximum;
                fFromY = fMax - (float)ToFront.Value;
                fToY = fMax - (float)FromFront.Value;
            }
            else if (U11_Right == U11)
            {
                float fMax = (float)ToFront.Maximum;
                fFromX = fMax - (float)ToFront.Value;
                fToX = fMax - (float)FromFront.Value;

                fFromY = (float)FromLeft.Value;
                fToY = (float)ToLeft.Value;
            }
            else if (U11_Bottom == U11)
            {
                fFromX = (float)FromLeft.Value;
                fToX = (float)ToLeft.Value;

                fFromY = (float)FromFront.Value;
                fToY = (float)ToFront.Value;
            }

            if ((fFromX == fToX) && (fFromY == fToY))
            {
                MessageBox.Show("Walls cannot be zero-length", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Progress.Value = 0;
                Progress.Visible = false;
                return;
            }

            GeneratableFile LotPack = null;
            R_LotDescription ldLot = null;
#if !DEBUG
            try
#endif
            {
                // ToDo: Why not keep LotPack and ldLot across function calls?
                // Already exists from SpecificationScreen
                if (!UseLotCatalog.Checked)
                {
                    int iIndex = -1;
                    foreach (IPackedFileDescriptor LotDescriptor in LotPFDs)
                    {
                        ldLot = new R_DESC(NBPack, LotDescriptor, false);
                        iIndex++;
                        if ((string)ldLot.LotName == (string)Liste.SelectedItem)
                            break;
                    }

                    string sDirectory = Path.Combine(Path.GetDirectoryName(NBPack.FileName), "Lots");
                    string sFileName = Path.GetFileName(NBPack.FileName);
                    int iUnderscore = sFileName.IndexOf('_');
                    string sPrefix = sFileName.Substring(0, iUnderscore);
                    LotPackName = Path.Combine(sDirectory,
                         sPrefix + "_Lot" + LotPFDs[iIndex].Instance.ToString() + ".package");
                }
                LotPack = SimPe.Packages.File.LoadFromFile(LotPackName);
                if (UseLotCatalog.Checked)
                {
                    IPackedFileDescriptor LotDescriptor = LotPack.FindFile(0x6C589723, 0, 0xFFFFFFFF, 0);
                    ldLot = new R_LOT(LotPack, LotDescriptor);
                }

                IPackedFileDescriptor[] aPFD = LotPack.FindFiles(0x8A84D7B0);
                Progress.Maximum = aPFD.Length + 3;
                Progress.Value = 0;
                Progress.Visible = true;

                // Check that Sims 2 version of file
                // is not greater than the version that we know how to handle.
                IPackedFileDescriptor PFD = LotPack.FindFile(0xEBFEE342, 0, 0xFFFFFFFF, 0);
                if(null != PFD)
                {
                    if (Test_PrintDebugInfo)
                        Debug.Print("IPFD Type = EBFEE342 = VERS - Version  IPFD Instance = {0:X8}", PFD.Instance);
                    R_VERS Ver = new R_VERS(LotPack, PFD, sVersionStrings);
                    if (Ver.VersionNumber > uVersionNumber)
                    {
                        if (MessageBox.Show(
                            string.Format(RME.GetString("MessageWrongVer"), Ver.VersionString, sVersionStrings[uVersionNumber]),
                            RME.GetString("TitleWrongVer"),
                            MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button2)
                            == DialogResult.No)
                        {
                            bAbort = true;
                            bUserAbort = true;
                        }
                        else if (MessageBox.Show(
                            string.Format(RME.GetString("MessageCorruptLot"), Ver.VersionString, sVersionStrings[uVersionNumber]),
                            RME.GetString("TitleCorruptLot"),
                            MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button2)
                            == DialogResult.No)
                        {
                            bAbort = true;
                            bUserAbort = true;
                        }
                    }
                }
                Progress.Value += 1;

                R_WGRA.Clear();
                R_WGRA ResWGRA = null;
                PFD = LotPack.FindFile(0x0A284D0B, 0, 0xFFFFFFFF, 5);
                if ((null != PFD) && (!bAbort))
                {
                    if (Test_PrintDebugInfo)
                        Debug.Print("IPFD Type = 0A284D0B = WGRA - Wall Graph  IPFD Instance = {0:X8}", PFD.Instance);
                    ResWGRA = new R_WGRA(LotPack, PFD);
                    ResWGRA.Parse();
                }
                Progress.Value += 1;

                PFD = LotPack.FindFile(0x0A284D0B, 0, 0xFFFFFFFF, 0x18);
                if ((null != PFD) && (!bAbort))
                {
                    if (Test_PrintDebugInfo)
                        Debug.Print("IPFD Type = 0A284D0B = WGRA - Wall Graph  IPFD Instance = {0:X8}", PFD.Instance);
                    ResWGRA = new R_WGRA(LotPack, PFD);
                    ResWGRA.Parse();
                }
                Progress.Value += 1;

                if ((null != ResWGRA) && (!bAbort))
                {
                    foreach (IPackedFileDescriptor IPFD in aPFD)
                    {
                        R_WLL ResWLL = new R_WLL(LotPack, IPFD);
                        if (ResWLL.WallCount > 0)
                            iChanged += ResWLL.Change(ResWGRA, uFromWall, uToWall,
                                iFromLevel, iToLevel, fFromX, fToX, fFromY, fToY);
                        Progress.Value += 1;
                    }
                }
                if (iChanged == 0)
                {
                    MessageBox.Show("No walls match the criteria specified.", "No walls were changed.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Progress.Value = 0;
                    Progress.Visible = false;
                    return;
                }
            }
#if !DEBUG
            catch( Exception e)
            {
                // There was an unhandled exception.
                // Prevent unsavvy users from continuing.
                bAbort = true;
                sAbort = e.Message;
            }
#endif


            if (bAbort || Test_AlwaysAbort)
            {
                LotPack.ForgetUpdate();
                LotPack.Close();
                LotPack = null;

                if (null != NBPack)
                {
                    // Neighborhood should be unchanged, but let's make sure
                    NBPack.ForgetUpdate();
                    NBPack.Close();
                    NBPack = null;
                }

                if (bUserAbort)
                {
                    Title.Text = RME.GetString("Abort");
                    Explanation.Text = RME.GetString("ExplainUserAbort");
                }
                else
                {
                    // Will setting focus ensure that active form is not null? No.
                    Title.Text = sAbort;
                    Explanation.Visible = true;
                    Explanation.BringToFront();
                    Explanation.Text = RME.GetString("ExplainAbort");
                }
                Title.ForeColor = Color.Red;
                NextButton.Enabled = false;
            }
            else
            {
                PackageSave(LotPack);

                Explanation.Visible = true;
                Explanation.BringToFront();
                if( iChanged == 0)
                    Explanation.Text = RME.GetString("ChangedNoWalls");
                else if (iChanged == 1)
                    Explanation.Text = RME.GetString("ChangedWall");
                else
                    Explanation.Text = string.Format(RME.GetString("ChangedWalls"), iChanged);
            }
            Progress.Visible = false;
            AdvancedButton.Visible = false;
            NextButton.Visible = true;
            NextButton.Text = RME.GetString("Restart");
            BackButton.Visible = true;
            BackButton.Text = RMF.GetString("BackButton.Text");
            BackButton.Focus();
            MultiBackup.Visible = false;
            Screen = Screen_Final;
        }

        private uint MaxBKP(string sPath, string sExt, uint uMaxBKP)
        {
            string sDir = Path.GetDirectoryName(sPath);
            string sName = Path.GetFileNameWithoutExtension(sPath);

            string sBKP = string.Concat(sName, "_*");
            sBKP = Path.ChangeExtension(sBKP, sExt);

            // Find unique package file number for backup
            string[] sNames = System.IO.Directory.GetFiles(sDir, sBKP);
            for (int i = 0; i < sNames.Length; i++)
            {
                string s1 = Path.GetFileNameWithoutExtension(sNames[i]);
                string s2 = s1.Substring(sName.Length + 1);
                uint uCurrent = 0;
                try
                {
                    uCurrent = uint.Parse(s2);
                }
                catch (FormatException)
                {
                    continue;
                }
                if (uMaxBKP <= uCurrent)
                    uMaxBKP = uCurrent + 1;
            }
            return uMaxBKP;
        }

        private string BackupFileName(GeneratableFile Package, string sExt, bool bVersion, uint uVersion)
        {
            string sPath = Package.FileName;
            if (bVersion)
            {
                string sDir = Path.GetDirectoryName(sPath);
                string sName = Path.GetFileNameWithoutExtension(sPath);

                sPath = string.Concat(sDir, Path.DirectorySeparatorChar, sName);
                sPath = string.Concat(sPath, "_", uVersion.ToString());
                sPath = Path.ChangeExtension(sPath, sExt);  // add in backup extension
            }
            else
                sPath = Path.ChangeExtension(sPath, sExt);
            return sPath;
        }

        private void PackageSave(GeneratableFile Package)
        {
            string sExt = ".bkp";
            uint uMaxBKP = 0;
            if (bBackupVersioning)
                uMaxBKP = MaxBKP(Package.FileName, sExt, uMaxBKP);

            // This method exists only because SimPE GeneratableFile.Save() does not work here!
            string sPath = BackupFileName(Package, sExt, bBackupVersioning, uMaxBKP);
            System.IO.File.Copy(Package.FileName, sPath, true);
            MemoryStream MS = Package.Build();
            Package.Close();
            FileStream FS = new FileStream(Package.FileName, FileMode.Create, FileAccess.ReadWrite);
            FS.Seek(0, SeekOrigin.Begin);
            FS.SetLength(0);
            byte[] B = MS.ToArray();
            FS.Write(B, 0, B.Length);
            FS.Close();
        }

        private void PrimaryForm_Load(object sender, EventArgs e)
        {
            Explanation.Text = string.Format(RMF.GetString("Explanation.Text"), sVersionStrings[uVersionNumber]);

            // Remove the original default trace listener.
            // Debug.Listeners.RemoveAt(0);

            // Create a listener that outputs to the console screen, and add it to the debug listeners.
            // TextWriterTraceListener myWriter = new TextWriterTraceListener(System.Console.Out);
            // Debug.Listeners.Add(myWriter);

            try
            {
                sBackupConfigFile = Application.ExecutablePath;
                sBackupConfigFile = Path.GetDirectoryName(sBackupConfigFile);
                sBackupConfigFile = string.Concat(sBackupConfigFile, Path.DirectorySeparatorChar, "CWBKPVER.TXT");
                bBackupVersioning = System.IO.File.Exists(sBackupConfigFile);
            }
            catch
            {
            }
        }

        private void PrimaryForm_Shown(object sender, EventArgs e)
        {
            NextButton.Focus();
        }

        private void PrimaryForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (null != sBackupConfigFile)
            {
                try
                {
                    if (!bBackupVersioning)
                        System.IO.File.Delete(sBackupConfigFile);
                    else if (!System.IO.File.Exists(sBackupConfigFile))
                        System.IO.File.Create(sBackupConfigFile);
                }
                catch
                {
                }
            }
        }

        private void MultiBackup_CheckedChanged(object sender, EventArgs e)
        {
            bBackupVersioning = MultiBackup.Checked;
        }

        private void UseLotCatalog_CheckedChanged(object sender, EventArgs e)
        {
            Liste.Enabled = ! UseLotCatalog.Checked;
        }
    }
}


