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
        
        public static void Main(string[] args)
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

                    switch (embedLevel)
                    {
                        case EmbedLevel.Runtime:
                            Process.Start("ilrepack",
                                $"/out:{fileName}.exe {fileName}.exe .\\BrainSharp.Runtime.dll");
                            break;
                        case EmbedLevel.All:
                            Process.Start("ilrepack",
                                $"/out:{fileName}.exe {fileName}.exe .\\System.Memory.dll .\\BrainSharp.Runtime.dll");
                            break;
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
        }
    }
}