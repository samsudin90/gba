namespace GbaEmulator.Core;

public sealed class MemoryBus
{
    private readonly byte[] _rom;

    private readonly byte[] _ewram = new byte[256 * 1024];

    public MemoryBus(byte[] rom)
    {
        _rom = rom;
    }

    public byte Read8(uint address)
    {
        if (address >= 0x02000000 && address <= 0x0203FFFF)
        {
            uint ewramOffset = address - 0x02000000;
            return _ewram[(int)ewramOffset];
        }

        if (address >= 0x08000000 && address <= 0x09FFFFFF)
        {
            uint romOffset = address - 0x08000000;
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
        if (address >= 0x02000000 && address <= 0x0203FFFF)
        {
            uint ewramOffset = address - 0x02000000;
            _ewram[(int)ewramOffset] = value;
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