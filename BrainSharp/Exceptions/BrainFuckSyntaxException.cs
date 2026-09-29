using System;

namespace BrainSharp.Exceptions
{
    public class BrainFuckSyntaxException : Exception
    {
        public BrainFuckSyntaxException(string message) : base(message)
        {
        }
    }
}