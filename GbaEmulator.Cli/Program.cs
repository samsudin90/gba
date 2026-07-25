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

byte[] testRom =
[
    0x0A, 0x10, 0xA0, 0xE3,
    0x05, 0x00, 0x81, 0xE2
];

MemoryBus testBus = new MemoryBus(testRom);
Arm7tdmiCpu testCpu = new Arm7tdmiCpu(testBus);

testCpu.Step();
testCpu.Step();

Console.WriteLine($"R1 after MOV: {testCpu.GetRegister(1)}");
Console.WriteLine($"R0 after ADD: {testCpu.GetRegister(0)}");