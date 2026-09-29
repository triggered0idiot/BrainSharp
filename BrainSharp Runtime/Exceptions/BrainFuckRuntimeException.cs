using System;

namespace BrainSharp.Runtime.Exceptions;
public class BrainFuckRuntimeException(string message) : Exception(message);