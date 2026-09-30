using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BrainSharp.Exceptions;
using BrainSharp.Processors;
using BrainSharp.Runtime;
using BrainSharp.Runtime.Exceptions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace BrainSharp
{
    /// <summary>
    /// Compiles brainfuck into il code
    /// </summary>
    public class Compiler
    {
        public static readonly List<IBrainFuckCharacterProcessor> CharacterProcessors = new();
        static Compiler() // autoload processors
        {
            RefreshProcessors();
        }
        public static void RefreshProcessors()
        {
            CharacterProcessors.Clear();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes();
                foreach (var type in t)
                {
                    if (type.GetInterface("IBrainFuckCharacterProcessor") != null)
                    {
                        Console.WriteLine($"Found processor {type.FullName}");
                        var constructor = type.GetConstructor(Type.EmptyTypes);
                        CharacterProcessors.Add(constructor?.Invoke(null) as IBrainFuckCharacterProcessor);
                    }
                }
            }
        }
        
        
        public readonly bool IncludeSafetyChecks;
        public readonly int AllocatedBytes;
        public readonly bool OptimiseCode;
        
        public readonly CompilerReferences References = new();
        
        /// <summary>
        /// Creates the compiler with a targeted il processor
        /// TODO: Requires the processor to be in a class that has been init-ed by Compiler.PrepareClass
        /// </summary>
        /// <param name="ilProcessor">The il processor to append compiled code to</param>
        /// <param name="includeSafetyChecks">When true extra checks are added to the compiled code to throw an error when an invalid state occurs</param>
        /// <param name="allocatedBytes">How much memory space to allocate to the program, unlike some other brainfuck interpreters memory isn't allocated dynamically</param>
        public Compiler(ILProcessor ilProcessor, bool includeSafetyChecks = false, int allocatedBytes = 8192, bool optimise = false)
        {
            OptimiseCode =  optimise;
            References.IlProcessor = ilProcessor;
            IncludeSafetyChecks = includeSafetyChecks;
            AllocatedBytes = allocatedBytes;

            var targetBody = ilProcessor.Body;
            var targetMethod = ilProcessor.Body.Method;
            var targetClass = ilProcessor.Body.Method.DeclaringType;
            var targetModule = ilProcessor.Body.Method.DeclaringType.Module;
            var byteType = targetModule.ImportReference(typeof(byte));
            
            References.PtrOutOfBoundsConstructor = targetModule.ImportReference(typeof(MemoryPointerOutOfBounds).GetConstructor(
                new[] { typeof(string) }));
            
            References.ExceptionHandlerType = targetModule.ImportReference(typeof(BrainFuckExceptionHandler));
            References.ExceptionHandlerConstructor = targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetConstructor(
                new[] { typeof(string), typeof(Memory<byte>) }));
            References.ExceptionHandlerCurrentCharacterField =
                targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetField("CurrentCharacterIndex"));
            References.ExceptionHandlerCurrentMemoryPointerField =
                targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetField("CurrentMemorySpaceIndex"));
            References.ExceptionHandlerCurrentCodeScopeField = 
                targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetField("CurrentCodeScope"));
            
            References.DisposeMethod =
                targetModule.ImportReference(typeof(IDisposable).GetMethod("Dispose"));
            References.MemoryByteConstructor = 
                targetModule.ImportReference(typeof(Memory<byte>).GetConstructor(new[] { typeof(byte[]) }));
            
            References.RuntimeConfigArgsMethod = 
                targetModule.ImportReference(typeof(BrainFuckRuntimeConfig).GetMethod("ConfigFromArgs"));
            
            References.ConsoleWriteMethod = targetModule.ImportReference(typeof(Console).GetMethod("Write",
                new[] { typeof(char) }));
            References.ConsoleReadMethod = targetModule.ImportReference(typeof(ConsoleReader).GetMethod("ReadOrPoll", Type.EmptyTypes));

            References.MemoryArrayField = new FieldDefinition("Memory",
                FieldAttributes.Public | FieldAttributes.Static, new ArrayType(targetModule.TypeSystem.Byte));
            References.MemoryPointerField = new FieldDefinition("MemoryPointer",
                FieldAttributes.Public | FieldAttributes.Static, targetModule.TypeSystem.Int32);
            targetClass.Fields.Add(References.MemoryArrayField);
            targetClass.Fields.Add(References.MemoryPointerField);

            var tempByteVar = new VariableDefinition(targetModule.TypeSystem.Byte);
            targetBody.Variables.Add(tempByteVar);
            
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call, References.RuntimeConfigArgsMethod);
            
            // Memory = new byte[AllocatedBytes]
            ilProcessor.Emit(OpCodes.Ldc_I4, AllocatedBytes);
            ilProcessor.Emit(OpCodes.Newarr, byteType);
            ilProcessor.Emit(OpCodes.Stsfld, References.MemoryArrayField);
            // MemoryPointer = 0
            ilProcessor.Emit(OpCodes.Ldc_I4_0);
            ilProcessor.Emit(OpCodes.Stsfld, References.MemoryPointerField);
        }
        
        /// <summary>
        /// Compiles brainfuck code, appends compiled code to the current il processor
        /// </summary>
        /// <param name="code"></param>
        /// <exception cref="InvalidLoopException">The code contains a '[' that isn't followed by a corresponding ']'</exception>
        public void Compile(string code)
        {
            var ilProcessor = References.IlProcessor;
            if(IncludeSafetyChecks)
            {
                References.ExceptionHandlerVariable = new VariableDefinition(References.ExceptionHandlerType);
                ilProcessor.Body.Variables.Add(References.ExceptionHandlerVariable);

                var endInstruction = Instruction.Create(OpCodes.Nop);
                var tryStart = Instruction.Create(OpCodes.Nop);
                var tryEnd = Instruction.Create(OpCodes.Nop);
                var finallyStart = Instruction.Create(OpCodes.Nop);
                var endFinally = Instruction.Create(OpCodes.Endfinally);
                var finallyEnd = Instruction.Create(OpCodes.Nop);
                // using
                ilProcessor.Emit(OpCodes.Ldstr, code);
                ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                ilProcessor.Emit(OpCodes.Newobj, References.MemoryByteConstructor);
                ilProcessor.Emit(OpCodes.Newobj, References.ExceptionHandlerConstructor);
                ilProcessor.Emit(OpCodes.Stloc, References.ExceptionHandlerVariable);

                ilProcessor.Append(tryStart);
                CompilerIteration(code);

                ilProcessor.Emit(OpCodes.Leave, endInstruction);
                //_ilProcessor.Append(tryEnd);

                ilProcessor.Append(finallyStart);
                ilProcessor.Emit(OpCodes.Ldloc, References.ExceptionHandlerVariable);
                ilProcessor.Emit(OpCodes.Brfalse, endFinally);

                ilProcessor.Emit(OpCodes.Ldloc, References.ExceptionHandlerVariable);
                ilProcessor.Emit(OpCodes.Callvirt, References.DisposeMethod);

                ilProcessor.Append(endFinally);
                ilProcessor.Append(finallyEnd);
                ilProcessor.Append(endInstruction);

                var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = tryStart,
                    TryEnd = finallyStart,
                    HandlerStart = finallyStart,
                    HandlerEnd = finallyEnd,
                };
                ilProcessor.Body.ExceptionHandlers.Add(handler);
            }
            else
                CompilerIteration(code);
        }

        internal void CompilerIteration(string code, int ptrOffset = 0)
        {
            var ilProcessor = References.IlProcessor;
            if (IncludeSafetyChecks)
            {
                ilProcessor.Emit(OpCodes.Ldloc, References.ExceptionHandlerVariable);
                ilProcessor.Emit(OpCodes.Ldstr, code);
                ilProcessor.Emit(OpCodes.Stfld, References.ExceptionHandlerCurrentCodeScopeField);
            }
            int ptr = -1;
            while (++ptr < code.Length)
            {
                List<IBrainFuckCharacterProcessor> targetProcessors = new();
                foreach (var processor in CharacterProcessors)
                {
                    if (processor.FlagCharacter(code, ptr))
                    {
                        targetProcessors.Add(processor);
                    }
                }

                if (targetProcessors.Count > 0)
                {
                    if (IncludeSafetyChecks)
                    {
                        ilProcessor.Emit(OpCodes.Ldloc, References.ExceptionHandlerVariable);
                        ilProcessor.Emit(OpCodes.Ldc_I4, ptr);// + ptrOffset);
                        ilProcessor.Emit(OpCodes.Stfld, References.ExceptionHandlerCurrentCharacterField);
                    }

                    foreach (var processor in targetProcessors)
                        processor.ProcessCharacter(code, ref ptr, ilProcessor, this);
                }
                
            }
        }
    }
}


                /*
                switch (c)
                {
                    case '+':
                        // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] + 1);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldelem_U1);
                        ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        ilProcessor.Emit(OpCodes.Add);

                        ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                    case '-':
                        // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] - 1);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldelem_U1);
                        ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        ilProcessor.Emit(OpCodes.Sub);

                        ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                    case '>':
                        // memoryPointer++
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        ilProcessor.Emit(OpCodes.Add);
                        ilProcessor.Emit(OpCodes.Stsfld, References.MemoryPointerField);
                        
                        if(IncludeSafetyChecks)
                        {
                            ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                            ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                            ilProcessor.Emit(OpCodes.Stfld, References.ExceptionHandlerCurrentMemoryPointerField);
                            
                            // if (MemoryPointer < Memory.Length)
                            ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                            ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                            ilProcessor.Emit(OpCodes.Ldlen);
                            ilProcessor.Emit(OpCodes.Conv_I4);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            var nopEnd2 = Instruction.Create(OpCodes.Nop);
                            ilProcessor.Emit(OpCodes.Bge, nopEnd);
                            ilProcessor.Emit(OpCodes.Br, nopEnd2);
                            ilProcessor.Append(nopEnd);
                            ilProcessor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Overflow);
                            ilProcessor.Emit(OpCodes.Newobj, References.PtrOutOfBoundsConstructor);
                            ilProcessor.Emit(OpCodes.Throw);
                            ilProcessor.Append(nopEnd2);
                        }
                        break;
                    case '<':
                        // memoryPointer--
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        ilProcessor.Emit(OpCodes.Sub);
                        ilProcessor.Emit(OpCodes.Stsfld, References.MemoryPointerField);
                        
                        if(IncludeSafetyChecks)
                        {
                            ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                            ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                            ilProcessor.Emit(OpCodes.Stfld, References.ExceptionHandlerCurrentMemoryPointerField);
                            
                            // if (0 > MemoryPointer)
                            ilProcessor.Emit(OpCodes.Ldc_I4_0);
                            ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                            ilProcessor.Emit(OpCodes.Cgt);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            ilProcessor.Emit(OpCodes.Brfalse, nopEnd);
                            ilProcessor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Underflow);
                            ilProcessor.Emit(OpCodes.Newobj, References.PtrOutOfBoundsConstructor);
                            ilProcessor.Emit(OpCodes.Throw);
                            ilProcessor.Append(nopEnd);
                        }
                        break;
                    case '[':
                        int b = 1;
                        tempPtr = ptr;
                        StringBuilder bracketCode = new StringBuilder();
                        while (b > 0)
                        {
                            tempPtr++;
                            if (tempPtr >= code.Length)
                                throw new InvalidLoopException("Cannot start a loop '[' without an ending ']'");

                            if (code[tempPtr] == ']')
                                b--;
                            else if (code[tempPtr] == '[')
                                b++;
                            
                            bracketCode.Append(code[tempPtr]);
                        }

                        var start = Instruction.Create(OpCodes.Nop);
                        var end = Instruction.Create(OpCodes.Nop);

                        ilProcessor.Append(start);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldelem_U1);
                        ilProcessor.Emit(OpCodes.Brfalse, end);
                        
                        // ptrOffset is purely used for exception handling checking
                        _compilerIteration(bracketCode.ToString(), exceptionHandlerVariable, ptr + 1, lineCount);
                        
                        ilProcessor.Emit(OpCodes.Br, start);
                        ilProcessor.Append(end);
                        ptr = tempPtr;
                        break;
                    case ']':
                        break;
                    case '.':
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Ldelem_U1);
                        ilProcessor.Emit(OpCodes.Call, References.ConsoleWriteMethod);
                        break;
                    case ',':
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryArrayField);
                        ilProcessor.Emit(OpCodes.Ldsfld, References.MemoryPointerField);
                        ilProcessor.Emit(OpCodes.Call, References.ConsoleReadMethod);
                        ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                }
                */