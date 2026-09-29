using System;
using System.Text;
using BrainSharp.Exceptions;
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
        private readonly MethodReference _outOfMemoryConstruct;
        private readonly MethodReference _outOfRangeConstruct;
        private readonly MethodReference _consoleWriteMethod;
        private readonly MethodReference _consoleReadMethod;
        private readonly FieldDefinition _memoryArrayField;
        private readonly FieldDefinition _memoryPointerField;

        /// <summary>
        /// Creates the compiler with a targeted il processor
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
            
            _outOfMemoryConstruct = targetModule.ImportReference(typeof(OutOfMemoryException).GetConstructor(Type.EmptyTypes));
            _outOfRangeConstruct = targetModule.ImportReference(typeof(ArgumentOutOfRangeException).GetConstructor(Type.EmptyTypes));
            
            _consoleWriteMethod = targetModule.ImportReference(typeof(Console).GetMethod("Write", new []{typeof(char)}));
            _consoleReadMethod = targetModule.ImportReference(typeof(Console).GetMethod("Read", Type.EmptyTypes));

            _memoryArrayField = new FieldDefinition("Memory",
                FieldAttributes.Public | FieldAttributes.Static, new ArrayType(targetModule.TypeSystem.Byte));
            _memoryPointerField = new FieldDefinition("MemoryPointer",
                FieldAttributes.Public | FieldAttributes.Static, targetModule.TypeSystem.Int32);
            targetClass.Fields.Add(_memoryArrayField);
            targetClass.Fields.Add(_memoryPointerField);

            targetMethod.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, new ArrayType(targetModule.TypeSystem.String)));
            targetClass.Methods.Add(targetMethod);

            var tempByteVar = new VariableDefinition(targetModule.TypeSystem.Byte);
            targetBody.Variables.Add(tempByteVar);
            
            _ilProcessor.Emit(OpCodes.Ldc_I4, AllocatedBytes);
            _ilProcessor.Emit(OpCodes.Newarr, byteType);
            _ilProcessor.Emit(OpCodes.Stsfld, _memoryArrayField);
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
            int ptr = -1;
            while (++ptr < code.Length)
            {
                char c = code[ptr];
                int tempPtr;
                Instruction nopEnd;
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
                            // if (MemoryPointer < Memory.Length)
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryArrayField);
                            _ilProcessor.Emit(OpCodes.Ldlen);
                            _ilProcessor.Emit(OpCodes.Conv_I4);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            _ilProcessor.Emit(OpCodes.Bge, nopEnd);
                            _ilProcessor.Emit(OpCodes.Newobj, _outOfMemoryConstruct);
                            _ilProcessor.Emit(OpCodes.Throw);
                            _ilProcessor.Append(nopEnd);
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
                            // if (0 > MemoryPointer)
                            _ilProcessor.Emit(OpCodes.Ldc_I4_0);
                            _ilProcessor.Emit(OpCodes.Ldsfld, _memoryPointerField);
                            _ilProcessor.Emit(OpCodes.Cgt);

                            nopEnd = Instruction.Create(OpCodes.Nop);
                            _ilProcessor.Emit(OpCodes.Brfalse, nopEnd);
                            _ilProcessor.Emit(OpCodes.Newobj, _outOfRangeConstruct);
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
                        
                        Compile(bracketCode.ToString());
                        
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