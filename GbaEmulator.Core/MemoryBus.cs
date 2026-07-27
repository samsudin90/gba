namespace GbaEmulator.Core;

public sealed class MemoryBus
{
    private const uint EwramStart = 0x02000000;
    private const uint EwramEnd = 0x02FFFFFF;
    private const uint IwramStart = 0x03000000;
    private const uint IwramEnd = 0x03FFFFFF;
    private const uint RomStart = 0x08000000;
    private const uint RomEnd = 0x09FFFFFF;
    private const uint PaletteRamStart = 0x05000000;
    private const uint PaletteRamEnd = 0x05FFFFFF;
    private const uint VramStart = 0x06000000;
    private const uint VramEnd = 0x06FFFFFF;
    private const uint OamStart = 0x07000000;
    private const uint OamEnd = 0x07FFFFFF;
    private const int EwramSize = 256 * 1024;
    private const int IwramSize = 32 * 1024;
    private const int PaletteRamSize = 1024;
    private const int VramSize = 96 * 1024;
    private const int OamSize = 1024;

    private ushort _keyInput = 0x03FF;
    private const uint IoStart = 0x04000000;
    private const uint IoEnd = 0x040003FE;
    private const int IoSize = 0x400;
    private const int ScanlineStepCycles = 64;
    private const int ScanlineCount = 228;
    private const int FirstVBlankScanline = 160;
    private const uint DispStat = 0x04000004;
    private const uint VCount = 0x04000006;
    private const uint KeyInput = 0x04000130;
    private const uint InterruptFlag = 0x04000202;
    private readonly byte[] _ioRegisters = new byte[IoSize];
    private byte _vcount;
    private int _scanlineCycles;
    private static readonly uint[] DmaBaseAddresses =
    [
        0x040000B0,
        0x040000BC,
        0x040000C8,
        0x040000D4
    ];

    private const uint BiosStart = 0x00000000;
    private const uint BiosEnd = 0x00003FFF;
    private const int BiosSize = 16 * 1024;

    private readonly byte[] _rom;
    private readonly byte[]? _bios;

    private readonly byte[] _ewram = new byte[EwramSize];

    private readonly byte[] _iwram = new byte[IwramSize];
    private readonly byte[] _paletteRam = new byte[PaletteRamSize];
    private readonly byte[] _vram = new byte[VramSize];
    private readonly byte[] _oam = new byte[OamSize];

    public MemoryBus(byte[] rom, byte[]? bios = null)
    {
        if (bios is not null && bios.Length != BiosSize)
        {
            throw new ArgumentException("GBA BIOS must be exactly 16 KiB.", nameof(bios));
        }

        _rom = rom;
        _bios = bios;
    }

    private static bool IsInRange(uint address, uint start, uint end)
    {
        return address >= start && address <= end;
    }

    private static int MirrorOffset(uint address, uint start, int size)
    {
        return (int)((address - start) % size);
    }

    private static int IoOffset(uint address)
    {
        return (int)(address - IoStart);
    }

    private ushort ReadIo16(uint address)
    {
        int offset = IoOffset(address);
        return (ushort)(_ioRegisters[offset] | (_ioRegisters[offset + 1] << 8));
    }

    private uint ReadIo32(uint address)
    {
        ushort low = ReadIo16(address);
        ushort high = ReadIo16(address + 2);
        return (uint)(low | (high << 16));
    }

    public void Tick(int cycles)
    {
        _scanlineCycles += cycles;

        while (_scanlineCycles >= ScanlineStepCycles)
        {
            _scanlineCycles -= ScanlineStepCycles;
            AdvanceScanline();
        }
    }

    private void AdvanceScanline()
    {
        _vcount = (byte)((_vcount + 1) % ScanlineCount);
        bool inVBlank = _vcount >= FirstVBlankScanline;
        int dispStatOffset = IoOffset(DispStat);

        if (inVBlank)
        {
            _ioRegisters[dispStatOffset] |= 1;

            if (_vcount == FirstVBlankScanline)
            {
                _ioRegisters[IoOffset(InterruptFlag)] |= 1;
            }
        }
        else
        {
            _ioRegisters[dispStatOffset] &= 0xFE;
        }
    }

    public void SetButtonState(GbaButton button, bool pressed)
    {
        ushort mask = (ushort)button;

        if (pressed)
        {
            _keyInput &= (ushort)~mask;
        }
        else
        {
            _keyInput |= mask;
        }
    }

