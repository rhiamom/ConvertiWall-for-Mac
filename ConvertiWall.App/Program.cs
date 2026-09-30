/**************************************************************************
 *   ConvertiWall for Mac                                                 *
 *   ConvertiWall © 2009-2010 Mootilda (http://Mootilda.ModTheSims.info)  *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   macOS entry point: starts the Avalonia app.                          *
 *************************************************************************/

using Avalonia;
using System;

namespace ConvertiWall.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
