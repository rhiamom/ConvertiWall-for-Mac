/**************************************************************************
 *   ConvertiWall for Mac                                                 *
 *   ConvertiWall © 2009-2010 Mootilda (http://Mootilda.ModTheSims.info)  *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Draws Mootilda's form and hands every click to her own code.         *
 *************************************************************************/

// Her PrimaryForm (PrimaryForm.cs, unchanged) runs headless: its controls are
// the stand-ins from WinFormsHeadless.cs. This window draws one Avalonia
// control per stand-in, at her Designer's positions, sizes and fonts, and
// keeps the two in step:
//
//   user input  -> set the stand-in (Checked, Value, SelectedIndex, Text) or
//                  PerformClick(); her event handlers run
//   Refresh()   -> copy every stand-in's Visible/Enabled/Text/Checked/Value
//                  back onto the screen
//
// Button clicks run her code on a worker thread, because her handlers call
// MessageBox.Show / OpenFileDialog.ShowDialog synchronously. Those calls hop
// to the UI thread and wait for the answer. Checkbox, number and list changes
// never show a dialog, so they run directly on the UI thread.
//
// (Adapted from LotAdjuster for Mac's window, which does the same for her
// LotAdjuster form.)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using WF = LotExpander;

namespace ConvertiWall.App.Views;

public sealed class MainWindow : Window
{
    private const double CheckBoxHeight = 32;
    private static readonly IBrush FormBack = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

    private readonly WF.PrimaryForm _form;
    private readonly Canvas _root;
    private readonly List<Action> _refreshers = new();
    private Avalonia.Controls.ProgressBar? _progress;
    private bool _refreshing;
    private bool _busy;

