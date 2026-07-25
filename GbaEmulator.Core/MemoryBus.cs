namespace GbaEmulator.Core;

public sealed class MemoryBus
{
    private readonly byte[] _rom;

    public MemoryBus(byte[] rom)
    {
        _rom = rom;
    }

    public byte Read8(uint address)
    {
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

}