using System;
using System.Collections.Generic;
using BrainSharp.Exceptions;
using BrainSharp.Runtime.Exceptions;

namespace BrainSharp
{
    /// <summary>
    /// Super simple brainfuck interpreter
    /// </summary>
    public static class Interpreter
    {
        private class LoopData
        {
            public int StartBracket = 0;
            public int EndBracket = 0;
        }

        public static bool IncludeSafetyChecks = true;
        public static int AllocatedBytes = 8192;
        // Logs the highest memory address that has been used so far (program specific)
        public static bool MemoryTrack = false;

        /// <summary>
        /// Executes brainfuck code
        /// </summary>
        /// <param name="code">The code to execute</param>
        /// <exception cref="MemoryPointerOutOfBounds">The memory pointer has moved below 0 or above <see cref="AllocatedBytes"/></exception>
        /// <exception cref="InvalidLoopException">The code contains a '[' that isn't followed by a corresponding ']'</exception>
        public static void Run(string code)
        {
            int highestMemAddr = 0;
            Stack<LoopData> loops = new Stack<LoopData>();
            byte[] memory = new byte[AllocatedBytes];
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
                            throw new MemoryPointerOutOfBounds("The memory pointer has overflown past the maximum index.");
                        break;
                    case '<':
                        memPtr--;
                        if (memPtr < 0)
                            throw new MemoryPointerOutOfBounds("The memory pointer has underflow past 0.");
                        break;
                    case '[':
                        int b = 1;
                        tempPtr = ptr;
                        while (b > 0)
                        {
                            tempPtr++;
                            if (tempPtr >= code.Length)
                                throw new InvalidLoopException("Cannot start a loop '[' without an ending ']'");

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
                if (memPtr > highestMemAddr)
                {
                    highestMemAddr = memPtr;
                    Console.WriteLine($"New highest memory address {highestMemAddr}");
                }
            }

            Console.Write('\n');
            highestMemAddr = Math.Max(memPtr, highestMemAddr) + 1;
            Console.WriteLine($"Highest memory address {highestMemAddr}");
            Console.WriteLine("End of Program");
        }
    }
}