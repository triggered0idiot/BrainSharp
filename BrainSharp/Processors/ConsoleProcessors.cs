using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    public class PrintProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '.';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor, Compiler compiler)
        {
            var references = compiler.References;
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldelem_U1);
            processor.Emit(OpCodes.Call, references.ConsoleWriteMethod);
        }
    }
    
    public class ReadProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == ',';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor, Compiler compiler)
        {
            var references = compiler.References;
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Call, references.ConsoleReadMethod);
            processor.Emit(OpCodes.Stelem_I1);
        }
    }
}