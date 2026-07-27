using GbaEmulator.Core;

if (args.Length < 2)
{
    PrintUsage();
    return;
}

string command = args[0];
string romPath = args[1];

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
        int stepCount = args.Length >= 3 ? int.Parse(args[2]) : 10;
        RunStepCommand(romBytes, biosBytes: null, skipBios: true, stepCount);
        break;

    case "bios-step":
        int biosStepCount = args.Length >= 3 ? int.Parse(args[2]) : 10;
        string biosPath = args.Length >= 4 ? args[3] : "bios/bios.bin";

        if (!File.Exists(biosPath))
        {
            Console.WriteLine($"BIOS file not found: {biosPath}");
            return;
        }

        byte[] biosBytes = File.ReadAllBytes(biosPath);
        RunStepCommand(romBytes, biosBytes, skipBios: false, biosStepCount);
        break;

    case "bios-logo":
        string logoOutputPath = args.Length >= 3 ? args[2] : "bios-logo.bmp";
        string logoBiosPath = args.Length >= 4 ? args[3] : "bios/bios.bin";

        if (!File.Exists(logoBiosPath))
        {
            Console.WriteLine($"BIOS file not found: {logoBiosPath}");
            return;
        }

        _ = new MemoryBus(romBytes, File.ReadAllBytes(logoBiosPath));
        WriteMinimalBootLogo(logoOutputPath);
        Console.WriteLine($"Wrote minimal BIOS splash: {logoOutputPath}");
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

static void RunStepCommand(byte[] romBytes, byte[]? biosBytes, bool skipBios, int stepCount)
{
    MemoryBus bus = new MemoryBus(romBytes, biosBytes);
    Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus, skipBios);

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
        uint armInstruction = bus.Read32(item.Key);
        Console.WriteLine($"PC=0x{item.Key:X8}, Thumb=0x{thumbInstruction:X4}, ARM=0x{armInstruction:X8}, Hits={item.Value}");
    }
}

static void WriteMinimalBootLogo(string outputPath)
{
    BmpFrameBuffer frameBuffer = new BmpFrameBuffer(240, 160);
    frameBuffer.Clear(238, 241, 246);

    frameBuffer.FillRect(0, 0, 240, 160, 238, 241, 246);
    frameBuffer.FillRect(0, 118, 240, 42, 210, 218, 232);
    frameBuffer.FillRect(18, 32, 204, 74, 42, 58, 92);
    frameBuffer.FillRect(24, 38, 192, 62, 248, 250, 252);

    DrawBlockG(frameBuffer, 45, 55);
    DrawBlockB(frameBuffer, 98, 55);
    DrawBlockA(frameBuffer, 151, 55);

    frameBuffer.FillRect(52, 124, 136, 5, 42, 58, 92);
    frameBuffer.FillRect(70, 134, 100, 4, 92, 111, 148);
    frameBuffer.SaveBmp(outputPath);
}

static void DrawBlockG(BmpFrameBuffer frameBuffer, int x, int y)
{
    frameBuffer.FillRect(x, y, 34, 8, 35, 70, 170);
    frameBuffer.FillRect(x, y, 8, 42, 35, 70, 170);
    frameBuffer.FillRect(x, y + 34, 34, 8, 35, 70, 170);
    frameBuffer.FillRect(x + 22, y + 20, 12, 8, 35, 70, 170);
    frameBuffer.FillRect(x + 26, y + 20, 8, 22, 35, 70, 170);
}

static void DrawBlockB(BmpFrameBuffer frameBuffer, int x, int y)
{
    frameBuffer.FillRect(x, y, 8, 42, 35, 70, 170);
    frameBuffer.FillRect(x, y, 28, 8, 35, 70, 170);
    frameBuffer.FillRect(x, y + 17, 30, 8, 35, 70, 170);
    frameBuffer.FillRect(x, y + 34, 28, 8, 35, 70, 170);
    frameBuffer.FillRect(x + 26, y + 6, 8, 13, 35, 70, 170);
    frameBuffer.FillRect(x + 26, y + 23, 8, 13, 35, 70, 170);
}

static void DrawBlockA(BmpFrameBuffer frameBuffer, int x, int y)
{
    frameBuffer.FillRect(x, y + 8, 8, 34, 35, 70, 170);
    frameBuffer.FillRect(x + 26, y + 8, 8, 34, 35, 70, 170);
    frameBuffer.FillRect(x + 6, y, 22, 8, 35, 70, 170);
    frameBuffer.FillRect(x, y + 18, 34, 8, 35, 70, 170);
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  GbaEmulator.Cli info <path-to-rom.gba>");
    Console.WriteLine("  GbaEmulator.Cli step <path-to-rom.gba> [step-count]");
    Console.WriteLine("  GbaEmulator.Cli bios-step <path-to-rom.gba> [step-count] [path-to-bios.bin]");
    Console.WriteLine("  GbaEmulator.Cli bios-logo <path-to-rom.gba> [output.bmp] [path-to-bios.bin]");
}
