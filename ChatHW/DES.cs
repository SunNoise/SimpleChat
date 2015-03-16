using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ChatHW
{
    internal static class DES
    {
        private static byte[] PerformCryptography(ICryptoTransform cryptoTransform, byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();
                    return memoryStream.ToArray();
                }
            }
        }

        internal static byte[] Encrypt(byte[] data, byte[] key, byte[] iv = null)
        {
            if (iv == null)
                iv = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using (DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider())
            {
                cryptoProvider.Padding = PaddingMode.None;
                cryptoProvider.Mode = CipherMode.CBC;

                if (System.Security.Cryptography.DES.IsWeakKey(key) || System.Security.Cryptography.DES.IsSemiWeakKey(key))
                {
                    using (var decryptor = cryptoProvider.CreateWeakEncryptor(key, iv))
                    {
                        return PerformCryptography(decryptor, data);
                    }
                }
                else
                {
                    using (var decryptor = cryptoProvider.CreateEncryptor(key, iv))
                    {
                        return PerformCryptography(decryptor, data);
                    }
                }
            }
        }

        internal static byte[] Decrypt(byte[] data, byte[] key, byte[] iv = null)
        {
            if (iv == null)
                iv = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using (DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider())
            {
                cryptoProvider.Padding = PaddingMode.None;
                cryptoProvider.Mode = CipherMode.CBC;

                if (System.Security.Cryptography.DES.IsWeakKey(key) || System.Security.Cryptography.DES.IsSemiWeakKey(key))
                {
                    using (var decryptor = cryptoProvider.CreateWeakDecryptor(key, iv))
                    {
                        return PerformCryptography(decryptor, data);
                    }
                }
                else
                {
                    using (var decryptor = cryptoProvider.CreateDecryptor(key, iv))
                    {
                        return PerformCryptography(decryptor, data);
                    }
                }
            }
        }
    }

    public static class DESCryptoExtensions
    {
        public static ICryptoTransform CreateWeakEncryptor(this DESCryptoServiceProvider cryptoProvider, byte[] key, byte[] iv, int cryptoAPITransformMode = 0)
        {
            // reflective way of doing what CreateEncryptor() does, bypassing the check for weak keys
            MethodInfo mi = cryptoProvider.GetType().GetMethod("_NewEncryptor", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] Par = { key, cryptoProvider.Mode, iv, cryptoProvider.FeedbackSize, cryptoAPITransformMode };
            ICryptoTransform trans = mi.Invoke(cryptoProvider, Par) as ICryptoTransform;
            return trans;
        }

        public static ICryptoTransform CreateWeakEncryptor(this DESCryptoServiceProvider cryptoProvider)
        {
            return CreateWeakEncryptor(cryptoProvider, cryptoProvider.Key, cryptoProvider.IV);
        }

        public static ICryptoTransform CreateWeakDecryptor(this DESCryptoServiceProvider cryptoProvider, byte[] key, byte[] iv)
        {
            return CreateWeakEncryptor(cryptoProvider, key, iv, 1);
        }

        public static ICryptoTransform CreateWeakDecryptor(this DESCryptoServiceProvider cryptoProvider)
        {
            return CreateWeakDecryptor(cryptoProvider, cryptoProvider.Key, cryptoProvider.IV);
        }
    }
}