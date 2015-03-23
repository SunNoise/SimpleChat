using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ChatHW
{
    public enum Function
    {
        SIMP_CHAT_MSG = 1,
        SIMP_INIT_COMM = 2,
        SIMP_KEY_COMPUTED = 3
    }

    class Message
    {
        public const int SIZE = 256;
        byte[] _ascp = new byte[4];
        byte[] _version = new byte[5];
        byte _size = new byte();
        byte[] _function = new byte[2];
        byte[] _state = new byte[4];
        byte[] _id_session = new byte[4];
        byte[] _data = new byte[236];
        byte[] completeBytes = new byte[SIZE];

        public byte[] CompleteBytes
        {
            get { return completeBytes; }
        }

        public byte[] getData
        {
            get { return _data; }
        }

        public byte[] getFunction
        {
            get { return _function; }
        }

        public Message(string message, Function func = Function.SIMP_CHAT_MSG)//Cambiar por ascii si los otros lo hacen por ascii.
        {
            ReplaceBytes(_ascp, System.Text.Encoding.GetEncoding(28591).GetBytes("ASCP"));

            byte[] intBytes = BitConverter.GetBytes(1);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            ReplaceBytes(_version, intBytes);

            _size = Convert.ToByte(message.Length);

            intBytes = BitConverter.GetBytes((char)func);
            ReplaceBytes(_function, intBytes);

            intBytes = BitConverter.GetBytes(0);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            ReplaceBytes(_state, intBytes);

            intBytes = BitConverter.GetBytes(0);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            ReplaceBytes(_id_session, intBytes);

            ReplaceBytes(_data, System.Text.Encoding.GetEncoding(28591).GetBytes(message), false);

            int offset = 0;
            FillBytesWith(_ascp, offset);
            offset = offset + _ascp.Length;
            FillBytesWith(_version, offset);
            offset = offset + _version.Length;
            completeBytes[offset] = _size;
            offset++;
            FillBytesWith(_function, offset);
            offset = offset + _function.Length;
            FillBytesWith(_state, offset);
            offset = offset + _state.Length;
            FillBytesWith(_id_session, offset);
            offset = offset + _id_session.Length;
            FillBytesWith(_data, offset);
            offset = offset + _data.Length;
        }

        private void ReplaceBytes(byte[] left, byte[] right, bool useDiff = true)
        {
            int diff = 0;
            if (useDiff)
                diff = left.Length - right.Length;
            for (int i = 0; i < right.Length; i++)
            {
                left[i + diff] = right[i];
            }
        }

        private void FillBytesWith(byte[] arr,int offset)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                completeBytes[i+offset] = arr[i];
            }
        }

        private void GetBytesFromComplete(byte[] arr, int offset)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = completeBytes[i + offset];
            }
        }

        public Message(byte[] complete)
        {
            if(complete.Length == 256)
                completeBytes = complete;
            else
                throw new FormatException("Message format is not correct");

            int offset = 0;
            GetBytesFromComplete(_ascp, offset);
            offset = offset + _ascp.Length;
            GetBytesFromComplete(_version, offset);
            offset = offset + _version.Length;
            _size = completeBytes[offset];
            offset++;
            GetBytesFromComplete(getFunction, offset);
            offset = offset + getFunction.Length;
            GetBytesFromComplete(_state, offset);
            offset = offset + _state.Length;
            GetBytesFromComplete(_id_session, offset);
            offset = offset + _id_session.Length;
            GetBytesFromComplete(_data, offset);
            offset = offset + _data.Length;
        }
    }
}
