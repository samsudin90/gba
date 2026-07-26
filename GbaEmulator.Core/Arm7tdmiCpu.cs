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

    private static bool IsSingleDataTransfer(uint instruction)
    {
        return (instruction & 0x0C000000) == 0x04000000;
    }

    public void SetRegisterForTesting(int index, uint value)
    {
        _registers[index] = value;
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

    private void SetCarryFlagForAddition(uint left, uint right, uint result)
    {
        if (result < left)
        {
            Cpsr |= CarryFlag;
        }
        else
        {
            Cpsr &= ~CarryFlag;
        }
    }

    private void SetCarryFlagForSubtraction(uint left, uint right)
    {
        if (left >= right)
        {
            Cpsr |= CarryFlag;
        }
        else
        {
            Cpsr &= ~CarryFlag;
        }
    }

    private void SetOverflowFlagForAddition(uint left, uint right, uint result)
    {
        bool leftNegative = (left & 0x80000000) != 0;
        bool rightNegative = (right & 0x80000000) != 0;
        bool resultNegative = (result & 0x80000000) != 0;

        if (leftNegative == rightNegative && leftNegative != resultNegative)
        {
            Cpsr |= OverflowFlag;
        }
        else
        {
            Cpsr &= ~OverflowFlag;
        }
    }

    private void SetOverflowFlagForSubtraction(uint left, uint right, uint result)
    {
        bool leftNegative = (left & 0x80000000) != 0;
        bool rightNegative = (right & 0x80000000) != 0;
        bool resultNegative = (result & 0x80000000) != 0;

        if (leftNegative != rightNegative && leftNegative != resultNegative)
        {
            Cpsr |= OverflowFlag;
        }
        else
        {
            Cpsr &= ~OverflowFlag;
        }
    }

    private static bool IsDataProcessingImmediate(uint instruction)
    {
        return (instruction & 0x0E000000) == 0x02000000;
    }

    private static uint RotateRight(uint value, int amount)
    {
        if (amount == 0)
        {
            return value;
        }

        return (value >> amount) | (value << (32 - amount));
    }

    private void ExecuteMovImmediate(uint instruction)
    {
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);

        _registers[destinationRegister] = operand;

        if (ShouldUpdateFlags(instruction))
        {
            SetNegativeAndZeroFlags(operand);
        }
    }

    private static uint DecodeImmediateOperand(uint instruction)
    {
        uint immediate = instruction & 0xFF;
        int rotate = (int)((instruction >> 8) & 0xF) * 2;

        return RotateRight(immediate, rotate);
    }

    private void ExecuteAddImmediate(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);
        uint left = _registers[sourceRegister];
        uint result = left + operand;

        _registers[destinationRegister] = result;

        if (ShouldUpdateFlags(instruction))
        {
            SetNegativeAndZeroFlags(result);
            SetCarryFlagForAddition(left, operand, result);
            SetOverflowFlagForAddition(left, operand, result);
        }
    }

    private void ExecuteSubImmediate(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);
        uint left = _registers[sourceRegister];
        uint result = left - operand;

        _registers[destinationRegister] = result;

        if (ShouldUpdateFlags(instruction))
        {
            SetNegativeAndZeroFlags(result);
            SetCarryFlagForSubtraction(left, operand);
            SetOverflowFlagForSubtraction(left, operand, result);
        }
    }

    private void ExecuteCmpImmediate(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);
        uint left = _registers[sourceRegister];
        uint result = left - operand;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForSubtraction(left, operand);
        SetOverflowFlagForSubtraction(left, operand, result);
    }

    private void ExecuteDataProcessingImmediate(uint instruction)
    {
        uint opcode = (instruction >> 21) & 0xF;

        if (opcode == 0xA)
        {
            ExecuteCmpImmediate(instruction);
            return;
        }
        if (opcode == 0x2)
        {
            ExecuteSubImmediate(instruction);
            return;
        }

        if (opcode == 0x4)
        {
            ExecuteAddImmediate(instruction);
            return;
        }

        if (opcode == 0xD)
        {
            ExecuteMovImmediate(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported data processing immediate opcode: 0x{opcode:X}");
    }

    private void ExecuteSingleDataTransfer(uint instruction)
    {
        bool isImmediateOffset = (instruction & (1u << 25)) == 0;
        bool isPreIndexed = (instruction & (1u << 24)) != 0;
        bool addOffset = (instruction & (1u << 23)) != 0;
        bool isByteTransfer = (instruction & (1u << 22)) != 0;
        bool writeBack = (instruction & (1u << 21)) != 0;
        bool isLoad = (instruction & (1u << 20)) != 0;

        if (!isImmediateOffset || !isPreIndexed || isByteTransfer || writeBack || !isLoad)
        {
            throw new NotSupportedException($"Unsupported single data transfer: 0x{instruction:X8}");
        }

        int baseRegister = (int)((instruction >> 16) & 0xF);
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        uint offset = instruction & 0xFFF;

        uint address = _registers[baseRegister];

        if (addOffset)
        {
            address += offset;
        }
        else
        {
            address -= offset;
        }

        _registers[destinationRegister] = _bus.Read32(address);
    }

    private static bool ShouldUpdateFlags(uint instruction)
    {
        return (instruction & (1u << 20)) != 0;
    }

    private void SetNegativeAndZeroFlags(uint result)
    {
        if ((result & 0x80000000) != 0)
        {
            Cpsr |= NegativeFlag;
        }
        else
        {
            Cpsr &= ~NegativeFlag;
        }

        if (result == 0)
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

        if (IsDataProcessingImmediate(instruction))
        {
            ExecuteDataProcessingImmediate(instruction);
            return;
        }

        if (IsSingleDataTransfer(instruction))
        {
            ExecuteSingleDataTransfer(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported ARM instruction: 0x{instruction:X8}");
    }

}
