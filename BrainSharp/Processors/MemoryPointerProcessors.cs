using BrainSharp.Runtime.Exceptions;
using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    public class IncrementMemoryPointerProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '>';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            
            var addValue = 1;
            var tempPtr = currentCharacterIndex;
            if(compiler.OptimiseCode)
            {
                while (++tempPtr < code.Length)
                {
                    if (code[tempPtr] == '>')
                    {
                        addValue++;
                        currentCharacterIndex = tempPtr;
                    }
                    else if (code[tempPtr] == '<')
                    {
                        addValue--;
                        currentCharacterIndex = tempPtr;
                    }
                    else
                        break;
                }
                if(addValue == 0)
                    return;
            }
            
            // memoryPointer++
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldc_I4, addValue);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stsfld, references.MemoryPointerField);
                        
            if(compiler.IncludeSafetyChecks)
            {
                if(addValue > 0)
                    IncrementMemoryPointerProcessor.AddSafetyCheck(code, ref currentCharacterIndex, processor, compiler);
                else
                    DecrementMemoryPointerProcessor.AddSafetyCheck(code, ref currentCharacterIndex, processor, compiler);
            }
        }

        public static void AddSafetyCheck(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            processor.Emit(OpCodes.Ldloc, references.ExceptionHandlerVariable);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Stfld, references.ExceptionHandlerCurrentMemoryPointerField);
                            
            // if (MemoryPointer < Memory.Length)
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldlen);
            processor.Emit(OpCodes.Conv_I4);

            var nopEnd = Instruction.Create(OpCodes.Nop);
            var nopEnd2 = Instruction.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Bge, nopEnd);
            processor.Emit(OpCodes.Br, nopEnd2);
            processor.Append(nopEnd);
            processor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Overflow);
            processor.Emit(OpCodes.Newobj, references.PtrOutOfBoundsConstructor);
            processor.Emit(OpCodes.Throw);
            processor.Append(nopEnd2);
        }
    }
    
    public class DecrementMemoryPointerProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '<';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            
            var subValue = 1;
            var tempPtr = currentCharacterIndex;
            if(compiler.OptimiseCode)
            {
                while (++tempPtr < code.Length)
                {
                    if (code[tempPtr] == '>')
                    {
                        subValue--;
                        currentCharacterIndex = tempPtr;
                    }
                    else if (code[tempPtr] == '<')
                    {
                        subValue++;
                        currentCharacterIndex = tempPtr;
                    }
                    else
                        break;
                }
                if(subValue == 0)
                    return;
            }
            
            // memoryPointer--
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldc_I4, subValue);
            processor.Emit(OpCodes.Sub);
            processor.Emit(OpCodes.Stsfld, references.MemoryPointerField);
                        
            if(compiler.IncludeSafetyChecks)
            {
                if(subValue > 0)
                    IncrementMemoryPointerProcessor.AddSafetyCheck(code, ref currentCharacterIndex, processor, compiler);
                else
                    DecrementMemoryPointerProcessor.AddSafetyCheck(code, ref currentCharacterIndex, processor, compiler);
            }
        }

        public static void AddSafetyCheck(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            
            processor.Emit(OpCodes.Ldloc, references.ExceptionHandlerVariable);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Stfld, references.ExceptionHandlerCurrentMemoryPointerField);
                            
            // if (0 > MemoryPointer)
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Cgt);

            var nopEnd = Instruction.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Brfalse, nopEnd);
            processor.Emit(OpCodes.Ldstr, MemoryPointerOutOfBounds.Underflow);
            processor.Emit(OpCodes.Newobj, references.PtrOutOfBoundsConstructor);
            processor.Emit(OpCodes.Throw);
            processor.Append(nopEnd);
        }
    }
}