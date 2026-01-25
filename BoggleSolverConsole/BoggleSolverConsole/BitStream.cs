namespace BoggleSolverConsole
{
    public class BitWriter
    {
        private readonly Stream _stream;
        private ulong _buffer;
        private int _bitsInBuffer;

        public long BitsWritten { get; private set; }

        public BitWriter(Stream stream)
        {
            _stream = stream;
        }

        public void WriteBits(uint value, int bitCount)
        {
            if (bitCount > 32) throw new ArgumentException("Cannot write more than 32 bits at once");

            _buffer |= (ulong)value << _bitsInBuffer;
            _bitsInBuffer += bitCount;
            BitsWritten += bitCount;

            while (_bitsInBuffer >= 8)
            {
                _stream.WriteByte((byte)_buffer);
                _buffer >>= 8;
                _bitsInBuffer -= 8;
            }
        }

        public void WriteBit(bool value)
        {
            WriteBits(value ? 1u : 0u, 1);
        }

        public void Flush()
        {
            if (_bitsInBuffer > 0)
            {
                _stream.WriteByte((byte)_buffer);
                _bitsInBuffer = 0;
                _buffer = 0;
            }
        }
    }

    public class BitReader
    {
        private readonly Stream _stream;
        private ulong _buffer;
        private int _bitsInBuffer;

        public long BitsRead { get; private set; }

        public BitReader(Stream stream)
        {
            _stream = stream;
        }

        public uint ReadBits(int bitCount)
        {
            if (bitCount > 32) throw new ArgumentException("Cannot read more than 32 bits at once");

            while (_bitsInBuffer < bitCount)
            {
                int b = _stream.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                _buffer |= (ulong)b << _bitsInBuffer;
                _bitsInBuffer += 8;
            }

            uint result = (uint)(_buffer & ((1UL << bitCount) - 1));
            _buffer >>= bitCount;
            _bitsInBuffer -= bitCount;
            BitsRead += bitCount;
            return result;
        }

        public bool ReadBit()
        {
            return ReadBits(1) == 1;
        }
    }
}
