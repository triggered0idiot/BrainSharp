using System;

namespace BrainSharp.Exceptions;
public class BrainFuckSyntaxException(string message) : Exception(message);