    public byte Read8(uint address)
    {

        if (IsInRange(address, BiosStart, BiosEnd))
        {
            if (_bios is null)
            {
                return 0x00;
            }

            uint biosOffset = address - BiosStart;
            return _bios[(int)biosOffset];
        }

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

        if (IsInRange(address, PaletteRamStart, PaletteRamEnd))
        {
            int paletteOffset = MirrorOffset(address, PaletteRamStart, PaletteRamSize);
            return _paletteRam[paletteOffset];
        }

        if (IsInRange(address, VramStart, VramEnd))
        {
            int vramOffset = MirrorOffset(address, VramStart, VramSize);
            return _vram[vramOffset];
        }

        if (IsInRange(address, OamStart, OamEnd))
        {
            int oamOffset = MirrorOffset(address, OamStart, OamSize);
            return _oam[oamOffset];
        }

        if (IsInRange(address, IoStart, IoEnd))
        {
            if (address == VCount)
            {
                return _vcount;
            }

            if (address == VCount + 1)
            {
                return 0x00;
            }

            if (address == KeyInput)
            {
                return (byte)(_keyInput & 0xFF);
            }

            if (address == KeyInput + 1)
            {
                return (byte)(_keyInput >> 8);
            }

            return _ioRegisters[IoOffset(address)];
        }

        if (IsInRange(address, RomStart, RomEnd))
        {
            uint romOffset = address - RomStart;
            if (romOffset < _rom.Length)
            {
                return _rom[(int)romOffset];
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

        if (IsInRange(address, PaletteRamStart, PaletteRamEnd))
        {
            int paletteOffset = MirrorOffset(address, PaletteRamStart, PaletteRamSize);
            _paletteRam[paletteOffset] = value;
            return;
        }

        if (IsInRange(address, VramStart, VramEnd))
        {
            int vramOffset = MirrorOffset(address, VramStart, VramSize);
            _vram[vramOffset] = value;
            return;
        }

        if (IsInRange(address, OamStart, OamEnd))
        {
            int oamOffset = MirrorOffset(address, OamStart, OamSize);
            _oam[oamOffset] = value;
            return;
        }

        if (IsInRange(address, IoStart, IoEnd))
        {
            if (address == KeyInput || address == KeyInput + 1 || address == VCount || address == VCount + 1)
            {
                return;
            }

            if (address == InterruptFlag || address == InterruptFlag + 1)
            {
                int offset = IoOffset(address);
                _ioRegisters[offset] = (byte)(_ioRegisters[offset] & ~value);
                return;
            }

            _ioRegisters[IoOffset(address)] = value;
            TryRunDma(address);
            return;
        }
    }

    private void TryRunDma(uint writtenAddress)
    {
        for (int channel = 0; channel < DmaBaseAddresses.Length; channel++)
        {
            uint dmaBase = DmaBaseAddresses[channel];
            uint controlHighAddress = dmaBase + 0x0B;

            if (writtenAddress == controlHighAddress && (_ioRegisters[IoOffset(controlHighAddress)] & 0x80) != 0)
            {
                RunDma(channel, dmaBase);
                return;
            }
        }
    }

    private void RunDma(int channel, uint dmaBase)
    {
        uint source = ReadIo32(dmaBase);
        uint destination = ReadIo32(dmaBase + 4);
        int count = ReadIo16(dmaBase + 8);
        ushort control = ReadIo16(dmaBase + 10);
        bool transfer32Bit = (control & (1 << 10)) != 0;

        if (count == 0)
        {
            count = channel == 3 ? 0x10000 : 0x4000;
        }

        int unitSize = transfer32Bit ? 4 : 2;

        for (int i = 0; i < count; i++)
        {
            if (transfer32Bit)
            {
                Write32(destination, Read32(source));
            }
            else
            {
                Write16(destination, Read16(source));
            }

            source += (uint)unitSize;
            destination += (uint)unitSize;
        }

        uint controlHighAddress = dmaBase + 0x0B;
        _ioRegisters[IoOffset(controlHighAddress)] &= 0x7F;
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

    public void ClearEwram()
    {
        Array.Clear(_ewram);
    }

    public void ClearIwram()
    {
        Array.Clear(_iwram, 0, IwramSize - 0x200);
    }

}
