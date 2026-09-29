using Mono.Cecil.Cil;

namespace BrainSharp.Processors
{
    /// <summary>
    /// Interface used to define a character processor.
    /// </summary>
    public interface IBrainFuckCharacterProcessor
    {
        /// <summary>
        /// Flags a character as being a character this processor can process
        /// </summary>
        /// <param name="code">The input code</param>
        /// <param name="currentCharacterIndex">Current character's index</param>
        /// <returns>Weather to process this character or not</returns>
        bool FlagCharacter(string code, int currentCharacterIndex);

        /// <summary>
        /// Processes a character.
        /// </summary>
        /// <param name="code">The input code</param>
        /// <param name="currentCharacterIndex">Current character's index</param>
        /// <param name="processor">The processor to send the generated code to</param>
        /// <param name="compiler">The compiler being used to compile the code</param>
        void ProcessCharacter(string code, ref int currentCharacterIndex, ILProcessor processor, Compiler compiler);
    }
}