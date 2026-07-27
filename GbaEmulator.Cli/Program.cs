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

    // for (int i = 0; i < stepCount; i++)
    // {
    //     Console.WriteLine($"Before step {i}: PC=0x{cpu.Pc:X8}");
    //     cpu.Step();
    //     Console.WriteLine($"After step {i}:  PC=0x{cpu.Pc:X8}");
    //     uint instruction = bus.Read32(cpu.Pc);

    //     Console.WriteLine(
    //         $"Before step {i}: PC=0x{cpu.Pc:X8}, Instruction=0x{instruction:X8}, " +
    //         $"R0=0x{cpu.GetRegister(0):X8}, R1=0x{cpu.GetRegister(1):X8}, " +
    //         $"SP=0x{cpu.GetRegister(13):X8}, LR=0x{cpu.GetRegister(14):X8}, " +
    //         $"CPSR=0x{cpu.Cpsr:X8}");
    // }

    Dictionary<uint, int> pcHits = new();

    for (int i = 0; i < stepCount; i++)
    {
        uint pc = cpu.Pc;

        if (!pcHits.TryAdd(pc, 1))
        {
            pcHits[pc]++;
        }

        cpu.Step();
    }

    Console.WriteLine("Top PC hits:");

    foreach (var item in pcHits.OrderByDescending(x => x.Value).Take(10))
    {
        Console.WriteLine($"PC=0x{item.Key:X8}, Hits={item.Value}");
    }

    Console.WriteLine($"Final PC: 0x{cpu.Pc:X8}");
    Console.WriteLine($"Final CPSR: 0x{cpu.Cpsr:X8}");
    Console.WriteLine(
        $"R0=0x{cpu.GetRegister(0):X8}, R1=0x{cpu.GetRegister(1):X8}, " +
        $"R2=0x{cpu.GetRegister(2):X8}, R3=0x{cpu.GetRegister(3):X8}");
    Console.WriteLine(
        $"R4=0x{cpu.GetRegister(4):X8}, R5=0x{cpu.GetRegister(5):X8}, " +
        $"R6=0x{cpu.GetRegister(6):X8}, R7=0x{cpu.GetRegister(7):X8}");
    Console.WriteLine($"SP=0x{cpu.GetRegister(13):X8}, LR=0x{cpu.GetRegister(14):X8}");

    Console.WriteLine("Hot PC instructions:");

    foreach (var item in pcHits.OrderByDescending(x => x.Value).Take(10).OrderBy(x => x.Key))
    {
        ushort thumbInstruction = bus.Read16(item.Key);
        Console.WriteLine($"PC=0x{item.Key:X8}, Thumb=0x{thumbInstruction:X4}, Hits={item.Value}");
    }
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  GbaEmulator.Cli info <path-to-rom.gba>");
    Console.WriteLine("  GbaEmulator.Cli step <path-to-rom.gba>");
}
