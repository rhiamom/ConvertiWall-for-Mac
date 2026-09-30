/***************************************************************************
 *   macOS port © 2026 GramzeSweatshop                                     *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 ***************************************************************************/
// The headless half of Mootilda's PrimaryForm. It stands in for her
// PrimaryForm.Designer.cs, which is pure WinForms layout and does not come
// to macOS. Her PrimaryForm.cs is compiled UNCHANGED beside it.
//
// InitializeComponent below declares the same controls under the same names
// (as stand-ins from WinFormsHeadless.cs), applies the same starting values
// (her resx strings, Visible/Enabled flags and layout via PrimaryForm.resx,
// plus the few values her Designer set in code), and wires exactly the same
// event handlers her Designer wired, in the same order.
//
// The controls are public so a host (the Avalonia app, the SmokeTest) can
// read and set them. A host drives her form the way a user did: pick list
// entries, set the wall boxes and ranges, then PerformClick() her buttons.

using System;
using System.Globalization;
using System.Resources;

namespace LotExpander
{
    public partial class PrimaryForm : Form
    {
        #region Controls (names and types as in PrimaryForm.Designer.cs)
        public Label Title;
        public ListBox Liste;
        public Button NextButton;
        public Button BackButton;
        public TextBox Explanation;
        public Button AdvancedButton;
        public ProgressBar Progress;
        public GroupBox LotProperties;
        public Label ToWallLabel;
        public Label FromWallLabel;
        public ComboBox FromWall;
        public ComboBox ToWall;
        public GroupBox groupBox1;
        public Label ToLabel;
        public Label FromLabel;
        public NumericUpDown ToLeft;
        public Label LeftLabel;
        public NumericUpDown FromLeft;
        public NumericUpDown ToFront;
        public Label FrontLabel;
        public NumericUpDown FromFront;
        public NumericUpDown ToLevel;
        public Label LevelLabel;
        public NumericUpDown FromLevel;
        public CheckBox MultiBackup;
        public CheckBox UseLotCatalog;
        #endregion

        private ResourceManager _designerResources;

        private string Res(string key) => _designerResources.GetString(key, CultureInfo.InvariantCulture);

        private T Make<T>(string name) where T : Control, new()
        {
            var c = new T { Name = name };
            // ApplyResources: the Text, Visible and Enabled her resx holds.
            string text = Res(name + ".Text");
            if (text != null) c.Text = text;
            string visible = Res(name + ".Visible");
            if (visible != null) c.Visible = bool.Parse(visible);
            string enabled = Res(name + ".Enabled");
            if (enabled != null) c.Enabled = bool.Parse(enabled);
            // Layout values, for hosts that draw the form.
            string location = Res(name + ".Location");
            if (location != null) { var v = Pair(location); c.Location = new System.Drawing.Point(v.Item1, v.Item2); }
            string size = Res(name + ".Size");
            if (size != null) { var v = Pair(size); c.Size = new System.Drawing.Size(v.Item1, v.Item2); }
            c.Font = Res(name + ".Font");
            c.TextAlign = Res(name + ".TextAlign");
            c.Multiline = Res(name + ".Multiline") == "True";
            c.AutoSize = Res(name + ".AutoSize") == "True";
            return c;
        }

        private static (int, int) Pair(string s)
        {
            string[] p = s.Split(',');
            return (int.Parse(p[0].Trim(), CultureInfo.InvariantCulture), int.Parse(p[1].Trim(), CultureInfo.InvariantCulture));
        }

        private object[] WallItems(string name)
        {
            var items = new object[11];
            for (int i = 0; i < items.Length; i++)
                items[i] = Res(name + ".Items" + (i == 0 ? "" : i.ToString(CultureInfo.InvariantCulture)));
            return items;
        }

