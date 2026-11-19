using System;
using System.IO;
using System.Security.Cryptography;
using Shooter.Generation.Icons;

// ADR-123 installer art: InstallerArt <outputDirectory>. Writes setup.ico, wizard.bmp and wizard-small.bmp
// from the analytic brand recipe and prints "name sha256" lines for the build report.
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: InstallerArt <outputDirectory>");
    return 2;
}
string dir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(dir);
foreach (var (name, bytes) in new (string, byte[])[]
{
    ("setup.ico", InstallerArt.Ico()),
    ("wizard.bmp", InstallerArt.WizardImage()),
    ("wizard-small.bmp", InstallerArt.WizardSmallImage())
})
{
    File.WriteAllBytes(Path.Combine(dir, name), bytes);
    Console.WriteLine(name + " " + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}
return 0;
