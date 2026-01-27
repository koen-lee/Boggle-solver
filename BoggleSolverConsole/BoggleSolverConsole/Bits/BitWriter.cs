namespace BoggleSolverConsole.Bits
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

        public void WritePrefix(BitPrefix prefix)
        {
            WriteBits(prefix.Bits, prefix.Length);
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
}
