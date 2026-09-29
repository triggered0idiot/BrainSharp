using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    public class AdditionProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '+';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            
            // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] + 1);
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
                        
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldelem_U1);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            
            processor.Emit(OpCodes.Stelem_I1);
        }
    }
    
    public class SubtractionProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '-';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor,
            Compiler compiler)
        {
            var references = compiler.References;
            // Memory[MemoryPointer] = (byte)(Memory[MemoryPointer] - 1);
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
                        
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldelem_U1);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Sub);
            
            processor.Emit(OpCodes.Stelem_I1);
        }
    }
}