using HlslDecompiler.D3D;
using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HlslDecompiler;

class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine($"HLSLDecompiler [{Assembly.GetExecutingAssembly().GetName().Version} (Built {GetLinkerTimestampUtc(Assembly.GetExecutingAssembly())} UTC)]");
        Console.WriteLine();
        Console.WriteLine("Please report any bugs and/or feature requests:");
        Console.WriteLine("https://github.com/MtnDewIt/HlslDecompiler/issues");

        Console.WriteLine("\nEnter the path to a compiled DX shader file (.fxc/.fxo):");
        Console.Write("> ");

        var input = Console.ReadLine();

        var options = CommandLineOptions.Parse(input);

        if (options.InputFilename == null)
        {
            Console.WriteLine("Expected input filename");
            return;
        }

        string baseFilename = Path.GetFileNameWithoutExtension(options.InputFilename);

        if (options.Compile)
        {
            var macros = new List<D3D.D3D.SHADER_MACRO>();
            var bytecode = D3DCompiler.GenerateByteCode(options.InputFilename, macros, options.EntryPoint, options.Version);

            File.WriteAllBytes($"{baseFilename}.fxc", bytecode);
        }
        else 
        {
            using var inputStream = File.Open(options.InputFilename, FileMode.Open, FileAccess.Read);
            var format = FormatDetector.Detect(inputStream);
            switch (format)
            {
                case ShaderFileFormat.ShaderModel:
                    ReadShaderModel(baseFilename, inputStream, options);
                    break;
                case ShaderFileFormat.Dxbc:
                    ReadDxbc(baseFilename, inputStream, options);
                    break;
                case ShaderFileFormat.Rgxa:
                    ReadRgxa(baseFilename, inputStream, options.DoAstAnalysis);
                    break;
                case ShaderFileFormat.Unknown:
                    Console.WriteLine("Unknown file format!");
                    break;
            }

            if (!options.PrintToConsole)
            {
                Console.WriteLine("Finished.");
            }
        }
    }

    private static DateTime GetLinkerTimestampUtc(Assembly assembly)
    {
        var location = assembly.Location;
        return GetLinkerTimestampUtc(location);
    }

    private static DateTime GetLinkerTimestampUtc(string filePath)
    {
        const int peHeaderOffset = 60;
        const int linkerTimestampOffset = 8;
        var bytes = new byte[2048];

        using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            file.ReadExactly(bytes, 0, bytes.Length);
        }

        var headerPos = BitConverter.ToInt32(bytes, peHeaderOffset);
        var secondsSince1970 = BitConverter.ToInt32(bytes, headerPos + linkerTimestampOffset);
        var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return dt.AddSeconds(secondsSince1970);
    }

    private static void ReadShaderModel(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        using (var input = new ShaderReader(inputStream, true))
        {
            ShaderModel shader = input.ReadShader();

            if (!options.PrintToConsole)
            {
                var writer = new AsmWriter(shader);
                string asmFilename = $"{baseFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                writer.Write(asmFilename);
            }

            var hlslWriter = CreateHlslWriter(shader, options.DoAstAnalysis);
            if (options.PrintToConsole)
            {
                hlslWriter.Write(Console.Out);
            }
            else
            {
                string hlslFilename = $"{baseFilename}.fx";
                Console.WriteLine("Writing {0}", hlslFilename);
                hlslWriter.Write(hlslFilename);
            }
        }
    }

    private static void ReadDxbc(string baseFilename, FileStream inputStream, CommandLineOptions options)
    {
        using (var input = new DxbcReader(inputStream, true))
        {
            ShaderModel shader = input.ReadShader();

            if (!options.PrintToConsole)
            {
                var writer = new AsmWriter(shader);
                string asmFilename = $"{baseFilename}.asm";
                Console.WriteLine("Writing {0}", asmFilename);
                writer.Write(asmFilename);
            }

            var hlslWriter = CreateHlslWriter(shader, options.DoAstAnalysis);
            if (options.PrintToConsole)
            {
                hlslWriter.Write(Console.Out);
            }
            else
            {
                string hlslFilename = $"{baseFilename}.fx";
                Console.WriteLine("Writing {0}", hlslFilename);
                hlslWriter.Write(hlslFilename);
            }
        }
    }

    private static void ReadRgxa(string baseFilename, FileStream inputStream, bool doAstAnalysis)
    {
        using var input = new RgxaReader(inputStream, true);
        int ivs = 0, ips = 0;
        while (true)
        {
            ShaderModel shader = input.ReadShader();
            if (shader is null)
            {
                break;
            }

            string outFilename;
            if (shader.Type == ShaderType.Vertex)
            {
                outFilename = $"{baseFilename}_vs{ivs}";
                ivs++;
            }
            else
            {
                outFilename = $"{baseFilename}_ps{ips}";
                ips++;
            }
            Console.WriteLine(outFilename);

            //shader.ToFile("outFilename.fxc");

            var writer = new AsmWriter(shader);
            writer.Write(outFilename + ".asm");

            var hlslWriter = CreateHlslWriter(shader, doAstAnalysis);
            hlslWriter.Write(outFilename + ".fx");
        }
    }

    private static HlslWriter CreateHlslWriter(ShaderModel shader, bool doAstAnalysis)
    {
        if (doAstAnalysis)
        {
            return new HlslAstWriter(shader);
        }
        return new HlslSimpleWriter(shader);
    }
}
