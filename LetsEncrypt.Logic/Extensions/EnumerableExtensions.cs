using System;
using System.Collections.Generic;
using System.Linq;

namespace LetsEncrypt.Logic.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool Contains(this IEnumerable<string> enumerable, string value, StringComparison stringComparison)
        {
            return enumerable.Any(e => e.IndexOf(value, stringComparison) >= 0);
        }
    }
}