    public MainWindow()
    {
        Title = "ConvertiWall for Mac";
        CanResize = false;
        Background = FormBack;
        FontFamily = new FontFamily("Arial");
        SizeToContent = SizeToContent.Manual;

        WF.MessageBox.Handler = AskFromWorker;
        WF.OpenFileDialog.Handler = PickFromWorker;

        _form = new WF.PrimaryForm();
        _root = new Canvas
        {
            Width = _form.Size.Width, Height = _form.Size.Height, ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        BuildChildren(_form, _root, "Microsoft Sans Serif, 8.25pt");
        Content = _root;
        Width = _form.Size.Width;
        Height = _form.Size.Height;

        _form.Progress.ValueChanged += (_, _) => Dispatcher.UIThread.Post(RefreshProgress);
        _form.Show();   // her Load and Shown handlers
        Refresh();

        Closing += (_, _) => SaveHerSettings();
    }

    // Her FormClosing keeps the "Versioned backups" choice (a marker file).
    // Closing the window raises it; quitting with Cmd-Q may not close the
    // window first, so App also calls this on shutdown.
    public void SaveHerSettings()
    {
        if (!_form.IsClosed) _form.Close();
    }

    #region Building the controls

    private void BuildChildren(WF.Control parent, Canvas canvas, string parentFont)
    {
        // WinForms draws the first-added control on top; Avalonia the last.
        foreach (WF.Control c in Enumerable.Reverse(parent.Controls))
        {
            string font = c.Font ?? parentFont;
            Avalonia.Controls.Control ui = Create(c, font);
            Canvas.SetLeft(ui, c.Location.X);
            // The Mac checkbox needs its full height; centre it on her row.
            Canvas.SetTop(ui, c is WF.CheckBox ? c.Location.Y + (c.Size.Height - CheckBoxHeight) / 2.0 : c.Location.Y);
            canvas.Children.Add(ui);

            ui.PointerEntered += (_, _) => Do(c.PerformMouseHover);
            ui.GotFocus += (_, _) => Do(c.PerformEnter);
            ui.LostFocus += (_, _) => Do(c.PerformLeave);

            _refreshers.Add(() =>
            {
                ui.IsVisible = c.Visible;
                ui.IsEnabled = c.Enabled;
                ui.ZIndex = parent.Controls.Count - parent.Controls.IndexOf(c);   // follows BringToFront
            });
        }
    }

    private Avalonia.Controls.Control Create(WF.Control c, string font)
    {
        var (size, bold) = ParseFont(font);
        switch (c)
        {
            case WF.GroupBox g: return MakeGroup(g, font, size, bold);
            case WF.Panel p:
            {
                var canvas = new Canvas { Width = p.Size.Width, Height = p.Size.Height };
                BuildChildren(p, canvas, font);
                return canvas;
            }
            case WF.CheckBox cb: return MakeCheckBox(cb, size, bold);
            case WF.NumericUpDown n: return MakeNumber(n, size, bold);
            case WF.ListBox lb: return MakeList(lb, size, bold);
            case WF.ComboBox co: return MakeCombo(co, size, bold);
            case WF.Button b: return MakeButton(b, size, bold);
            case WF.ProgressBar pr: return MakeProgress(pr);
            case WF.TextBox t: return MakeText(t, size, bold);
            default: return MakeLabel(c, size, bold);
        }
    }

    private Avalonia.Controls.Control MakeGroup(WF.GroupBox g, string font, double size, bool bold)
    {
        var canvas = new Canvas { Width = g.Size.Width, Height = g.Size.Height, Background = Brushes.Transparent };
        var frame = new Border
        {
            Width = g.Size.Width, Height = g.Size.Height - 8,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
        };
        Canvas.SetTop(frame, 8);
        var header = new TextBlock { FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Background = FormBack, Padding = new Thickness(2, 0) };
        Canvas.SetLeft(header, 6);
        canvas.Children.Add(frame);
        canvas.Children.Add(header);
        BuildChildren(g, canvas, font);
        _refreshers.Add(() => header.Text = StripMnemonic(g.Text));
        return canvas;
    }

    private Avalonia.Controls.Control MakeCheckBox(WF.CheckBox c, double size, bool bold)
    {
        var text = new TextBlock { FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
        var ui = new Avalonia.Controls.CheckBox
        {
            Content = text, MinHeight = 0, MinWidth = 0, Height = CheckBoxHeight,
            Padding = new Thickness(string.IsNullOrEmpty(c.Text) ? 0 : 4, 0, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ui.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            bool want = ui.IsChecked == true;
            Do(() => c.Checked = want);
        };
        _refreshers.Add(() =>
        {
            text.Text = StripMnemonic(c.Text);
            text.Foreground = Brush(c.ForeColor, EffectivelyEnabled(c));
            ui.IsChecked = c.Checked;
        });
        return ui;
    }

    // A WinForms-sized NumericUpDown: text box plus small up/down arrows.
    // (Avalonia's own spinner needs more width than her 58-pixel boxes.)
    private Avalonia.Controls.Control MakeNumber(WF.NumericUpDown n, double size, bool bold)
    {
        var box = new TextBox
        {
            FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            MinHeight = 0, MinWidth = 0, Padding = new Thickness(3, 0, 5, 0), BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        // WinForms NumericUpDown text is left-aligned unless her form says otherwise.
        if ((n.TextAlign ?? "").EndsWith("Right"))
        {
            box.HorizontalContentAlignment = HorizontalAlignment.Right;
            box.TextAlignment = TextAlignment.Right;
        }
        RepeatButton Arrow(string glyph) => new RepeatButton
        {
            Content = new TextBlock { Text = glyph, FontSize = 7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 15, MinHeight = 0, Padding = new Thickness(0), CornerRadius = new CornerRadius(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        var up = Arrow("▲");
        var down = Arrow("▼");
        var arrows = new Grid { RowDefinitions = new RowDefinitions("*,*") };
        Grid.SetRow(down, 1);
        up.VerticalAlignment = down.VerticalAlignment = VerticalAlignment.Stretch;
        arrows.Children.Add(up);
        arrows.Children.Add(down);
        var ui = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Width = n.Size.Width, Height = n.Size.Height };
        Grid.SetColumn(arrows, 1);
        ui.Children.Add(box);
        ui.Children.Add(arrows);

        // WinForms clamps to the range instead of throwing.
        void Set(decimal v) => Do(() => n.Value = Math.Clamp(v, n.Minimum, n.Maximum));
        up.Click += (_, _) => Set(n.Value + n.Increment);
        down.Click += (_, _) => Set(n.Value - n.Increment);
        void Commit()
        {
            if (_refreshing) return;
            if (decimal.TryParse(box.Text, out decimal v)) Set(v);
            else Refresh();
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Commit();
            else if (e.Key == Key.Up) { Set(n.Value + n.Increment); e.Handled = true; }
            else if (e.Key == Key.Down) { Set(n.Value - n.Increment); e.Handled = true; }
        };
        box.LostFocus += (_, _) => Commit();
        _refreshers.Add(() => box.Text = n.Value.ToString("0"));
        return ui;
    }

    private Avalonia.Controls.Control MakeList(WF.ListBox l, double size, bool bold)
    {
        var items = new List<string>();
        var ui = new ListBox
        {
            Width = l.Size.Width, Height = l.Size.Height, FontSize = size,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
        };
        ui.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(4, 1)),
                new Avalonia.Styling.Setter(ListBoxItem.MinHeightProperty, 0.0),
            },
        });
        ui.SelectionChanged += (_, _) =>
        {
            if (_refreshing || ui.SelectedIndex < 0) return;
            int i = ui.SelectedIndex;
            Do(() => l.SelectedIndex = i);
        };
        ui.DoubleTapped += (_, _) => RunAsync(l.PerformDoubleClick);
        // Her Liste_KeyDown notes an Up arrow before the selection moves, so
        // she can skip header lines in the right direction.
        ui.AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Up) l.PerformKeyDown(0x26); },
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _refreshers.Add(() =>
        {
            var now = l.Items.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
            if (!now.SequenceEqual(items))
            {
                items = now;
                ui.ItemsSource = now.ToList();
            }
            ui.SelectedIndex = l.SelectedIndex;
            if (l.SelectedIndex >= 0) ui.ScrollIntoView(l.SelectedIndex);
        });
        return ui;
    }

