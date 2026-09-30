/**************************************************************************
 *   ConvertiWall for Mac                                                 *
 *   ConvertiWall © 2009-2010 Mootilda (http://Mootilda.ModTheSims.info)  *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Modal dialogs for her MessageBox calls: same caption, text, buttons  *
 *   and default button as WinForms.                                      *
 *************************************************************************/

using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DialogResult = LotExpander.DialogResult;
using MessageBoxButtons = LotExpander.MessageBoxButtons;
using MessageBoxIcon = LotExpander.MessageBoxIcon;
using MessageRequest = LotExpander.MessageRequest;

namespace ConvertiWall.App.Views;

internal static class Dialogs
{
    public static Task<DialogResult> Ask(Window owner, MessageRequest req)
    {
        var tcs = new TaskCompletionSource<DialogResult>();

        (string label, DialogResult result)[] buttons = req.Buttons switch
        {
            MessageBoxButtons.OKCancel => new[] { ("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel) },
            MessageBoxButtons.YesNo => new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No) },
            MessageBoxButtons.YesNoCancel => new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel) },
            MessageBoxButtons.RetryCancel => new[] { ("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel) },
            MessageBoxButtons.AbortRetryIgnore => new[] { ("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore) },
            _ => new[] { ("OK", DialogResult.OK) },
        };
        // Closing the window without a button: the safe answer.
        DialogResult closed = req.Buttons == MessageBoxButtons.OK ? DialogResult.OK
                            : req.Buttons == MessageBoxButtons.YesNo ? DialogResult.No
                            : DialogResult.Cancel;

        string glyph = req.Icon switch
        {
            MessageBoxIcon.Error => "⛔",
            MessageBoxIcon.Warning => "⚠️",
            MessageBoxIcon.Question => "❓",
            MessageBoxIcon.Information => "ℹ️",
            _ => "",
        };

        var win = new Window
        {
            Title = req.Caption,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        DialogResult def = req.DefaultResult;
        foreach (var (label, result) in buttons)
        {
            var b = new Button { Content = label, MinWidth = 90, IsDefault = result == def, HorizontalContentAlignment = HorizontalAlignment.Center };
            b.Click += (_, _) => { tcs.TrySetResult(result); win.Close(); };
            row.Children.Add(b);
        }

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        if (glyph.Length > 0)
            body.Children.Add(new TextBlock { Text = glyph, FontSize = 28, VerticalAlignment = VerticalAlignment.Top });
        body.Children.Add(new TextBlock { Text = req.Text, TextWrapping = TextWrapping.Wrap, MaxWidth = 540 });

        win.Content = new StackPanel { Margin = new Thickness(20), Spacing = 18, Children = { body, row } };
        win.Closed += (_, _) => tcs.TrySetResult(closed);
        _ = win.ShowDialog(owner);
        return tcs.Task;
    }

    public static Task Error(Window owner, string caption, string text) =>
        Ask(owner, new MessageRequest { Caption = caption, Text = text, Buttons = MessageBoxButtons.OK, Icon = MessageBoxIcon.Error });
}
