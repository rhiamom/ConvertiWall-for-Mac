/**************************************************************************
 *   ConvertiWall for Mac                                                 *
 *   ConvertiWall © 2009-2010 Mootilda                                    *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Headless harness. Drives Mootilda's own form logic (PrimaryForm.cs,  *
 *   unchanged) through the headless controls, the way a user clicked it. *
 *************************************************************************/

// ConvertiWall smoke test.
//
//   list    <HOOD>                        lots in a neighborhood package
//   walls   <HOOD> <lot#>                 every wall on the lot: WLL id, level,
//                                         end points (lot-file coordinates)
//   convert <HOOD> <lot#> --from ID --to ID [--levels A:B] [--front A:B] [--left A:B]
//                                         her Specification screen + Finish;
//                                         ranges default to hers (whole lot,
//                                         front starting at 10)
//
// <HOOD> is a hood code (N001) for its main package, or CODE/<package file>
// for a subhood. Options: --out DIR (work folder), --in-place (edit the real
// hood; never the default).
//
// Unless --in-place is given, the whole hood folder is first copied to a work
// folder and only the copy is changed.

using System.Drawing;
using LotExpander;
using SimPe.Packages;

namespace ConvertiWall.SmokeTest;

internal static class Program
{
    private const uint DESC = 0x0BF999E7;
    private const uint WLL = 0x8A84D7B0;
    private const uint WGRA = 0x0A284D0B;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: list|walls|convert <HOOD> [lot#] [--from ID --to ID --levels A:B --front A:B --left A:B] [--out DIR] [--in-place]");
            return 2;
        }

        // Her message boxes: print, answer with the default button.
        MessageBox.Handler = (owner, m) =>
        {
            Console.WriteLine($"    message: [{m.Caption}] {m.Text.Replace("\n", " ")} -> {m.DefaultResult}");
            return m.DefaultResult;
        };

        string mode = args[0];
        string hoodArg = args[1];
        bool inPlace = args.Contains("--in-place");
        string? outDir = Opt(args, "--out");

        string? nbRoot = SimsPaths.NeighborhoodsFolder;
        if (nbRoot == null) { Console.Error.WriteLine("Sims 2 Neighborhoods folder not found."); return 1; }

        string code = hoodArg.Split('/')[0];
        string hoodDir = Path.Combine(nbRoot, code);
        if (!Directory.Exists(hoodDir)) { Console.Error.WriteLine($"No hood folder {hoodDir}"); return 1; }

        if (mode == "convert" && !inPlace)
        {
            outDir ??= Path.Combine(Path.GetTempPath(), "convertiwall-smoke", $"{code}-{DateTime.Now:yyyyMMdd-HHmmss}");
            string copy = Path.Combine(outDir, code);
            CopyDir(hoodDir, copy);
            Console.WriteLine($"Working on a copy: {copy}");
            hoodDir = copy;
        }

        string package = hoodArg.Contains('/')
            ? Path.Combine(hoodDir, hoodArg.Split('/', 2)[1])
            : Path.Combine(hoodDir, code + "_Neighborhood.package");

