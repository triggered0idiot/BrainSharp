using System;

namespace BrainSharp.Exceptions;
public class InvalidLoopException(string message) : BrainFuckSyntaxException(message);