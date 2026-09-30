/**************************************************************************
 *   ConvertiWall for Mac                                                 *
 *   ConvertiWall © 2009-2010 Mootilda (http://Mootilda.ModTheSims.info)  *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Avalonia application entry: creates the main window.                 *
 *************************************************************************/

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ConvertiWall.App.Views;

namespace ConvertiWall.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
