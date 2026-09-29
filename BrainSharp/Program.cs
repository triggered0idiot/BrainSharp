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
        public class LoopData
        {
            public int StartBracket = 0;
            public int EndBracket = 0;
        }
        
        public static void Interp(string code)
        {
            Stack<LoopData> loops = new Stack<LoopData>();
            byte[] memory = new byte[8192];
            int memPtr = 0;
            int ptr = -1;
            while (++ptr < code.Length)
            {
                char c = code[ptr];
                int tempPtr;
                switch (c)
                {
                    case '+':
                        memory[memPtr]++;
                        break;
                    case '-':
                        memory[memPtr]--;
                        break;
                    case '>':
                        memPtr++;
                        if (memPtr >= memory.Length)
                            throw new OutOfMemoryException();
                        break;
                    case '<':
                        memPtr--;
                        if (memPtr < 0)
                            throw new IndexOutOfRangeException();
                        break;
                    case '[':
                        int b = 1;
                        tempPtr = ptr;
                        while (b > 0)
                        {
                            tempPtr++;
                            if (tempPtr >= code.Length)
                                throw new InvalidOperationException("Cannot start a loop '[' without an ending ']'");

                            if (code[tempPtr] == ']')
                                b--;
                            else if (code[tempPtr] == '[')
                                b++;
                        }

                        loops.Push(new LoopData { StartBracket = ptr, EndBracket = tempPtr });
                        if (memory[memPtr] == 0)
                        {
                            ptr = tempPtr;
                            loops.Pop();
                        }
                        break;
                    case ']':
                        if (memory[memPtr] != 0)
                            ptr = loops.Peek().StartBracket;
                        else
                            loops.Pop();
                        break;
                    case '.':
                        Console.Write((char)memory[memPtr]);
                        break;
                    case ',':
                        memory[memPtr] = (byte)Console.Read();
                        break;
                }
            }

            Console.Write('\n');
            Console.WriteLine("End of Program");
        }

        public static bool IncludeSafetyChecks = true;
        
        public static void Main(string[] args)
        {
            string code = File.ReadAllText("ghost.bf");
            //Interp(code);
            
            var assemblyName = new AssemblyNameDefinition("BrainSharpAssembly", new Version(1, 0, 0, 0));

            using (var assembly = AssemblyDefinition.CreateAssembly(assemblyName, "BrainSharp", ModuleKind.Console))
            {
                var module = assembly.MainModule;
                var byteType = module.ImportReference(typeof(byte));
                var outOfMemoryConstruct = module.ImportReference(typeof(OutOfMemoryException).GetConstructor(Type.EmptyTypes));
                var outOfRangeConstruct = module.ImportReference(typeof(ArgumentOutOfRangeException).GetConstructor(Type.EmptyTypes));
                var consoleWriteMethod = module.ImportReference(typeof(Console).GetMethod("Write", new []{typeof(char)}));
                var consoleReadMethod = module.ImportReference(typeof(Console).GetMethod("Read", Type.EmptyTypes));

                var classAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AnsiClass |
                                      TypeAttributes.BeforeFieldInit | TypeAttributes.Abstract | TypeAttributes.Sealed;
                var mainClass = new TypeDefinition("BrainSharp.Compiled", "Program", classAttributes, module.TypeSystem.Object);
                module.Types.Add(mainClass);

                var memoryArrayField = new FieldDefinition("Memory",
                    FieldAttributes.Public | FieldAttributes.Static, new ArrayType(module.TypeSystem.Byte));
                var memoryPointerField = new FieldDefinition("MemoryPointer",
                    FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Int32);
                mainClass.Fields.Add(memoryArrayField);
                mainClass.Fields.Add(memoryPointerField);

                var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static;
                var mainMethod = new MethodDefinition("Main", methodAttributes, module.TypeSystem.Void);
                mainMethod.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, new ArrayType(module.TypeSystem.String)));
                mainClass.Methods.Add(mainMethod);
                module.EntryPoint = mainMethod;

                var tempByteVar = new VariableDefinition(module.TypeSystem.Byte);
                mainMethod.Body.Variables.Add(tempByteVar);
                var il = mainMethod.Body.GetILProcessor();
                
                il.Emit(OpCodes.Ldc_I4, 8192);
                il.Emit(OpCodes.Newarr, byteType);
                il.Emit(OpCodes.Stsfld, memoryArrayField);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stsfld, memoryPointerField);

                void ParseCode(string codeToParse)
                {
                    int ptr = -1;
                    while (++ptr < codeToParse.Length)
                    {
                        char c = codeToParse[ptr];
                        int tempPtr;
                        Instruction nopEnd;
                        switch (c)
                        {
                            case '+':
                                // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] + 1);
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldelem_U1);
                                il.Emit(OpCodes.Ldc_I4_1);
                                il.Emit(OpCodes.Add);

                                il.Emit(OpCodes.Stelem_I1);
                                break;
                            case '-':
                                // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] - 1);
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldelem_U1);
                                il.Emit(OpCodes.Ldc_I4_1);
                                il.Emit(OpCodes.Sub);

                                il.Emit(OpCodes.Stelem_I1);
                                break;
                            case '>':
                                // memoryPointer++
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldc_I4_1);
                                il.Emit(OpCodes.Add);
                                il.Emit(OpCodes.Stsfld, memoryPointerField);
                                
                                if(IncludeSafetyChecks)
                                {
                                    // if (MemoryPointer < Memory.Length)
                                    il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                    il.Emit(OpCodes.Ldlen);
                                    il.Emit(OpCodes.Conv_I4);
                                    il.Emit(OpCodes.Ldsfld, memoryPointerField);

                                    nopEnd = Instruction.Create(OpCodes.Nop);
                                    il.Emit(OpCodes.Bge, nopEnd);
                                    il.Emit(OpCodes.Newobj, outOfMemoryConstruct);
                                    il.Emit(OpCodes.Throw);
                                    il.Append(nopEnd);
                                }
                                break;
                            case '<':
                                // memoryPointer--
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldc_I4_1);
                                il.Emit(OpCodes.Sub);
                                il.Emit(OpCodes.Stsfld, memoryPointerField);
                                
                                if(IncludeSafetyChecks)
                                {
                                    // if (0 > MemoryPointer)
                                    il.Emit(OpCodes.Ldc_I4_0);
                                    il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                    il.Emit(OpCodes.Cgt);

                                    nopEnd = Instruction.Create(OpCodes.Nop);
                                    il.Emit(OpCodes.Brfalse, nopEnd);
                                    il.Emit(OpCodes.Newobj, outOfRangeConstruct);
                                    il.Emit(OpCodes.Throw);
                                    il.Append(nopEnd);
                                }
                                break;
                            case '[':
                                int b = 1;
                                tempPtr = ptr;
                                StringBuilder bracketCode = new StringBuilder();
                                while (b > 0)
                                {
                                    tempPtr++;
                                    if (tempPtr >= codeToParse.Length)
                                        throw new InvalidOperationException("Cannot start a loop '[' without an ending ']'");

                                    if (codeToParse[tempPtr] == ']')
                                        b--;
                                    else if (codeToParse[tempPtr] == '[')
                                        b++;
                                    
                                    bracketCode.Append(codeToParse[tempPtr]);
                                }

                                var start = Instruction.Create(OpCodes.Nop);
                                var end = Instruction.Create(OpCodes.Nop);

                                il.Append(start);
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldelem_U1);
                                il.Emit(OpCodes.Brfalse, end);
                                
                                ParseCode(bracketCode.ToString());
                                
                                il.Emit(OpCodes.Br, start);
                                il.Append(end);
                                ptr = tempPtr;
                                break;
                            case ']':
                                break;
                            case '.':
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Ldelem_U1);
                                il.Emit(OpCodes.Call, consoleWriteMethod);
                                break;
                            case ',':
                                il.Emit(OpCodes.Ldsfld, memoryArrayField);
                                il.Emit(OpCodes.Ldsfld, memoryPointerField);
                                il.Emit(OpCodes.Call, consoleReadMethod);
                                il.Emit(OpCodes.Stelem_I1);
                                break;
                        }
                    }
                }

                ParseCode(code);
                
                il.Emit(OpCodes.Nop);
                il.Emit(OpCodes.Ret);
                
                assembly.Write("BrainSharp.Compiled.Program.exe");
            }

            Console.WriteLine("Assembly 'BrainSharp.Compiled.Program' created successfully!");
        }
    }
}