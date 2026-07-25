namespace GbaEmulator.Core;

public sealed class Arm7tdmiCpu
{
    private readonly MemoryBus _bus;
    private readonly uint[] _registers = new uint[16];

    public uint Cpsr {get; private set;}

    public uint Pc
    {
        get => _registers[15];
        private set => _registers[15] = value;
    }

    public Arm7tdmiCpu(MemoryBus bus)
    {
        _bus = bus;
        Pc = 0x08000000;
        Cpsr = 0x0000001F;
    }

    private static bool IsBranch(uint instruction)
    {
        return (instruction & 0x0E000000) == 0x0A000000;
    }

    private void ExecuteBranch(uint instruction)
    {
        int offset = (int)(instruction & 0x00FFFFFF);

        if ((offset & 0x00800000) != 0)
        {
            offset |= unchecked((int)0xFF000000);
        }

        offset <<= 2;

        Pc = (uint)((int)Pc + offset);
    }

    public uint Fetch32()
    {
        uint instruction = _bus.Read32(Pc);
        Pc += 4;

        return instruction;
    }

    public uint GetRegister(int index)
    {
        return _registers[index];
    }

    public void Step()
    {
        uint instruction = Fetch32();

        if (IsBranch(instruction))
        {
            ExecuteBranch(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported ARM instruction: 0x{instruction:X8}");
    }

}
