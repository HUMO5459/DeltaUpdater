using System.Security.Cryptography;
using System.Text.Json;

namespace ManifestGenerator;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Manifest Generator ===");
        Console.WriteLine();

        // --- Papka yo'lini olish ---
        string folderPath;
        if (args.Length >= 1 && Directory.Exists(args[0]))
        {
            folderPath = Path.GetFullPath(args[0]);
        }
        else
        {
            Console.Write("Papka yo'lini kiriting (bo'sh = joriy papka): ");
            string input = Console.ReadLine()?.Trim().Trim('"') ?? "";
            folderPath = string.IsNullOrWhiteSpace(input)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(input);
        }

        if (!Directory.Exists(folderPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Xatolik: Papka topilmadi: {folderPath}");
            Console.ResetColor();
            Exit();
            return;
        }

        // --- Versiyani olish ---
        string version;
        if (args.Length >= 2)
        {
            version = args[1];
        }
        else
        {
            string existing = ReadExistingVersion(folderPath);
            Console.Write($"Versiyani kiriting [{existing}]: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            version = string.IsNullOrWhiteSpace(input) ? existing : input;
        }

        // --- manifest.json chiqish yo'li ---
        string outputPath;
        if (args.Length >= 3)
        {
            outputPath = args[2];
        }
        else
        {
            string defaultOutput = Path.Combine(folderPath, "manifest.json");
            Console.Write($"manifest.json yo'li [{defaultOutput}]: ");
            string input = Console.ReadLine()?.Trim().Trim('"') ?? "";
            outputPath = string.IsNullOrWhiteSpace(input) ? defaultOutput : input;
        }

        Console.WriteLine();
        Console.WriteLine($"Papka  : {folderPath}");
        Console.WriteLine($"Versiya: {version}");
        Console.WriteLine($"Chiqish: {outputPath}");
        Console.WriteLine();

        // --- Fayllarni skanerlash ---
        Console.WriteLine("Fayllar skanerlanmoqda...");
        var files = new List<FileEntry>();
        int skipped = 0;

        ScanDirectory(folderPath, folderPath, files, ref skipped);

        Console.WriteLine($"\r  Topildi : {files.Count} ta fayl");
        Console.WriteLine($"  O'tkazib : {skipped} ta fayl");
        Console.WriteLine();

        // --- Manifest yaratish ---
        var manifest = new Manifest
        {
            Version = version,
            GeneratedAt = DateTime.UtcNow,
            Files = files
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(manifest, options);

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("manifest.json muvaffaqiyatli yaratildi!");
        Console.ResetColor();
        Console.WriteLine($"Yo'l: {outputPath}");

        Exit();
    }

    static void ScanDirectory(string dirPath, string basePath, List<FileEntry> files, ref int skipped)
    {
        try
        {
            foreach (string filePath in Directory.GetFiles(dirPath))
            {
                string relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');

                if (ShouldSkip(relativePath))
                {
                    skipped++;
                    continue;
                }

                Console.Write($"\r  Tekshirilmoqda: {files.Count + skipped + 1} ta...");

                files.Add(new FileEntry
                {
                    RelativePath = relativePath,
                    Size = new FileInfo(filePath).Length,
                    Checksum = ComputeChecksum(filePath),
                    LastModified = File.GetLastWriteTime(filePath)
                });
            }

            foreach (string subDir in Directory.GetDirectories(dirPath))
                ScanDirectory(subDir, basePath, files, ref skipped);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n  Ogohlantirish: {ex.Message}");
        }
    }

    static bool ShouldSkip(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath).ToLower();

        string[] skipExact     = { "manifest.json", "temp.txt" };
        string[] skipContains  = { "updater.exe", "updater.pdb" };
        string[] skipExtensions = { ".tmp", ".log", ".backup" };

        if (skipExact.Contains(fileName)) return true;
        if (skipContains.Any(s => fileName.Contains(s))) return true;
        if (skipExtensions.Any(ext => fileName.EndsWith(ext))) return true;

        return false;
    }

    static string ComputeChecksum(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    static string ReadExistingVersion(string folderPath)
    {
        string versionFile = Path.Combine(folderPath, "version.txt");
        if (File.Exists(versionFile))
            return File.ReadAllText(versionFile).Trim();

        string manifestFile = Path.Combine(folderPath, "manifest.json");
        if (File.Exists(manifestFile))
        {
            try
            {
                string json = File.ReadAllText(manifestFile);
                var existing = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (!string.IsNullOrWhiteSpace(existing?.Version))
                    return existing.Version;
            }
            catch { }
        }

        return "1.0.0";
    }

    static void Exit()
    {
        Console.WriteLine();
        Console.WriteLine("Chiqish uchun Enter bosing...");
        Console.ReadLine();
    }
}

class Manifest
{
    public string Version { get; set; } = "1.0.0";
    public DateTime GeneratedAt { get; set; }
    public List<FileEntry> Files { get; set; } = new();
}

class FileEntry
{
    public string RelativePath { get; set; } = "";
    public long Size { get; set; }
    public string Checksum { get; set; } = "";
    public DateTime LastModified { get; set; }
}
