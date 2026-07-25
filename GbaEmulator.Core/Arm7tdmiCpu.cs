namespace GbaEmulator.Core;

public sealed class Arm7tdmiCpu
{
    private readonly MemoryBus _bus;
    private readonly uint[] _registers = new uint[16];

    public uint Cpsr {get; private set;}

    private const uint NegativeFlag = 1u << 31;
    private const uint ZeroFlag = 1u << 30;
    private const uint CarryFlag = 1u << 29;
    private const uint OverflowFlag = 1u << 28;

    public bool NegativeFlagSet => (Cpsr & NegativeFlag) != 0;
    public bool ZeroFlagSet => (Cpsr & ZeroFlag) != 0;
    public bool CarryFlagSet => (Cpsr & CarryFlag) != 0;
    public bool OverflowFlagSet => (Cpsr & OverflowFlag) != 0;

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

    public void SetZeroFlagForTesting(bool value)
    {
        if (value)
        {
            Cpsr |= ZeroFlag;
        }
        else
        {
            Cpsr &= ~ZeroFlag;
        }
    }

    private void ExecuteBranch(uint instruction)
    {
        int offset = (int)(instruction & 0x00FFFFFF);

        if ((offset & 0x00800000) != 0)
        {
            offset |= unchecked((int)0xFF000000);
        }

        offset <<= 2;

        Pc = (uint)((int)Pc + 4 + offset);
    }

    private bool ShouldExecute(uint instruction)
    {
        uint condition = instruction >> 28;

        return condition switch
        {
            0x0 => ZeroFlagSet,
            0x1 => !ZeroFlagSet,
            0x2 => CarryFlagSet,
            0x3 => !CarryFlagSet,
            0x4 => NegativeFlagSet,
            0x5 => !NegativeFlagSet,
            0x6 => OverflowFlagSet,
            0x7 => !OverflowFlagSet,
            0x8 => CarryFlagSet && !ZeroFlagSet,
            0x9 => !CarryFlagSet || ZeroFlagSet,
            0xA => NegativeFlagSet == OverflowFlagSet,
            0xB => NegativeFlagSet != OverflowFlagSet,
            0xC => !ZeroFlagSet && NegativeFlagSet == OverflowFlagSet,
            0xD => ZeroFlagSet || NegativeFlagSet != OverflowFlagSet,
            0xE => true,
            _ => false
        };
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

        if (!ShouldExecute(instruction))
        {
            return;
        }

        if (IsBranch(instruction))
        {
            ExecuteBranch(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported ARM instruction: 0x{instruction:X8}");
    }

}
