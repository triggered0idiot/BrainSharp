using System;

namespace BrainSharp.Runtime.Exceptions;

public class MemoryPointerOutOfBounds(string message) : BrainFuckRuntimeException(message)
{
    public const string Underflow = "The memory pointer has underflow past 0.";
    public const string Overflow = "The memory pointer has overflown past the maximum index.";
}