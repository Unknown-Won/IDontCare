// Offline check: loads IDontCare.dll next to the game assemblies, applies each patch class and lists what it hooked.
// Usage: PatchCheck.exe [path to IDontCare.dll]
using System;
using System.IO;
using System.Linq;
using System.Reflection;

using HarmonyLib;

internal static class Program
{
    private const string Game = @"D:\Games\Steam\steamapps\common\Mount & Blade II Bannerlord";
    private const string Mods = @"D:\Games\Modding\Mount & Blade II Bannerlord\mods";

    private static readonly string[] SearchDirs =
    {
        Path.Combine(Game, @"bin\Win64_Shipping_Client"),
        Path.Combine(Game, @"Modules\Native\bin\Win64_Shipping_Client"),
        Path.Combine(Game, @"Modules\SandBoxCore\bin\Win64_Shipping_Client"),
        Path.Combine(Game, @"Modules\SandBox\bin\Win64_Shipping_Client"),
        Path.Combine(Mods, @"Harmony\Bannerlord.Harmony\bin\Win64_Shipping_Client"),
        Path.Combine(Mods, @"Mod Configuration Menu\Bannerlord.MBOptionScreen\bin\Win64_Shipping_Client"),
    };

    private static readonly string[] PatchClasses =
    {
        "IDontCare.Patches.LogEntriesPatch",
        "IDontCare.Patches.CustomLogsPatches",
        "IDontCare.Patches.NotificationPatches",
    };

    private static int Main(string[] args)
    {
        string dll = args.Length > 0 ? args[0] : Path.GetFullPath(@"..\..\..\_shared\buildshim\Modules\IDontCare\bin\Win64_Shipping_Client\IDontCare.dll");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var dir in SearchDirs.Append(Path.GetDirectoryName(dll)!))
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        };

        var asm = Assembly.LoadFrom(dll);
        var harmony = new Harmony("idontcare.patchcheck");
        int failures = 0;
        foreach (string className in PatchClasses)
        {
            try
            {
                var patchClass = asm.GetType(className, true);
                harmony.CreateClassProcessor(patchClass).Patch();
                var hooked = harmony.GetPatchedMethods()
                    .Where(m => Harmony.GetPatchInfo(m).Prefixes.Any(p => p.owner == harmony.Id && p.PatchMethod.DeclaringType == patchClass))
                    .ToList();
                foreach (var method in hooked)
                {
                    Console.WriteLine($"{className}: {method.DeclaringType?.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                }

                if (hooked.Count == 0)
                {
                    failures++;
                    Console.WriteLine($"{className}: no prefix applied");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"{className}: FAILED {ex.GetBaseException().Message}");
            }
        }

        Console.WriteLine(failures == 0 ? "All patches applied." : $"{failures} failure(s).");
        return failures == 0 ? 0 : 1;
    }
}
