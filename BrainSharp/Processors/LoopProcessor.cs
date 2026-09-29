using System.Text;
using BrainSharp.Exceptions;
using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    public class LoopProcessor : IBrainFuckCharacterProcessor
    {
        public bool FlagCharacter(string code, int currentCharacterIndex)
        {
            return code[currentCharacterIndex] == '[';
        }

        public void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor, Compiler compiler)
        {
            var references = compiler.References;
            
            int b = 1;
            int tempPtr = currentCharacterIndex;
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

            string strBracketCode = bracketCode.ToString();
            if (compiler.OptimiseCode)
            {
                bool IsCodeUsed()
                {
                    for (int i = 0; i < strBracketCode.Length; i++)
                    {
                        foreach (var characterProcessor in Compiler.CharacterProcessors)
                        {
                            if (characterProcessor.FlagCharacter(strBracketCode, i))
                                return true;
                        }
                    }

                    return false;
                }

                if (!IsCodeUsed())
                    return;
            }

            var start = Instruction.Create(OpCodes.Nop);
            var end = Instruction.Create(OpCodes.Nop);

            processor.Append(start);
            processor.Emit(OpCodes.Ldsfld, references.MemoryArrayField);
            processor.Emit(OpCodes.Ldsfld, references.MemoryPointerField);
            processor.Emit(OpCodes.Ldelem_U1);
            processor.Emit(OpCodes.Brfalse, end);
            
            // ptrOffset is purely used for exception handling checking
            compiler.CompilerIteration(strBracketCode, currentCharacterIndex + 1);
                        
            processor.Emit(OpCodes.Br, start);
            processor.Append(end);
            currentCharacterIndex = tempPtr;
        }
    }
}