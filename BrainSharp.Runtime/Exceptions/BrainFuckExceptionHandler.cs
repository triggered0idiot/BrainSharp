using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace BrainSharp.Runtime.Exceptions
{
    public class BrainFuckExceptionHandler : IDisposable
    {
        /// <summary>
        /// Used to determine where in the brainfuck program we are
        /// </summary>
        public int CurrentCharacterIndex = 0;
        public int CurrentMemorySpaceIndex = 0;

        private Memory<byte> _memorySpace = Array.Empty<byte>();
        private string _executingCode;
        private readonly Stopwatch _stopwatch;
    
        public BrainFuckExceptionHandler(string currentCode)
        {
            _executingCode = currentCode;
            AppDomain.CurrentDomain.UnhandledException += _handleException;
            _stopwatch = Stopwatch.StartNew();
        }
    
        public BrainFuckExceptionHandler(string currentCode, Memory<byte> memorySpace)
        {
            _executingCode = currentCode;
            _memorySpace = memorySpace;
            AppDomain.CurrentDomain.UnhandledException += _handleException;
            _stopwatch = Stopwatch.StartNew();
        }
        
        private bool _disposed = false;
        public void Dispose()
        {
            if(_disposed) return;

            _disposed = true;
            _stopwatch.Stop();
            Console.WriteLine($"Program took {_stopwatch.ElapsedMilliseconds}ms to run");
            // weird offset thing so that the object doesn't dispose before it can catch the exception
            Task.Run(() =>
            {
                AppDomain.CurrentDomain.UnhandledException -= _handleException;
            });
        }

        private void _handleException(object sender, UnhandledExceptionEventArgs args)
        {
            ExceptionCaught(args.ExceptionObject as Exception);
        }

        public void ExceptionCaught(Exception exception)
        {
            int lineIndex = 0;
            int characterIndex = 0;
            foreach (var c in _executingCode)
            {
                characterIndex++;
                if(characterIndex == CurrentCharacterIndex)
                    break;
                if (c == '\n')
                {
                    lineIndex++;
                    characterIndex = 0;
                }
            }
        
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Exception caught at {lineIndex}:{characterIndex}");
            Console.WriteLine(exception.Message);
        
            Console.ResetColor();
            Console.WriteLine("BrainSharp state: ");
        
            // code window
            for (int i = -5; i < 5; i++)
            {
                int realIndex = i + CurrentCharacterIndex;
            
                Console.ForegroundColor = ConsoleColor.White;
                if(i == 0)
                    Console.ForegroundColor = ConsoleColor.Cyan;
            
                if(realIndex < 0 || realIndex >= _executingCode.Length)
                {
                    Console.Write(' ');
                    continue;
                }
            
                Console.Write(_executingCode[realIndex]);
            }
            Console.Write('\n');
        
            // memory window
            if (_memorySpace.Length > 0)
            {
                for (int i = -5; i < 5; i++)
                {
                    int realIndex = i + CurrentMemorySpaceIndex;
                
                    Console.ForegroundColor = ConsoleColor.White;
                    if(i == 0)
                        Console.ForegroundColor = ConsoleColor.Cyan;
                
                    if(realIndex < 0 || realIndex >= _memorySpace.Span.Length)
                    {
                        if(i != 0)
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write("***  ");
                        continue;
                    }
            
                    Console.Write($"{_memorySpace.Span[realIndex]:000}");
                    Console.Write("  ");
                }
                Console.ResetColor();
                Console.Write('\n');
                Console.Write($"[memory pointer at {CurrentMemorySpaceIndex}]\n");
            }
            Console.ResetColor();
        }
    }
}