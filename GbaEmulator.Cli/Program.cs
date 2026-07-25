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
Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

Console.WriteLine($"PC before step: 0x{cpu.Pc:X8}");
cpu.Step();
Console.WriteLine($"PC after step:  0x{cpu.Pc:X8}");