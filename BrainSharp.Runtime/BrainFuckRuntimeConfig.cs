using System;
using System.Diagnostics.CodeAnalysis;

namespace BrainSharp.Runtime;

public static class BrainFuckRuntimeConfig
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum LinefeedMode
    {
        Unchanged,
        UnixOnly,
        UnixAndEOF,
        EOFOnly
    }
    
    // ReSharper disable once InconsistentNaming
    public static LinefeedMode LFMode = LinefeedMode.UnixAndEOF;

    public static void ConfigFromArgs(string[] args)
    {
        if(args.Length == 0)
            return;
        
        Console.WriteLine("Running BF program with custom args: ");
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--lfmode", StringComparison.CurrentCultureIgnoreCase) ||
                string.Equals(arg, "-l", StringComparison.CurrentCultureIgnoreCase))
            {
                int newIndex = index + 1;
                if (newIndex < args.Length)
                {
                    string level = args[newIndex].ToLower();
                    switch (level)
                    {
                        case "normal":
                            LFMode = LinefeedMode.Unchanged;
                            break;
                        case "unix":
                            LFMode = LinefeedMode.UnixAndEOF;
                            break;
                        case "unixonly":
                            LFMode = LinefeedMode.UnixOnly;
                            break;
                        case "eof":
                            LFMode = LinefeedMode.EOFOnly;
                            break;
                    }
                    Console.WriteLine(LFMode.ToString());
                }
            }
        }
        Console.WriteLine("End of arg info");
    }
}