        private void InitializeComponent()
        {
            _designerResources = new ResourceManager("LotExpander.PrimaryForm", typeof(PrimaryForm).Assembly);
            Name = "PrimaryForm";
            Text = Res("$this.Text") ?? "ConvertiWall";

            Title = Make<Label>("Title");
            Liste = Make<ListBox>("Liste");
            NextButton = Make<Button>("NextButton");
            BackButton = Make<Button>("BackButton");
            Explanation = Make<TextBox>("Explanation");
            AdvancedButton = Make<Button>("AdvancedButton");
            Progress = Make<ProgressBar>("Progress");
            LotProperties = Make<GroupBox>("LotProperties");
            groupBox1 = Make<GroupBox>("groupBox1");
            ToLabel = Make<Label>("ToLabel");
            FromLabel = Make<Label>("FromLabel");
            ToLeft = Make<NumericUpDown>("ToLeft");
            LeftLabel = Make<Label>("LeftLabel");
            FromLeft = Make<NumericUpDown>("FromLeft");
            ToFront = Make<NumericUpDown>("ToFront");
            FrontLabel = Make<Label>("FrontLabel");
            FromFront = Make<NumericUpDown>("FromFront");
            ToLevel = Make<NumericUpDown>("ToLevel");
            LevelLabel = Make<Label>("LevelLabel");
            FromLevel = Make<NumericUpDown>("FromLevel");
            ToWall = Make<ComboBox>("ToWall");
            ToWallLabel = Make<Label>("ToWallLabel");
            FromWallLabel = Make<Label>("FromWallLabel");
            FromWall = Make<ComboBox>("FromWall");
            MultiBackup = Make<CheckBox>("MultiBackup");
            UseLotCatalog = Make<CheckBox>("UseLotCatalog");

            // Values her Designer set in code rather than through the resx.
            Explanation.BackColor = System.Drawing.SystemColors.Control;
            Explanation.BorderNone = true;
            Explanation.ReadOnly = true;
            ToWall.Items.AddRange(WallItems("ToWall"));
            FromWall.DropDownList = true;
            FromWall.Items.AddRange(WallItems("FromWall"));
            string clientSize = Res("$this.ClientSize");
            if (clientSize != null) { var v = Pair(clientSize); Size = new System.Drawing.Size(v.Item1, v.Item2); }

            // Containment, in her Designer's Controls.Add order.
            LotProperties.Add(groupBox1, ToWall, ToWallLabel, FromWallLabel, FromWall, Progress);
            groupBox1.Add(ToLabel, FromLabel, ToLeft, LeftLabel, FromLeft, ToFront, FrontLabel, FromFront,
                ToLevel, LevelLabel, FromLevel);
            Add(UseLotCatalog, MultiBackup, LotProperties, AdvancedButton, NextButton, BackButton, Title, Liste,
                Explanation);

            // Event wiring, in her Designer's order.
            Liste.DoubleClick += new EventHandler(Liste_DoubleClick);
            Liste.SelectedIndexChanged += new EventHandler(Liste_SelectedIndexChanged);
            Liste.KeyDown += new KeyEventHandler(Liste_KeyDown);
            NextButton.Click += new EventHandler(NextButton_Click);
            BackButton.Click += new EventHandler(BackButton_Click);
            AdvancedButton.Click += new EventHandler(AdvancedButton_Click);
            ToLeft.ValueChanged += new EventHandler(WidthChanged);
            FromLeft.ValueChanged += new EventHandler(WidthChanged);
            ToFront.ValueChanged += new EventHandler(DepthChanged);
            FromFront.ValueChanged += new EventHandler(DepthChanged);
            ToLevel.ValueChanged += new EventHandler(LevelChanged);
            FromLevel.ValueChanged += new EventHandler(LevelChanged);
            MultiBackup.CheckedChanged += new EventHandler(MultiBackup_CheckedChanged);
            UseLotCatalog.CheckedChanged += new EventHandler(UseLotCatalog_CheckedChanged);
            Shown += new EventHandler(PrimaryForm_Shown);
            FormClosing += new FormClosingEventHandler(PrimaryForm_FormClosing);
            Load += new EventHandler(PrimaryForm_Load);
        }

        #region Host API (not in her form)
        // What her neighborhood list did on double-click, for a host that
        // already knows the package: set NBPack, then show the Lot screen.
        public void OpenNeighborhood(string packagePath)
        {
            if (NBPack != null)
                NBPack.Close(true);
            UseLotCatalog.Checked = false;
            NBPack = SimPe.Packages.File.LoadFromFile(packagePath);
            this.Tag = 0;
            Screen = Screen_Neighborhood;
            LotScreen();
        }

        public int CurrentScreen => Screen;
        public const int ScreenInitial = Screen_Initial;
        public const int ScreenNeighborhood = Screen_Neighborhood;
        public const int ScreenLot = Screen_Lot;
        public const int ScreenAdvanced = Screen_Advanced;
        public const int ScreenSpecification = Screen_Specification;
        public const int ScreenFinal = Screen_Final;

        // The lot package the Specification screen opened (null before).
        public string LotPackageName => LotPackName;

        // The lot's front direction (her U11), set by the Specification screen.
        public byte LotRotation => U11;

        public string NeighborhoodFileName => NBPack?.FileName;
        #endregion
    }
}
