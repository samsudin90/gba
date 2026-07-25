using GbaEmulator.Core;

if (args.Length != 1)
{
    Console.WriteLine("Usage: GbaEmulator.Cli <path-to-rom.gba>");
    return;
}

string romPath = args[0];

if (!File.Exists(romPath))
{
    Console.WriteLine($"ROM file not found: {romPath}");
    return;
}

byte[] romBytes = File.ReadAllBytes(romPath);
GbaRomHeader header = GbaRomHeader.Parse(romBytes);

Console.WriteLine("GBA ROM Header");
Console.WriteLine($"Title: {header.GameTitle}");
Console.WriteLine($"Game Code: {header.GameCode}");
Console.WriteLine($"Maker Code: {header.MakerCode}");
Console.WriteLine($"Fixed Value 0x96: {header.HasValidFixedValue}");
Console.WriteLine($"Header Checksum: 0x{header.HeaderChecksum:X2}");
Console.WriteLine($"Checksum Valid: {header.HasValidHeaderChecksum}");

MemoryBus bus = new MemoryBus(romBytes);

byte b8 = bus.Read8(0x080000A0);
ushort b16 = bus.Read16(0x080000A0);
uint b32 = bus.Read32(0x080000A0);

Console.WriteLine($"Read8 title start:  0x{b8:X2}");
Console.WriteLine($"Read16 title start: 0x{b16:X4}");
Console.WriteLine($"Read32 title start: 0x{b32:X8}");