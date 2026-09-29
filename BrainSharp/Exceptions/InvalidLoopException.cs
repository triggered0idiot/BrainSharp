using System;

namespace BrainSharp.Exceptions
{
    public class InvalidLoopException : BrainFuckSyntaxException
    {
        public InvalidLoopException(string message) : base(message)
        {
        }
    }
}