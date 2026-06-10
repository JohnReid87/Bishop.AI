using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bishop.Life.SchemaCodegen;

/// <summary>
/// CLI entry-point for the C#→TypeScript schema codegen (card #1077). Reads
/// every <c>.cs</c> file under <c>--schema-dir</c> via Roslyn syntax parsing
/// and writes a single <c>schema.d.ts</c> covering Bishop.Life.Core's wire
/// types: <see cref="Bishop.Life.Core.Schema"/> records/enums plus the
/// <c>Schema.Envelopes</c> host↔viewer message contracts.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var parsed = ParseArgs(args);
        if (parsed is null)
        {
            Console.Error.WriteLine("Usage: Bishop.Life.SchemaCodegen --schema-dir <dir> --output <file>");
            return 2;
        }

        var (schemaDir, outputPath) = parsed.Value;

        if (!Directory.Exists(schemaDir))
        {
            Console.Error.WriteLine($"Schema directory not found: {schemaDir}");
            return 2;
        }

        var sources = Directory.EnumerateFiles(schemaDir, "*.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => (Path: p, Text: File.ReadAllText(p)))
            .ToList();

        var emitter = new TypeScriptEmitter();
        var output = emitter.Emit(sources.Select(s => s.Text));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // Skip writing if the content hasn't changed — keeps file mtime stable
        // for MSBuild's up-to-date check and avoids spurious git-status churn.
        if (File.Exists(outputPath) && File.ReadAllText(outputPath) == output)
        {
            Console.WriteLine($"schema codegen: {outputPath} up-to-date");
            return 0;
        }

        File.WriteAllText(outputPath, output);
        Console.WriteLine($"schema codegen: wrote {outputPath}");
        return 0;
    }

    private static (string SchemaDir, string OutputPath)? ParseArgs(string[] args)
    {
        string? schemaDir = null;
        string? outputPath = null;
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            switch (args[i])
            {
                case "--schema-dir": schemaDir = args[i + 1]; break;
                case "--output": outputPath = args[i + 1]; break;
            }
        }
        if (schemaDir is null || outputPath is null) return null;
        return (schemaDir, outputPath);
    }
}
