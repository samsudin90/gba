using GbaEmulator.Core;

if (args.Length != 1 && args.Length != 3)
{
    Console.WriteLine("Usage: GbaEmulator.Cli <path-to-rom.gba> [--bios <path-to-gba-bios.bin>]");
    return;
}

string romPath = args[0];
string? biosPath = null;

if (args.Length == 3)
{
    if (args[1] != "--bios")
    {
        Console.WriteLine("Usage: GbaEmulator.Cli <path-to-rom.gba> [--bios <path-to-gba-bios.bin>]");
        return;
    }

    biosPath = args[2];
}


if (!File.Exists(romPath))
{
    Console.WriteLine($"ROM file not found: {romPath}");
    return;
}

if (biosPath is not null && !File.Exists(biosPath))
{
    Console.WriteLine($"BIOS file not found: {biosPath}");
    return;
}

byte[] romBytes = File.ReadAllBytes(romPath);
byte[]? biosBytes = biosPath is null ? null : File.ReadAllBytes(biosPath);
GbaRomHeader header = GbaRomHeader.Parse(romBytes);

Console.WriteLine("GBA ROM Header");
Console.WriteLine($"Title: {header.GameTitle}");
Console.WriteLine($"Game Code: {header.GameCode}");
Console.WriteLine($"Maker Code: {header.MakerCode}");
Console.WriteLine($"Fixed Value 0x96: {header.HasValidFixedValue}");
Console.WriteLine($"Header Checksum: 0x{header.HeaderChecksum:X2}");
Console.WriteLine($"Checksum Valid: {header.HasValidHeaderChecksum}");

byte[] testRom =
[
    0x03, 0x00, 0xA0, 0xE3,
    0x01, 0x00, 0x50, 0xE2,
    0xFD, 0xFF, 0xFF, 0x1A
];

MemoryBus bus = new MemoryBus(romBytes, biosBytes);
Arm7tdmiCpu testCpu = new Arm7tdmiCpu(bus);

if (biosBytes is not null)
{
    Console.WriteLine($"BIOS Size: {biosBytes.Length} bytes");
} else
{
    Console.WriteLine("No BIOS provided, skipping BIOS execution.");
}

for (int i = 0; i < 6; i++)
{
    Console.WriteLine(
        $"Before step {i}: PC=0x{testCpu.Pc:X8}, R0={testCpu.GetRegister(0)}, Z={testCpu.ZeroFlagSet}");

    testCpu.Step();

    Console.WriteLine(
        $"After step {i}:  PC=0x{testCpu.Pc:X8}, R0={testCpu.GetRegister(0)}, Z={testCpu.ZeroFlagSet}");
}