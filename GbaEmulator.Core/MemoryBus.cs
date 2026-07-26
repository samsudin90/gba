namespace GbaEmulator.Core;

public sealed class MemoryBus
{
    private const uint EwramStart = 0x02000000;
    private const uint EwramEnd = 0x02FFFFFF;
    private const uint IwramStart = 0x03000000;
    private const uint IwramEnd = 0x03FFFFFF;
    private const uint RomStart = 0x08000000;
    private const uint RomEnd = 0x09FFFFFF;
    private const int EwramSize = 256 * 1024;
    private const int IwramSize = 32 * 1024;

    private readonly byte[] _rom;

    private readonly byte[] _ewram = new byte[EwramSize];

    private readonly byte[] _iwram = new byte[IwramSize];

    public MemoryBus(byte[] rom)
    {
        _rom = rom;
    }

    private static bool IsInRange(uint address, uint start, uint end)
    {
        return address >= start && address <= end;
    }

    private static int MirrorOffset(uint address, uint start, int size)
    {
        return (int)((address - start) % size);
    }

    public byte Read8(uint address)
    {
        if (IsInRange(address, EwramStart, EwramEnd))
        {
            int ewramOffset = MirrorOffset(address, EwramStart, EwramSize);
            return _ewram[ewramOffset];
        }

        if (IsInRange(address, IwramStart, IwramEnd))
        {
            int iwramOffset = MirrorOffset(address, IwramStart, IwramSize);
            return _iwram[iwramOffset];
        }

        if (IsInRange(address, RomStart, RomEnd))
        {
            uint romOffset = address - RomStart;
            if (romOffset < _rom.Length)
            {
                return _rom[romOffset];
            }
            return 0xFF;
        }
        return 0x00;
    }

    public ushort Read16(uint address)
    {
        byte low = Read8(address);
        byte high = Read8(address + 1);

        return (ushort)(low | (high << 8));
    }

    public uint Read32(uint address)
    {
        ushort low = Read16(address);
        ushort high = Read16(address + 2);
        return (uint)(low | (high << 16));
    }

    public void Write8(uint address, byte value)
    {
        if (IsInRange(address, EwramStart, EwramEnd))
        {
            int ewramOffset = MirrorOffset(address, EwramStart, EwramSize);
            _ewram[ewramOffset] = value;
            return;
        }

        if (IsInRange(address, IwramStart, IwramEnd))
        {
            int iwramOffset = MirrorOffset(address, IwramStart, IwramSize);
            _iwram[iwramOffset] = value;
            return;
        }
    }

    public void Write16(uint address, ushort value)
    {
        Write8(address, (byte)(value & 0xFF));
        Write8(address + 1, (byte)(value >> 8));
    }

    public void Write32(uint address, uint value)
    {
        Write16(address, (ushort)(value & 0xFFFF));
        Write16(address + 2, (ushort)(value >> 16));
    }

}