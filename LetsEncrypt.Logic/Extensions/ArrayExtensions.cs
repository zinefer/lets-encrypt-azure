using System;

namespace LetsEncrypt.Logic.Extensions
{
    public static class ArrayExtensions
    {
        public static bool IsNullOrEmpty(this Array array)
            => array == null || array.Length == 0;
    }
}
