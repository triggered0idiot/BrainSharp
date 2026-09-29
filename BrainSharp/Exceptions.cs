using System;

namespace BrainSharp.Exceptions
{
    public class InvalidLoopException : Exception
    {
        public InvalidLoopException(string message) : base(message){}
    }
}

// TODO: add to compiled code
namespace BrainSharp.Runtime.Exceptions
{
    public class MemoryPointerOutOfBounds : Exception
    {
        public MemoryPointerOutOfBounds(string message) : base(message){}
    }
}