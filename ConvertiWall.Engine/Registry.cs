/***************************************************************************
 *   macOS port © 2026 GramzeSweatshop                                     *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 ***************************************************************************/
// Mootilda's GetPath() (her PrimaryForm.cs, unchanged) finds the game's user
// folder as
//   Path.Combine(My Documents\EA Games,
//       Microsoft.Win32.Registry.LocalMachine
//           .OpenSubKey("Software\\EA Games\\The Sims 2").GetValue("DisplayName"))
// and then appends "Neighborhoods" or "LotCatalog".
//
// Her code names the registry as Microsoft.Win32.Registry from inside
// namespace LotExpander, so C# finds this LotExpander.Microsoft.Win32.Registry
// first. It answers DisplayName with the Mac user folder from SimsPaths. That
// is an absolute path, and Path.Combine keeps only the last absolute part, so
// her GetPath lands on .../Aspyr/The Sims 2/Neighborhoods. If no Sims 2
// folder is found it answers null, and her own try/catch falls back to My
// Documents, as it did on Windows without the game installed.

namespace LotExpander.Microsoft.Win32
{
    public static class Registry
    {
        public static RegistryKey LocalMachine { get; } = new RegistryKey();
    }

    public sealed class RegistryKey
    {
        public RegistryKey OpenSubKey(string name) =>
            name == "Software\\EA Games\\The Sims 2" ? this : null;

        public object GetValue(string name) =>
            name == "DisplayName" ? SimsPaths.UserFolder : null;
    }
}
