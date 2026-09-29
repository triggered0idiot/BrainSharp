using System;

namespace BrainSharp.Runtime.Exceptions
{
    public class MemoryPointerOutOfBounds : BrainFuckRuntimeException
    {
        public MemoryPointerOutOfBounds(string message) : base(message)
        {
        }

        public const string Underflow = "The memory pointer has underflow past 0.";
        public const string Overflow = "The memory pointer has overflown past the maximum index.";
    }
}