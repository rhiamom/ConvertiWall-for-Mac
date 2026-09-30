/***************************************************************************
 *   macOS port © 2026 GramzeSweatshop                                     *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 ***************************************************************************/
// Headless stand-ins for the System.Windows.Forms types that Mootilda's
// PrimaryForm.cs touches (taken from LotAdjuster for Mac, where the same
// approach runs her LotExpander.cs). They live in her namespace (LotExpander),
// so her file binds to them ahead of its `using System.Windows.Forms;` and
// compiles UNCHANGED on macOS.
//
// They are not a UI. They hold the same state her form's controls held
// (Checked, Value, Text, Visible, Enabled...) and raise the same events, so her
// rules (the From/To range boxes limiting each other, the lot list skipping
// hood-folder headings...) run exactly as she wrote them. The Avalonia app and the
// SmokeTest read and set this state instead of drawing her form.
//
// Where WinForms behaviour matters to her logic it is reproduced:
//  - NumericUpDown: Value outside [Minimum, Maximum] throws; moving Minimum or
//    Maximum past Value drags Value along and raises ValueChanged.
//  - CheckBox: Checked raises CheckedChanged then CheckStateChanged, only on
//    an actual change.
//  - ProgressBar: Value outside [Minimum, Maximum] throws.
//  - ListBox: Sorted orders items by ToString(); SelectedIndex raises
//    SelectedIndexChanged.
//  - ComboBox: a DropDownList's Text is its selected item; an editable one
//    (her "to" wall) keeps typed text, and text matching an item selects it.
//  - MessageBox: routed to MessageBox.Handler; with no handler it answers
//    with the dialog's DEFAULT button (what pressing Return would do), the
//    choice she designed each prompt around.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace LotExpander
{
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);

    public class KeyEventArgs : EventArgs
    {
        public KeyEventArgs(int keyValue) { KeyValue = keyValue; }
        public int KeyValue { get; }
    }

    public class FormClosingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }

    public sealed class Cursor
    {
        private readonly string _name;
        internal Cursor(string name) { _name = name; }
        public override string ToString() => _name;
    }

    public static class Cursors
    {
        public static readonly Cursor Default = new Cursor("Default");
        public static readonly Cursor WaitCursor = new Cursor("WaitCursor");
    }

    public class Control
    {
        public string Name { get; set; } = "";
        public virtual string Text { get; set; } = "";
        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public Color ForeColor { get; set; } = SystemColors.WindowText;
        public Color BackColor { get; set; } = SystemColors.Control;
        public object Tag { get; set; }
        public Point Location { get; set; }
        public Size Size { get; set; }
        public Cursor Cursor { get; set; } = Cursors.Default;

        // Layout, from her Designer and resx. Her code never reads these
        // (except Location/Size in LotExpander_Shown); a host draws with them.
        public Control Parent { get; private set; }
        public List<Control> Controls { get; } = new List<Control>();
        public string Font { get; set; }            // e.g. "Microsoft Sans Serif, 11pt, style=Bold"
        public string TextAlign { get; set; }       // e.g. "TopRight"
        public bool Multiline { get; set; }
        public bool AutoSize { get; set; }
        public bool BorderNone { get; set; }        // BorderStyle.None
        public bool BorderSingle { get; set; }      // BorderStyle.FixedSingle
        public string ImageName { get; set; }       // Properties.Resources image

        // WinForms Controls.Add: the first control added is drawn on top.
        public void Add(params Control[] children)
        {
            foreach (var c in children) { c.Parent = this; Controls.Add(c); }
        }

        public event EventHandler Enter;
        public event EventHandler Leave;
        public event EventHandler MouseHover;
        public event EventHandler Click;
        public event EventHandler DoubleClick;

        public bool Focus() => true;

        // WinForms: move to the top of the parent's z-order (index 0 is drawn
        // on top). Her ConvertiWall final screen relies on it to cover the
        // settings group with the result text.
        public void BringToFront()
        {
            if (Parent == null) return;
            Parent.Controls.Remove(this);
            Parent.Controls.Insert(0, this);
        }

        // As in WinForms, a click only happens on a visible, enabled control.
        public bool PerformClick()
        {
            if (!Visible || !Enabled) return false;
            Click?.Invoke(this, EventArgs.Empty);
            return true;
        }
        public void PerformDoubleClick() => DoubleClick?.Invoke(this, EventArgs.Empty);
        public void PerformEnter() => Enter?.Invoke(this, EventArgs.Empty);
        public void PerformLeave() => Leave?.Invoke(this, EventArgs.Empty);
        public void PerformMouseHover() => MouseHover?.Invoke(this, EventArgs.Empty);

        public override string ToString() => Name;
    }

    public class Label : Control { }
    public class Button : Control { }
    public class PictureBox : Control { }
    public class Panel : Control { }
    public class GroupBox : Control { }

    public class TextBox : Control
    {
        public bool ReadOnly { get; set; }
    }

    public class CheckBox : Control
    {
        private bool _checked;

        public event EventHandler CheckedChanged;
        public event EventHandler CheckStateChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                CheckStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public class NumericUpDown : Control
    {
        private decimal _value;
        private decimal _minimum;
        private decimal _maximum = 100;

        public event EventHandler ValueChanged;

        public decimal Increment { get; set; } = 1;

        public decimal Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                if (value < _minimum || value > _maximum)
                    throw new ArgumentOutOfRangeException(nameof(Value),
                        $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'.");
                _value = value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_minimum > _maximum) _maximum = _minimum;
                Value = Constrain(_value);
            }
        }

        public decimal Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_minimum > _maximum) _minimum = _maximum;
                Value = Constrain(_value);
            }
        }

        private decimal Constrain(decimal v) => v < _minimum ? _minimum : (v > _maximum ? _maximum : v);
    }

    public class ProgressBar : Control
    {
        private int _value;

        public int Minimum { get; set; }
        public int Maximum { get; set; } = 100;

        public int Value
        {
            get => _value;
            set
            {
                if (value < Minimum || value > Maximum)
                    throw new ArgumentOutOfRangeException(nameof(Value),
                        $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'minimum' and 'maximum'.");
                _value = value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Not a WinForms event; lets a host show progress.
        public event EventHandler ValueChanged;
    }

    public class ObjectCollection : IEnumerable
    {
        private readonly ListControl _owner;
        internal readonly List<object> List = new List<object>();
        internal ObjectCollection(ListControl owner) { _owner = owner; }

        public int Count => List.Count;
        public object this[int index] => List[index];

        public int Add(object item)
        {
            List.Add(item);
            _owner.ItemsChanged();
            return List.IndexOf(item);
        }

        public void AddRange(object[] items)
        {
            List.AddRange(items);
            _owner.ItemsChanged();
        }

        public void Clear()
        {
            List.Clear();
            _owner.ClearSelection();
        }

        public IEnumerator GetEnumerator() => List.GetEnumerator();
    }

    public abstract class ListControl : Control
    {
        private int _selectedIndex = -1;

        protected ListControl() { Items = new ObjectCollection(this); }

        public ObjectCollection Items { get; }
        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value < -1 || value >= Items.Count)
                    throw new ArgumentOutOfRangeException(nameof(SelectedIndex));
                if (value == _selectedIndex) return;
                _selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get => _selectedIndex >= 0 ? Items[_selectedIndex] : null;
            set
            {
                int i = value == null ? -1 : Items.List.IndexOf(value);
                if (i == -1 && value != null)
                {
                    // WinForms matches a string against the items' text.
                    for (int k = 0; k < Items.Count; k++)
                        if (Equals(Items[k]?.ToString(), value.ToString())) { i = k; break; }
                }
                if (i != -1 || value == null)
                    SelectedIndex = i;
            }
        }

        public int FindStringExact(string s)
        {
            for (int i = 0; i < Items.Count; i++)
                if (string.Equals(Items[i]?.ToString(), s, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        internal virtual void ItemsChanged() { }
        internal void ClearSelection() { _selectedIndex = -1; }
    }

    public class ComboBox : ListControl
    {
        private string _text = "";

        public ComboBox() { SelectedIndexChanged += (s, e) => { if (SelectedIndex >= 0) _text = Items[SelectedIndex]?.ToString() ?? ""; }; }

        // ComboBoxStyle.DropDownList: only the list's items can be chosen.
        public bool DropDownList { get; set; }

        public override string Text
        {
            get => DropDownList ? (SelectedItem?.ToString() ?? "") : _text;
            set
            {
                value ??= "";
                int i = -1;
                for (int k = 0; k < Items.Count; k++)
                    if (Items[k]?.ToString() == value) { i = k; break; }
                if (DropDownList && i == -1) return;
                _text = value;
                SelectedIndex = i;
            }
        }
    }

    public class ListBox : ListControl
    {
        private bool _sorted;
        private int _updateDepth;

        public event KeyEventHandler KeyDown;

        public bool Sorted
        {
            get => _sorted;
            set { _sorted = value; if (value) Sort(); }
        }

        public void BeginUpdate() { _updateDepth++; }
        public void EndUpdate() { if (_updateDepth > 0) _updateDepth--; }

        public void PerformKeyDown(int keyValue) => KeyDown?.Invoke(this, new KeyEventArgs(keyValue));

        internal override void ItemsChanged() { if (_sorted) Sort(); }

        private void Sort()
        {
            object selected = SelectedItem;
            Items.List.Sort((a, b) => string.Compare(a?.ToString(), b?.ToString(), StringComparison.CurrentCulture));
            if (selected != null)
            {
                ClearSelection();
                SelectedItem = selected;
            }
        }
    }

    public class Form : Control
    {
        public int Width { get; set; }

        public event EventHandler Load;
        public event EventHandler Shown;
        public event FormClosingEventHandler FormClosing;

        public bool IsClosed { get; private set; }

        // Raise Load then Shown, as WinForms does when a form is first shown.
        public void Show()
        {
            Load?.Invoke(this, EventArgs.Empty);
            Shown?.Invoke(this, EventArgs.Empty);
        }

        public void Close()
        {
            if (IsClosed) return;
            var e = new FormClosingEventArgs();
            FormClosing?.Invoke(this, e);
            if (!e.Cancel) IsClosed = true;
        }
    }

    public enum DialogResult { None = 0, OK = 1, Cancel = 2, Abort = 3, Retry = 4, Ignore = 5, Yes = 6, No = 7 }
    public enum MessageBoxButtons { OK = 0, OKCancel = 1, AbortRetryIgnore = 2, YesNoCancel = 3, YesNo = 4, RetryCancel = 5 }
    public enum MessageBoxIcon { None = 0, Error = 16, Question = 32, Warning = 48, Information = 64 }
    public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }

    public sealed class MessageRequest
    {
        public string Text { get; init; } = "";
        public string Caption { get; init; } = "";
        public MessageBoxButtons Buttons { get; init; }
        public MessageBoxIcon Icon { get; init; }
        public MessageBoxDefaultButton DefaultButton { get; init; }

        // The result pressing Return would give in WinForms.
        public DialogResult DefaultResult
        {
            get
            {
                DialogResult[] r = Buttons switch
                {
                    MessageBoxButtons.OKCancel => new[] { DialogResult.OK, DialogResult.Cancel },
                    MessageBoxButtons.AbortRetryIgnore => new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore },
                    MessageBoxButtons.YesNoCancel => new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel },
                    MessageBoxButtons.YesNo => new[] { DialogResult.Yes, DialogResult.No },
                    MessageBoxButtons.RetryCancel => new[] { DialogResult.Retry, DialogResult.Cancel },
                    _ => new[] { DialogResult.OK },
                };
                int i = DefaultButton == MessageBoxDefaultButton.Button3 ? 2
                      : DefaultButton == MessageBoxDefaultButton.Button2 ? 1 : 0;
                return r[Math.Min(i, r.Length - 1)];
            }
        }
    }

    public static class MessageBox
    {
        // Set by the host (Avalonia app / SmokeTest). Called on whatever thread
        // runs her code; the host is responsible for marshalling to its UI.
        public static Func<object, MessageRequest, DialogResult> Handler;

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
            => Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            => Show(null, text, caption, buttons, icon, defaultButton);

        public static DialogResult Show(object owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
            => Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(object owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            var req = new MessageRequest
            {
                Text = text, Caption = caption, Buttons = buttons, Icon = icon, DefaultButton = defaultButton,
            };
            return Handler != null ? Handler(owner, req) : req.DefaultResult;
        }
    }

    public class OpenFileDialog
    {
        public string InitialDirectory { get; set; } = "";
        public string Filter { get; set; } = "";
        public string FileName { get; set; } = "";
        public int FilterIndex { get; set; }
        public bool RestoreDirectory { get; set; }
        public string Title { get; set; } = "";

        // Her Browse... button. Headless there is nothing to show, so it is
        // cancelled unless a host supplies a picker.
        public static Func<OpenFileDialog, string> Handler;

        public DialogResult ShowDialog()
        {
            string path = Handler?.Invoke(this);
            if (string.IsNullOrEmpty(path)) return DialogResult.Cancel;
            FileName = path;
            return DialogResult.OK;
        }
    }

    public static class Application
    {
        // PrimaryForm_Load keeps her "versioned backups" setting as a marker
        // file CWBKPVER.TXT beside the executable. Inside a signed macOS .app
        // that would modify the bundle, so the marker's folder is
        // ~/Library/Application Support/ConvertiWall for Mac instead.
        public static string ExecutablePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "ConvertiWall for Mac");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "ConvertiWall");
            }
        }
    }
}
