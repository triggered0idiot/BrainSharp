using System;
using System.Text;
using BrainSharp.Exceptions;
using BrainSharp.Runtime.Exceptions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BrainSharp
{
    /// <summary>
    /// Compiles brainfuck into il code
    /// </summary>
    public class Compiler
    {
        public readonly bool IncludeSafetyChecks;
        public readonly int AllocatedBytes;
        
        private readonly ILProcessor _ilProcessor;
        
        private readonly MethodReference _ptrOutOfBoundsConstructor;
        
        private readonly MethodReference _consoleWriteMethod;
        private readonly MethodReference _consoleReadMethod;
        
        private readonly FieldDefinition _memoryArrayField;
        private readonly FieldDefinition _memoryPointerField;

        private readonly TypeReference _exceptionHandlerType;
        private readonly MethodReference _exceptionHandlerConstructor;
        private readonly FieldReference _exceptionHandlerCurrentCharacterField;
        private readonly FieldReference _exceptionHandlerCurrentMemoryPointerField;
        private readonly MethodReference _memoryByteConstructor;
        private readonly MethodReference _disposeMethod;

        /// <summary>
        /// Creates the compiler with a targeted il processor
        /// TODO: Requires the processor to be in a class that has been init-ed by Compiler.PrepareClass
        /// </summary>
        /// <param name="ilProcessor">The il processor to append compiled code to</param>
        /// <param name="includeSafetyChecks">When true extra checks are added to the compiled code to throw an error when an invalid state occurs</param>
        /// <param name="allocatedBytes">How much memory space to allocate to the program, unlike some other brainfuck interpreters memory isn't allocated dynamically</param>
        public Compiler(ILProcessor ilProcessor, bool includeSafetyChecks = false, int allocatedBytes = 8192)
        {
            _ilProcessor = ilProcessor;
            IncludeSafetyChecks = includeSafetyChecks;
            AllocatedBytes = allocatedBytes;

            var targetBody = _ilProcessor.Body;
            var targetMethod = _ilProcessor.Body.Method;
            var targetClass = _ilProcessor.Body.Method.DeclaringType;
            var targetModule = _ilProcessor.Body.Method.DeclaringType.Module;
            var byteType = targetModule.ImportReference(typeof(byte));
            
            _ptrOutOfBoundsConstructor = targetModule.ImportReference(typeof(MemoryPointerOutOfBounds).GetConstructor([typeof(string)]));
            
            _exceptionHandlerType = targetModule.ImportReference(typeof(BrainFuckExceptionHandler));
            _exceptionHandlerConstructor = targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetConstructor([typeof(string), typeof(Memory<byte>)]));
            _exceptionHandlerCurrentCharacterField =
                targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetField("CurrentCharacterIndex"));
            _exceptionHandlerCurrentMemoryPointerField =
                targetModule.ImportReference(typeof(BrainFuckExceptionHandler).GetField("CurrentMemorySpaceIndex"));
            _disposeMethod =
                targetModule.ImportReference(typeof(IDisposable).GetMethod("Dispose"));
            _memoryByteConstructor = 
                targetModule.ImportReference(typeof(Memory<byte>).GetConstructor([typeof(byte[])]));
            
            _consoleWriteMethod = targetModule.ImportReference(typeof(Console).GetMethod("Write", [typeof(char)]));
            _consoleReadMethod = targetModule.ImportReference(typeof(Console).GetMethod("Read", Type.EmptyTypes));

            _memoryArrayField = new FieldDefinition("Memory",
                FieldAttributes.Public | FieldAttributes.Static, new ArrayType(targetModule.TypeSystem.Byte));
            _memoryPointerField = new FieldDefinition("MemoryPointer",
                FieldAttributes.Public | FieldAttributes.Static, targetModule.TypeSystem.Int32);
            targetClass.Fields.Add(_memoryArrayField);
            targetClass.Fields.Add(_memoryPointerField);

            var tempByteVar = new VariableDefinition(targetModule.TypeSystem.Byte);
            targetBody.Variables.Add(tempByteVar);
            
            // Memory = new byte[AllocatedBytes]
            _ilProcessor.Emit(OpCodes.Ldc_I4, AllocatedBytes);
            _ilProcessor.Emit(OpCodes.Newarr, byteType);
            _ilProcessor.Emit(OpCodes.Stsfld, _memoryArrayField);
            // MemoryPointer = 0
            _ilProcessor.Emit(OpCodes.Ldc_I4_0);
            _ilProcessor.Emit(OpCodes.Stsfld, _memoryPointerField);
        }

        /// <summary>
        /// Compiles brainfuck code, appends compiled code to the current il processor
        /// </summary>
        /// <param name="code"></param>
        /// <exception cref="InvalidLoopException">The code contains a '[' that isn't followed by a corresponding ']'</exception>
        public void Compile(string code)
        {
            if(IncludeSafetyChecks)
            {
                var exceptionHandlerVariable = new VariableDefinition(_exceptionHandlerType);
                _ilProcessor.Body.Variables.Add(exceptionHandlerVariable);

                var endInstruction = Instruction.Create(OpCodes.Nop);
                var tryStart = Instruction.Create(OpCodes.Nop);
                var tryEnd = Instruction.Create(OpCodes.Nop);
                var finallyStart = Instruction.Create(OpCodes.Nop);
                var endFinally = Instruction.Create(OpCodes.Endfinally);
                var finallyEnd = Instruction.Create(OpCodes.Nop);
                // using
                _ilProcessor.Emit(OpCodes.Ldstr, code);
                _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                _ilProcessor.Emit(OpCodes.Newobj, _memoryByteConstructor);
                _ilProcessor.Emit(OpCodes.Newobj, _exceptionHandlerConstructor);
                _ilProcessor.Emit(OpCodes.Stloc, exceptionHandlerVariable);

                _ilProcessor.Append(tryStart);
                _compilerIteration(code, exceptionHandlerVariable);

                _ilProcessor.Emit(OpCodes.Leave, endInstruction);
                //_ilProcessor.Append(tryEnd);

                _ilProcessor.Append(finallyStart);
                _ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                _ilProcessor.Emit(OpCodes.Brfalse, endFinally);

                _ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                _ilProcessor.Emit(OpCodes.Callvirt, _disposeMethod);

                _ilProcessor.Append(endFinally);
                _ilProcessor.Append(finallyEnd);
                _ilProcessor.Append(endInstruction);

                var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = tryStart,
                    TryEnd = finallyStart,
                    HandlerStart = finallyStart,
                    HandlerEnd = finallyEnd,
                };
                _ilProcessor.Body.ExceptionHandlers.Add(handler);
            }
            else
                _compilerIteration(code, null);
        }

        private void _compilerIteration(string code, VariableDefinition exceptionHandlerVariable, int ptrOffset = 0)
        {
            int ptr = -1;
            while (++ptr < code.Length)
            {
                char c = code[ptr];
                int tempPtr;
                Instruction nopEnd;

                if (IncludeSafetyChecks)
                {
                    _ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                    _ilProcessor.Emit(OpCodes.Ldc_I4, ptr + ptrOffset);
                    _ilProcessor.Emit(OpCodes.Stfld, _exceptionHandlerCurrentCharacterField);
                }

                switch (c)
                {
                    case '+':
                        // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] + 1);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldelem_U1);
                        _ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        _ilProcessor.Emit(OpCodes.Add);

                        _ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                    case '-':
                        // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] - 1);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldelem_U1);
                        _ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        _ilProcessor.Emit(OpCodes.Sub);

                        _ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                    case '>':
                        // memoryPointer++
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        _ilProcessor.Emit(OpCodes.Add);
                        _ilProcessor.Emit(OpCodes.Stsfld, _memoryPointerField);
                        
                        if(IncludeSafetyChecks)
                        {
                            _ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                            _ilProcessor.Emit(OpCodes.Stfld, _exceptionHandlerCurrentMemoryPointerField);
                            
                            // if (MemoryPointer < Memory.Length)
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                            _ilProcessor.Emit(OpCodes.Ldlen);
                            _ilProcessor.Emit(OpCodes.Conv_I4);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            var nopEnd2 = Instruction.Create(OpCodes.Nop);
                            _ilProcessor.Emit(OpCodes.Bge, nopEnd);
                            _ilProcessor.Emit(OpCodes.Br, nopEnd2);
                            _ilProcessor.Append(nopEnd);
                            _ilProcessor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Overflow);
                            _ilProcessor.Emit(OpCodes.Newobj, _ptrOutOfBoundsConstructor);
                            _ilProcessor.Emit(OpCodes.Throw);
                            _ilProcessor.Append(nopEnd2);
                        }
                        break;
                    case '<':
                        // memoryPointer--
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldc_I4_1);
                        _ilProcessor.Emit(OpCodes.Sub);
                        _ilProcessor.Emit(OpCodes.Stsfld, _memoryPointerField);
                        
                        if(IncludeSafetyChecks)
                        {
                            _ilProcessor.Emit(OpCodes.Ldloc, exceptionHandlerVariable);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                            _ilProcessor.Emit(OpCodes.Stfld, _exceptionHandlerCurrentMemoryPointerField);
                            
                            // if (0 > MemoryPointer)
                            _ilProcessor.Emit(OpCodes.Ldc_I4_0);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                            _ilProcessor.Emit(OpCodes.Cgt);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            _ilProcessor.Emit(OpCodes.Brfalse, nopEnd);
                            _ilProcessor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Underflow);
                            _ilProcessor.Emit(OpCodes.Newobj, _ptrOutOfBoundsConstructor);
                            _ilProcessor.Emit(OpCodes.Throw);
                            _ilProcessor.Append(nopEnd);
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

                        _ilProcessor.Append(start);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldelem_U1);
                        _ilProcessor.Emit(OpCodes.Brfalse, end);
                        
                        // ptrOffset is purely used for exception handling checking
                        _compilerIteration(bracketCode.ToString(), exceptionHandlerVariable, ptr + 1);
                        
                        _ilProcessor.Emit(OpCodes.Br, start);
                        _ilProcessor.Append(end);
                        ptr = tempPtr;
                        break;
                    case ']':
                        break;
                    case '.':
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Ldelem_U1);
                        _ilProcessor.Emit(OpCodes.Call, _consoleWriteMethod);
                        break;
                    case ',':
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                        _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                        _ilProcessor.Emit(OpCodes.Call, _consoleReadMethod);
                        _ilProcessor.Emit(OpCodes.Stelem_I1);
                        break;
                }
            }
        }
    }
}