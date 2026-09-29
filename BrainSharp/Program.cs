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
        public static void Main(string[] args)
        {
            string programPath = "main.bf";
            bool compileMode = true;
            bool includeSafetyChecks = false;
            int allocatedBytes = 8192;
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

                    var memoryArrayField = new FieldDefinition("Memory",
                        FieldAttributes.Public | FieldAttributes.Static, new ArrayType(module.TypeSystem.Byte));
                    var memoryPointerField = new FieldDefinition("MemoryPointer",
                        FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Int32);
                    mainClass.Fields.Add(memoryArrayField);
                    mainClass.Fields.Add(memoryPointerField);

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

                    assembly.Write(Path.GetFileNameWithoutExtension(programPath) + ".exe");
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