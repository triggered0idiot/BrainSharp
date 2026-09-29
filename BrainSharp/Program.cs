using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BrainSharp
{
    internal class Program
    {
        enum EmbedLevel
        {
            None,
            Runtime,
            All
        }
        
        // Source - https://stackoverflow.com/a/63021455
        // Posted by John Gietzen, modified by community. See post 'Timeline' for change history
        // Retrieved 2026-09-29, License - CC BY-SA 4.0
        public static string Where(string file)
        {
            var paths = Environment.GetEnvironmentVariable("PATH").Split(';');
            var extensions = Environment.GetEnvironmentVariable("PATHEXT").Split(';');
            return (from p in new[] { Environment.CurrentDirectory }.Concat(paths)
                from e in new[] { string.Empty }.Concat(extensions)
                let path = Path.Combine(p.Trim(), file + e.ToLower())
                where File.Exists(path)
                select path).FirstOrDefault();
        }
        
        public static int Main(string[] args)
        {
            string programPath = "main.bf";
            bool compileMode = true;
            bool includeSafetyChecks = false;
            int allocatedBytes = 8192;
            
            // compiler specific
            EmbedLevel embedLevel = EmbedLevel.None;
            
            // interp specific
            bool memoryTrack = false;
            
            // very crude argument reader
            if (args.Length != 0)
            {
                programPath = args[0];
                for (var index = 0; index < args.Length; index++)
                {
                    var arg = args[index];
                    if (string.Equals(arg, "--unsafe", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-u", StringComparison.CurrentCultureIgnoreCase))
                        includeSafetyChecks = false;
                    if (string.Equals(arg, "--safe", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-s", StringComparison.CurrentCultureIgnoreCase))
                        includeSafetyChecks = true;
                    if (string.Equals(arg, "--allocate", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-a", StringComparison.CurrentCultureIgnoreCase))
                    {
                        int newIndex = index+1;
                        if (newIndex < args.Length)
                            allocatedBytes = int.Parse(args[newIndex]);
                    }
                    
                    // compiler specific
                    if (string.Equals(arg, "--compile", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-c", StringComparison.CurrentCultureIgnoreCase))
                        compileMode = true;
                    if (string.Equals(arg, "--embedded", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-e", StringComparison.CurrentCultureIgnoreCase))
                    {
                        int newIndex = index+1;
                        if (newIndex < args.Length)
                        {
                            string level = args[newIndex].ToLower();
                            if (level is "none")
                                embedLevel = EmbedLevel.None;
                            else if (level is "runtime" or "brainsharp")
                                embedLevel = EmbedLevel.Runtime;
                            else if (level is "all")
                                embedLevel = EmbedLevel.All;
                            else
                                embedLevel = EmbedLevel.All;
                        }
                    }
                    
                    // interp specific
                    if (string.Equals(arg, "--execute", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-e", StringComparison.CurrentCultureIgnoreCase))
                        compileMode = false;
                    if (string.Equals(arg, "--track", StringComparison.CurrentCultureIgnoreCase) ||
                        string.Equals(arg, "-t", StringComparison.CurrentCultureIgnoreCase))
                        memoryTrack = true;
                }
            }

            string ilrepackLoc = "";
            if (embedLevel != EmbedLevel.None)
            {
                ilrepackLoc = Where("ilrepack");
                if(string.IsNullOrWhiteSpace(ilrepackLoc))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ilrepack not installed, run 'dotnet tool install -g dotnet-ilrepack' to install!");
                    Console.ResetColor();
                    return 1;
                }
            }
            
            string code = File.ReadAllText(programPath);
            
            if(compileMode)
            {
                var assemblyName = new AssemblyNameDefinition("BrainSharpAssembly", new Version(1, 0, 0, 0));

                using (var assembly = AssemblyDefinition.CreateAssembly(assemblyName, "BrainSharp", ModuleKind.Console))
                {
                    var module = assembly.MainModule;

                    var classAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AnsiClass |
                                          TypeAttributes.BeforeFieldInit | TypeAttributes.Abstract |
                                          TypeAttributes.Sealed;
                    var mainClass = new TypeDefinition("BrainSharp.Compiled", "Program", classAttributes,
                        module.TypeSystem.Object);
                    module.Types.Add(mainClass);

                    var methodAttributes =
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static;
                    var mainMethod = new MethodDefinition("Main", methodAttributes, module.TypeSystem.Void);
                    mainMethod.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None,
                        new ArrayType(module.TypeSystem.String)));
                    mainClass.Methods.Add(mainMethod);
                    module.EntryPoint = mainMethod;

                    var tempByteVar = new VariableDefinition(module.TypeSystem.Byte);
                    mainMethod.Body.Variables.Add(tempByteVar);
                    var il = mainMethod.Body.GetILProcessor();

                    var brainfuckCompiler = new Compiler(il, includeSafetyChecks, allocatedBytes);
                    brainfuckCompiler.Compile(code);

                    il.Emit(OpCodes.Nop);
                    il.Emit(OpCodes.Ret);

                    string fileName = Path.GetFileNameWithoutExtension(programPath);
                    assembly.Write(fileName + ".exe");

                    if(embedLevel != EmbedLevel.None)
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = ilrepackLoc,
                            Arguments = "",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        switch (embedLevel)
                        {
                            case EmbedLevel.Runtime:
                                processInfo.Arguments += $"/out:{fileName}.exe {fileName}.exe .\\BrainSharp.Runtime.dll";
                                break;
                            case EmbedLevel.All:
                                processInfo.Arguments += $"/out:{fileName}.exe {fileName}.exe .\\System.Memory.dll .\\BrainSharp.Runtime.dll";
                                break;
                        }
                        Process.Start(processInfo);
                    }
                }

                Console.WriteLine($"Program '{Path.GetFileNameWithoutExtension(programPath) + ".exe"}' compiled successfully!");
            }
            else
            {
                Interpreter.IncludeSafetyChecks = includeSafetyChecks;
                Interpreter.AllocatedBytes = allocatedBytes;
                Interpreter.MemoryTrack = memoryTrack;
                Interpreter.Run(code);
            }

            return 0;
        }
    }
}