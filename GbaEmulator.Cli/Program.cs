using GbaEmulator.Core;

if (args.Length < 2)
{
    PrintUsage();
    return;
}

string command = args[0];
string romPath = args[1];
int stepCount = args.Length >= 3 ? int.Parse(args[2]) : 10;

if (!File.Exists(romPath))
{
    Console.WriteLine($"ROM file not found: {romPath}");
    return;
}

byte[] romBytes = File.ReadAllBytes(romPath);


switch (command)
{
    case "info":
        RunInfoCommand(romBytes);
        break;

    case "step":
        RunStepCommand(romBytes, stepCount);
        break;

    default:
        PrintUsage();
        break;
}

static void RunInfoCommand(byte[] romBytes)
{
    GbaRomHeader header = GbaRomHeader.Parse(romBytes);

    Console.WriteLine("GBA ROM Header");
    Console.WriteLine($"Title: {header.GameTitle}");
    Console.WriteLine($"Game Code: {header.GameCode}");
    Console.WriteLine($"Maker Code: {header.MakerCode}");
    Console.WriteLine($"Fixed Value 0x96: {header.HasValidFixedValue}");
    Console.WriteLine($"Header Checksum: 0x{header.HeaderChecksum:X2}");
    Console.WriteLine($"Checksum Valid: {header.HasValidHeaderChecksum}");
}

static void RunStepCommand(byte[] romBytes, int stepCount)
{
    MemoryBus bus = new MemoryBus(romBytes);
    Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

    for (int i = 0; i < stepCount; i++)
    {
        Console.WriteLine($"Before step {i}: PC=0x{cpu.Pc:X8}");
        cpu.Step();
        Console.WriteLine($"After step {i}:  PC=0x{cpu.Pc:X8}");
    }
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  GbaEmulator.Cli info <path-to-rom.gba>");
    Console.WriteLine("  GbaEmulator.Cli step <path-to-rom.gba>");
}