        return mode switch
        {
            "list" => List(package),
            "walls" => Walls(package, LotArg(args)),
            "convert" => Convert(package, LotArg(args), args),
            _ => 2,
        };
    }

    private static uint LotArg(string[] args) =>
        args.Length >= 3 && uint.TryParse(args[2].Replace("Lot", ""), out uint v) ? v
            : throw new ArgumentException("needs a lot number (see: list)");

    private static string? Opt(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static (int, int)? Range(string[] args, string name)
    {
        string? s = Opt(args, name);
        if (s == null) return null;
        string[] p = s.Split(':');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
            System.IO.File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (string d in Directory.GetDirectories(from))
            CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    private static PrimaryForm NewForm()
    {
        var form = new PrimaryForm();
        form.Show();    // Load + Shown, as WinForms did
        return form;
    }

    private static string LotPackagePath(string hoodPackage, uint instance)
    {
        string name = Path.GetFileName(hoodPackage);
        string prefix = name.Substring(0, name.IndexOf('_'));
        return Path.Combine(Path.GetDirectoryName(hoodPackage)!, "Lots", $"{prefix}_Lot{instance}.package");
    }

    private static int List(string package)
    {
        var hood = SimPe.Packages.File.LoadFromFile(package);
        foreach (var pfd in hood.FindFiles(DESC).OrderBy(p => p.Instance))
        {
            var d = new R_DESC(hood, pfd, false);
            string lot = LotPackagePath(package, pfd.Instance);
            int walls = System.IO.File.Exists(lot) ? ReadWalls(lot).Count : 0;
            Console.WriteLine($"  Lot{pfd.Instance,-4} {d.Width}x{d.Height} U11 {d.U11} type {d.LotType} walls {walls,4} | {d.LotName}");
        }
        return 0;
    }

    private sealed record WallInfo(uint Ref, uint Id, short Pattern1, short Pattern2, int Level,
                                   float X1, float Y1, float X2, float Y2);

    // WLL entries (ref, id, two patterns) joined to the WGRA wall graph for
    // level and end points. Instance 5 first, then 0x18, as her FinalScreen does.
    private static List<WallInfo> ReadWalls(string lotPath)
    {
        var pkg = SimPe.Packages.File.LoadFromFile(lotPath);
        var ends = new Dictionary<uint, (int, float, float, float, float)>();
        foreach (uint inst in new uint[] { 5, 0x18 })
        {
            var pfd = pkg.FindFile(WGRA, 0, 0xFFFFFFFF, inst);
            if (pfd == null) continue;
            byte[] g = pkg.Read(pfd).UncompressedData;
            if (g.Length <= 119 + 16) continue;
            var br = new BinaryReader(new MemoryStream(g));
            br.BaseStream.Position = 115;
            int nv = br.ReadInt32();
            var v = new Dictionary<uint, (float x, float y, int l)>();
            for (int i = 0; i < nv; i++) { uint k = br.ReadUInt32(); v[k] = (br.ReadSingle(), br.ReadSingle(), br.ReadInt32()); }
            int n2 = br.ReadInt32(); br.ReadBytes(n2 * 4);
            int nw = br.ReadInt32();
            for (int i = 0; i < nw; i++)
            {
                uint r = br.ReadUInt32(); uint a = br.ReadUInt32(); br.ReadUInt32(); uint b = br.ReadUInt32(); br.ReadUInt32();
                if (!ends.ContainsKey(r)) ends[r] = (v[a].l, v[a].x, v[a].y, v[b].x, v[b].y);
            }
        }

        var walls = new List<WallInfo>();
        foreach (var pfd in pkg.FindFiles(WLL))
        {
            byte[] w = pkg.Read(pfd).UncompressedData;
            var br = new BinaryReader(new MemoryStream(w));
            br.BaseStream.Position = 83;
            int n = br.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                uint r = br.ReadUInt32(), id = br.ReadUInt32();
                short p1 = br.ReadInt16(), p2 = br.ReadInt16();
                var e = ends.TryGetValue(r, out var x) ? x : (-99, 0f, 0f, 0f, 0f);
                walls.Add(new WallInfo(r, id, p1, p2, e.Item1, e.Item2, e.Item3, e.Item4, e.Item5));
            }
        }
        return walls;
    }

    private static int Walls(string package, uint lot)
    {
        var walls = ReadWalls(LotPackagePath(package, lot));
        Console.WriteLine($"{walls.Count} walls");
        foreach (var g in walls.GroupBy(w => (w.Level, w.Id)).OrderBy(g => g.Key))
            Console.WriteLine($"  level {g.Key.Level,2}  id {g.Key.Id,-10} x{g.Count()}");
        foreach (var w in walls.OrderBy(w => w.Level).ThenBy(w => w.Id).ThenBy(w => w.X1).ThenBy(w => w.Y1))
            Console.WriteLine($"    ref {w.Ref,5} id {w.Id,-10} L{w.Level,2}  ({w.X1,5},{w.Y1,5})-({w.X2,5},{w.Y2,5})  patterns {w.Pattern1},{w.Pattern2}");
        return 0;
    }

    // One pass through her screens: Lot list -> Next -> Specification -> Finish.
    private static int Convert(string package, uint lot, string[] args)
    {
        string lotPath = LotPackagePath(package, lot);
        var before = ReadWalls(lotPath);

        var hood = SimPe.Packages.File.LoadFromFile(package);
        string lotName = new R_DESC(hood, hood.FindFile(DESC, 0, 0xFFFFFFFF, lot), false).LotName;

        var form = NewForm();
        form.OpenNeighborhood(package);
        form.Liste.SelectedItem = lotName;
        if (!form.NextButton.PerformClick() || form.CurrentScreen != PrimaryForm.ScreenSpecification)
        {
            Console.WriteLine($"  could not open the lot's Specification screen: {form.Title.Text}");
            return 1;
        }

        Console.WriteLine($"  {form.Title.Text} walls on lot: {string.Join(" | ", form.FromWall.Items.Cast<object>())}");
        Console.WriteLine($"  ranges: levels {form.FromLevel.Value}..{form.ToLevel.Value}  front {form.FromFront.Value}..{form.ToFront.Value}" +
                          $"  left {form.FromLeft.Value}..{form.ToLeft.Value}  (U11 {form.LotRotation})");

        string? from = Opt(args, "--from"), to = Opt(args, "--to");
        if (from != null)
        {
            object? item = form.FromWall.Items.Cast<object>().FirstOrDefault(i => i.ToString()!.Split(':')[0] == from);
            if (item == null) { Console.WriteLine($"  wall id {from} is not on this lot"); return 1; }
            form.FromWall.SelectedItem = item;
        }
        if (to != null)
            form.ToWall.Text = form.ToWall.Items.Cast<object>().FirstOrDefault(i => i.ToString()!.Split(':')[0] == to)?.ToString() ?? to;

        // Her boxes limit each other (From.Maximum = To.Value...), so set the
        // "to" ends first when widening and the "from" ends first otherwise.
        void SetRange(NumericUpDown lo, NumericUpDown hi, (int, int)? r)
        {
            if (r == null) return;
            if (r.Value.Item2 >= hi.Value) { hi.Value = r.Value.Item2; lo.Value = r.Value.Item1; }
            else { lo.Value = r.Value.Item1; hi.Value = r.Value.Item2; }
        }
        SetRange(form.FromLevel, form.ToLevel, Range(args, "--levels"));
        SetRange(form.FromFront, form.ToFront, Range(args, "--front"));
        SetRange(form.FromLeft, form.ToLeft, Range(args, "--left"));

        Console.WriteLine($"  convert '{form.FromWall.Text}' -> '{form.ToWall.Text}'  levels {form.FromLevel.Value}..{form.ToLevel.Value}" +
                          $"  front {form.FromFront.Value}..{form.ToFront.Value}  left {form.FromLeft.Value}..{form.ToLeft.Value}");
        form.NextButton.PerformClick();

        bool aborted = form.Title.ForeColor == Color.Red;
        bool final = form.CurrentScreen == PrimaryForm.ScreenFinal;
        Console.WriteLine($"  {(aborted ? "ABORTED" : final ? "DONE" : "STAYED")}: {form.Title.Text} | {form.Explanation.Text.Trim()}");
        if (!final || aborted) return 1;

        // Check on disk: only the Wall ID of matching walls changed.
        var after = ReadWalls(lotPath);
        var changed = before.Zip(after).Where(p => p.First != p.Second).ToList();
        bool onlyIds = before.Count == after.Count && changed.All(p => p.First with { Id = p.Second.Id } == p.Second);
        bool bkp = System.IO.File.Exists(Path.ChangeExtension(lotPath, ".bkp"));
        Console.WriteLine($"  verify: {changed.Count} walls changed on disk, only wall IDs changed: {(onlyIds ? "yes" : "NO")}, backup: {(bkp ? "yes" : "NO")}");
        foreach (var (b, a) in changed)
            Console.WriteLine($"    ref {b.Ref,5} L{b.Level,2} ({b.X1},{b.Y1})-({b.X2},{b.Y2})  id {b.Id} -> {a.Id}");
        return onlyIds && bkp ? 0 : 1;
    }
}
