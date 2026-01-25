namespace BoggleSolverConsole.Bits
{

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
