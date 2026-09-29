using System;

namespace BrainSharp.Runtime.Exceptions
{
    public class BrainFuckRuntimeException : Exception
    {
        public BrainFuckRuntimeException(string message) : base(message)
        {
        }
    }
}