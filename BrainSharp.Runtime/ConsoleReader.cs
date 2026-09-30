using System;
using System.Collections.Generic;
using System.Linq;

namespace BrainSharp.Runtime;

public static class ConsoleReader
{
    private static Stack<byte> _currentStream = new();
    
    public static byte ReadOrPoll()
    {
        if (_currentStream.Count == 0)
        {
            List<byte> consoleRead = new List<byte>();
            int c;
            while ((c = Console.Read()) != -1)
            {
                if(BrainFuckRuntimeConfig.LFMode is not BrainFuckRuntimeConfig.LinefeedMode.Unchanged)
                    if(c == 13)
                        continue;
                
                consoleRead.Add((byte)c);
                if(c == 10)
                    break;
            }
            if(BrainFuckRuntimeConfig.LFMode is BrainFuckRuntimeConfig.LinefeedMode.UnixAndEOF or BrainFuckRuntimeConfig.LinefeedMode.EOFOnly)
                if(consoleRead.Count != 0 && consoleRead.Last() != 0)
                    consoleRead.Add(0);
            consoleRead.Reverse();
            foreach(var item in consoleRead)
                _currentStream.Push(item);
        }
        
        return _currentStream.Pop();
    }
}