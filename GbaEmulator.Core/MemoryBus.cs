namespace GbaEmulator.Core;

public sealed class MemoryBus
{
    private const uint EwramStart = 0x02000000;
    private const uint EwramEnd = 0x0203FFFF;
    private const uint IwramStart = 0x03000000;
    private const uint IwramEnd = 0x03007FFF;
    private const uint RomStart = 0x08000000;
    private const uint RomEnd = 0x09FFFFFF;

    private readonly byte[] _rom;

    private readonly byte[] _ewram = new byte[256 * 1024];

    private readonly byte[] _iwram = new byte[32 * 1024];

    public MemoryBus(byte[] rom)
    {
        _rom = rom;
    }

    private static bool IsInRange(uint address, uint start, uint end)
    {
        return address >= start && address <= end;
    }

    public byte Read8(uint address)
    {
        if (IsInRange(address, EwramStart, EwramEnd))
        {
            uint ewramOffset = address - EwramStart;
            return _ewram[(int)ewramOffset];
        }

        if (IsInRange(address, IwramStart, IwramEnd))
        {
            uint iwramOffset = address - IwramStart;
            return _iwram[(int)iwramOffset];
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
            uint ewramOffset = address - EwramStart;
            _ewram[(int)ewramOffset] = value;
            return;
        }

        if (IsInRange(address, IwramStart, IwramEnd))
        {
            uint iwramOffset = address - IwramStart;
            _iwram[(int)iwramOffset] = value;
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