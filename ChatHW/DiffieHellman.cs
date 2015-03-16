using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChatHW
{
    class DiffieHellman
    {
        public long q = 2426697107, a = 17123207;
        private long x;
        internal long key;
        public DiffieHellman(Random rnd, long y, out long publicKey)
        {
            x = LongRandom(q, rnd);
            publicKey = fast_exp(a, x, q);

            key = fast_exp(y, x, q);
        }

        public DiffieHellman(Random rnd, out long publicKey)
        {
            x = LongRandom(q, rnd);
            publicKey = fast_exp(a, x, q);
        }

        public DiffieHellman(Random rnd, long q, long a, long y, out long publicKey)
        {
            x = LongRandom(q, rnd);
            publicKey = fast_exp(a, x, q);

            key = fast_exp(y, x, q);
        }

        public DiffieHellman(Random rnd, long q, long a, out long publicKey)
        {
            x = LongRandom(q, rnd);
            publicKey = fast_exp(a, x, q);
        }

        private long LongRandom(long max, Random rand)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max)));
        }

        private long fast_exp(long lbase, long exp, long q)
        {
            if (exp == 0)
            {
                return 1;
            }
            else
            {
                if (exp%2 == 0)
                {
                    return fast_exp(lbase * lbase % q, exp / 2, q);
                }
                else
                {
                    return lbase * fast_exp(lbase, exp - 1, q) % q;
                }
            }
        }

        public void Update(long y)
        {
            key = fast_exp(y, x, q);
        }
    }
}