    // Her "change walls with this ID" list is a DropDownList whose items her
    // Specification screen fills from the lot; her "to this ID" box is an
    // editable combo, so any wall number can be typed as well as picked.
    private Avalonia.Controls.Control MakeCombo(WF.ComboBox c, double size, bool bold)
    {
        var items = new List<string>();
        var ui = new ComboBox
        {
            Width = c.Size.Width + 20, Height = c.Size.Height + 4, MinHeight = 0, FontSize = size,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Padding = new Thickness(6, 0),
            IsEditable = !c.DropDownList, VerticalContentAlignment = VerticalAlignment.Center,
        };
        // The editable combo's text box brings Fluent's own padding and height,
        // which clip in her 21-pixel row.
        ui.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<ComboBox>().Descendant().OfType<TextBox>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(TextBox.MinHeightProperty, 0.0),
                new Avalonia.Styling.Setter(TextBox.PaddingProperty, new Thickness(0)),
                new Avalonia.Styling.Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            },
        });
        ui.SelectionChanged += (_, _) =>
        {
            if (_refreshing || ui.SelectedIndex < 0) return;
            int i = ui.SelectedIndex;
            Do(() => c.SelectedIndex = i);
        };
        if (ui.IsEditable)
            ui.PropertyChanged += (_, e) =>
            {
                if (e.Property != ComboBox.TextProperty || _refreshing || _busy) return;
                string typed = ui.Text ?? "";
                if (typed != c.Text) Do(() => c.Text = typed);
            };
        _refreshers.Add(() =>
        {
            var now = c.Items.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
            if (!now.SequenceEqual(items))
            {
                items = now;
                ui.ItemsSource = now.ToList();
            }
            ui.SelectedIndex = c.SelectedIndex;
            if (ui.IsEditable && ui.Text != c.Text) ui.Text = c.Text;
        });
        return ui;
    }

    private Avalonia.Controls.Control MakeButton(WF.Button b, double size, bool bold)
    {
        var ui = new Avalonia.Controls.Button
        {
            Width = b.Size.Width, Height = b.Size.Height, MinHeight = 0, Padding = new Thickness(2, 0),
            FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        };
        ui.Click += (_, _) => RunAsync(() => b.PerformClick());
        _refreshers.Add(() => ui.Content = StripMnemonic(b.Text));
        return ui;
    }

    private Avalonia.Controls.Control MakeProgress(WF.ProgressBar p)
    {
        _progress = new Avalonia.Controls.ProgressBar { Width = p.Size.Width, Height = p.Size.Height, MinHeight = 0 };
        _refreshers.Add(RefreshProgress);
        return _progress;
    }

    private Avalonia.Controls.Control MakeText(WF.TextBox t, double size, bool bold)
    {
        // A borderless one-line read-only box is really a label; draw it as
        // text so Mac fonts don't clip.
        if (t.BorderNone && !t.Multiline)
            return MakeLabel(t, size, bold);
        if (t.BorderNone && t.ReadOnly)
            return MakeReadOnlyText(t, size, bold);

        var ui = new TextBox
        {
            Width = t.Size.Width, Height = t.Size.Height, MinHeight = 0, FontSize = size,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            IsReadOnly = t.ReadOnly, TextWrapping = t.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = t.Multiline, Padding = new Thickness(0),
        };
        if (t.BorderNone)
        {
            ui.Background = Brushes.Transparent;
            ui.BorderThickness = new Thickness(0);
        }
        _refreshers.Add(() =>
        {
            ui.Text = t.Text;
            ui.Foreground = Brush(t.ForeColor);
        });
        return ui;
    }

    // Her borderless multiline read-only box (Explanation). Opaque and full
    // size, like the WinForms box, so it hides what it is brought in front of.
    private Avalonia.Controls.Control MakeReadOnlyText(WF.TextBox t, double size, bool bold)
    {
        var ui = new SelectableTextBlock
        {
            Width = t.Size.Width, Height = t.Size.Height, Background = FormBack, FontSize = size,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap,
        };
        _refreshers.Add(() =>
        {
            ui.Foreground = Brush(t.ForeColor);
            ui.Text = (t.Text ?? "").Replace("\r\n", "\n").TrimEnd();
        });
        return ui;
    }

    private Avalonia.Controls.Control MakeLabel(WF.Control c, double size, bool bold)
    {
        var ui = new TextBlock
        {
            FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            TextWrapping = c.AutoSize ? TextWrapping.NoWrap : TextWrapping.Wrap,
        };
        if ((!c.AutoSize && c is not WF.TextBox) || (c.TextAlign ?? "").EndsWith("Right"))
            ui.Width = c.Size.Width;
        if ((c.TextAlign ?? "").EndsWith("Right"))
            ui.TextAlignment = TextAlignment.Right;
        ui.PointerPressed += (_, _) => Do(() => c.PerformClick());
        _refreshers.Add(() =>
        {
            ui.Text = StripMnemonic(c.Text);
            ui.Foreground = Brush(c.ForeColor, EffectivelyEnabled(c));
        });
        return ui;
    }

    #endregion

    #region Keeping screen and stand-ins in step

    private void Refresh()
    {
        _refreshing = true;
        try
        {
            foreach (var r in _refreshers) r();
        }
        finally { _refreshing = false; }
    }

    private void RefreshProgress()
    {
        if (_progress == null) return;
        _progress.Minimum = _form.Progress.Minimum;
        _progress.Maximum = _form.Progress.Maximum;
        _progress.Value = _form.Progress.Value;
    }

    // A change with no dialogs: run her handler now, on the UI thread.
    private void Do(Action action)
    {
        if (_busy || _refreshing) return;
        try { action(); }
        catch (Exception ex) { _ = ShowError(ex); }
        Refresh();
    }

    // A button: her handler may ask questions, so run it on a worker thread.
    private async void RunAsync(Action action)
    {
        if (_busy || _refreshing) return;
        _busy = true;
        _root.IsHitTestVisible = false;
        Cursor = new Avalonia.Input.Cursor(StandardCursorType.Wait);
        try { await Task.Run(action); }
        catch (Exception ex) { await ShowError(ex); }
        finally
        {
            _busy = false;
            _root.IsHitTestVisible = true;
            Cursor = Avalonia.Input.Cursor.Default;
            Refresh();
        }
        if (_form.IsClosed) { Close(); return; }
    }

    private Task ShowError(Exception ex)
    {
        string text = ex is UnauthorizedAccessException
            ? "macOS blocked access to the Sims 2 folder.\n\nOpen System Settings > Privacy & Security > " +
              "Files and Folders (or App Management / Full Disk Access) and allow ConvertiWall for Mac, then try again.\n\n" + ex.Message
            : ex.Message;
        return Dialogs.Error(this, "ConvertiWall", text);
    }

    private WF.DialogResult AskFromWorker(object owner, WF.MessageRequest req)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return req.DefaultResult;   // never expected: dialogs come from worker-thread code
        return Dispatcher.UIThread.InvokeAsync(() => Dialogs.Ask(this, req)).GetAwaiter().GetResult();
    }

    private string? PickFromWorker(WF.OpenFileDialog dlg)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return null;
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            IStorageFolder? start = string.IsNullOrEmpty(dlg.InitialDirectory)
                ? null : await StorageProvider.TryGetFolderFromPathAsync(dlg.InitialDirectory);
            string[] parts = dlg.Filter.Split('|');
            var type = new FilePickerFileType(parts[0])
            {
                Patterns = parts.Length > 1 ? parts[1].Split(';') : new[] { "*.package" },
            };
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = dlg.Title, AllowMultiple = false, SuggestedStartLocation = start,
                FileTypeFilter = new[] { type },
            });
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }).GetAwaiter().GetResult();
    }

    #endregion

    #region Helpers

    private static (double size, bool bold) ParseFont(string font)
    {
        // "Microsoft Sans Serif, 11pt, style=Bold"; WinForms points at 96 dpi.
        double pt = 8.25;
        foreach (string part in font.Split(','))
        {
            string p = part.Trim();
            if (p.EndsWith("pt") && double.TryParse(p[..^2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                pt = v;
        }
        return (pt * 96.0 / 72.0, font.Contains("Bold"));
    }

    private static IBrush Brush(System.Drawing.Color c, bool enabled = true) =>
        !enabled ? Brushes.Gray
        : c.IsSystemColor ? Brushes.Black : new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

    private static bool EffectivelyEnabled(WF.Control c)
    {
        for (WF.Control? x = c; x != null; x = x.Parent)
            if (!x.Enabled) return false;
        return true;
    }

    // WinForms "&Next" underlines N; "&&" is a literal ampersand.
    private static string StripMnemonic(string s) =>
        string.IsNullOrEmpty(s) ? "" : s.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&");

    #endregion
}
