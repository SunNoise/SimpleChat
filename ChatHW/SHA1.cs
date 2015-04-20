using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace ChatHW
{
    internal static class SHA1
    {
        public const int SIZE = 20;

        internal static byte[] Compute(byte[] temp)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(temp);
                return hash;
            }
        }
    }
}
