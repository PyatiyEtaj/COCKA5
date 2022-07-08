using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyV2
{
    public static class Util
    {
        private static object _lockker = new();
        public static ConsoleColor Orig { get; set; } = Console.ForegroundColor;
        public static void WriteLine(string msg, ConsoleColor color)
        {
            lock(_lockker)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(msg);
                Console.ForegroundColor = Orig;
            }
        }

        public static string ByteArrayToString(IEnumerable<byte> data) => string.Join(" ", Array.ConvertAll(data.ToArray(), b => b.ToString("X2")));
    }
}
