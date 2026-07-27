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
    private const uint ThumbStateFlag = 1u << 5;
    private const uint UserMode = 0x10;
    private const uint IrqMode = 0x12;
    private const uint SupervisorMode = 0x13;
    private const uint SystemMode = 0x1F;
    private const uint ModeMask = 0x1F;

    public bool NegativeFlagSet => (Cpsr & NegativeFlag) != 0;
    public bool ZeroFlagSet => (Cpsr & ZeroFlag) != 0;
    public bool CarryFlagSet => (Cpsr & CarryFlag) != 0;
    public bool OverflowFlagSet => (Cpsr & OverflowFlag) != 0;
    public bool ThumbState => (Cpsr & ThumbStateFlag) != 0;
    private uint CurrentMode => Cpsr & ModeMask;

    private uint _userSystemR13;
    private uint _userSystemR14;
    private uint _irqR13;
    private uint _irqR14;
    private uint _supervisorR13;
    private uint _supervisorR14;

    public uint Pc
    {
        get => _registers[15];
        private set => _registers[15] = value;
    }

    public Arm7tdmiCpu(MemoryBus bus, bool skipBios = true)
    {
        _bus = bus;

        if (skipBios)
        {
            Cpsr = SystemMode;
            LoadBankedRegisters();
            Pc = 0x08000000;
        }
        else
        {
            Cpsr = 0x000000D3;
            LoadBankedRegisters();
            Pc = 0x00000000;
        }
    }

    private static int CountBits(byte value)
    {
        int count = 0;

        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    public void SetThumbStateForTesting(bool value)
    {
        if (value)
        {
            Cpsr |= ThumbStateFlag;
        }
        else
        {
            Cpsr &= ~ThumbStateFlag;
        }
    }

    private void SaveBankedRegisters()
    {
        switch (CurrentMode)
        {
            case UserMode:
            case SystemMode:
                _userSystemR13 = _registers[13];
                _userSystemR14 = _registers[14];
                break;

            case IrqMode:
                _irqR13 = _registers[13];
                _irqR14 = _registers[14];
                break;

            case SupervisorMode:
                _supervisorR13 = _registers[13];
                _supervisorR14 = _registers[14];
                break;
        }
    }

    private void LoadBankedRegisters()
    {
        switch (CurrentMode)
        {
            case UserMode:
            case SystemMode:
                _registers[13] = _userSystemR13;
                _registers[14] = _userSystemR14;
                break;

            case IrqMode:
                _registers[13] = _irqR13;
                _registers[14] = _irqR14;
                break;

            case SupervisorMode:
                _registers[13] = _supervisorR13;
                _registers[14] = _supervisorR14;
                break;
        }
    }

    private void SetCpsr(uint value)
    {
        uint oldMode = CurrentMode;
        uint newMode = value & ModeMask;

        if (oldMode != newMode)
        {
            SaveBankedRegisters();
            Cpsr = value;
            LoadBankedRegisters();
            return;
        }

        Cpsr = value;
    }

    private uint GetAddressRegisterValue(int register)
    {
        if (register == 15)
        {
            return Pc + 4;
        }

        return _registers[register];
    }

    private uint GetOperandRegisterValue(int register)
    {
        if (register == 15)
        {
            return Pc + 4;
        }

        return _registers[register];
    }

    private static bool IsBranchExchange(uint instruction)
    {
        return (instruction & 0x0FFFFFF0) == 0x012FFF10;
    }

    private static bool IsPsrTransfer(uint instruction)
    {
        return (instruction & 0x0DB00000) == 0x01200000;
    }

    private static bool IsDataProcessingRegister(uint instruction)
    {
        return (instruction & 0x0E000010) == 0x00000000;
    }

    private static bool IsThumbPush(ushort instruction)
    {
        return (instruction & 0xFE00) == 0xB400;
    }

    private static bool IsHalfwordDataTransfer(uint instruction)
    {
        return (instruction & 0x0E0000F0) == 0x000000B0;
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

    private void ExecuteHalfwordDataTransfer(uint instruction)
    {
        bool isPreIndexed = (instruction & (1u << 24)) != 0;
        bool addOffset = (instruction & (1u << 23)) != 0;
        bool isImmediateOffset = (instruction & (1u << 22)) != 0;
        bool writeBack = (instruction & (1u << 21)) != 0;
        bool isLoad = (instruction & (1u << 20)) != 0;

        uint operation = (instruction >> 5) & 0x3;

        if (!isPreIndexed || !addOffset || !isImmediateOffset || writeBack || operation != 0x1)
        {
            throw new NotSupportedException($"Unsupported halfword data transfer: 0x{instruction:X8}");
        }

        int baseRegister = (int)((instruction >> 16) & 0xF);
        int dataRegister = (int)((instruction >> 12) & 0xF);

        uint address = GetAddressRegisterValue(baseRegister);

        if (isLoad)
        {
            _registers[dataRegister] = _bus.Read16(address);
        }
        else
        {
            _bus.Write16(address, (ushort)(_registers[dataRegister] & 0xFFFF));
        }
    }

    private void ExecuteSingleDataTransfer(uint instruction)
    {
        bool isImmediateOffset = (instruction & (1u << 25)) == 0;
        bool isPreIndexed = (instruction & (1u << 24)) != 0;
        bool addOffset = (instruction & (1u << 23)) != 0;
        bool isByteTransfer = (instruction & (1u << 22)) != 0;
        bool writeBack = (instruction & (1u << 21)) != 0;
        bool isLoad = (instruction & (1u << 20)) != 0;

        if (!isImmediateOffset || !isPreIndexed || writeBack)
        {
            throw new NotSupportedException($"Unsupported single data transfer: 0x{instruction:X8}");
        }

        int baseRegister = (int)((instruction >> 16) & 0xF);
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        uint offset = instruction & 0xFFF;

        uint address = GetAddressRegisterValue(baseRegister);

        if (addOffset)
        {
            address += offset;
        }
        else
        {
            address -= offset;
        }

        if (isLoad)
        {
            if (isByteTransfer)
            {
                _registers[destinationRegister] = _bus.Read8(address);
            }
            else
            {
                _registers[destinationRegister] = _bus.Read32(address);
            }
        }
        else
        {
            if (isByteTransfer)
            {
                _bus.Write8(address, (byte)(_registers[destinationRegister] & 0xFF));
            }
            else
            {
                _bus.Write32(address, _registers[destinationRegister]);
            }
        }
    }
    
    private void ExecutePsrTransfer(uint instruction)
    {
        bool isImmediateOperand = (instruction & (1u << 25)) != 0;
        bool useSpsr = (instruction & (1u << 22)) != 0;

        if (isImmediateOperand || useSpsr)
        {
            throw new NotSupportedException($"Unsupported PSR transfer: 0x{instruction:X8}");
        }

        int sourceRegister = (int)(instruction & 0xF);
        uint fieldMask = (instruction >> 16) & 0xF;
        uint value = _registers[sourceRegister];
        uint newCpsr = Cpsr;

        if ((fieldMask & 0x1) != 0)
        {
            newCpsr = (newCpsr & 0xFFFFFF00) | (value & 0x000000FF);
        }

        if ((fieldMask & 0x8) != 0)
        {
            newCpsr = (newCpsr & 0x0FFFFFFF) | (value & 0xF0000000);
        }

        SetCpsr(newCpsr);
    }

    private void ExecuteDataProcessingRegister(uint instruction)
    {
        uint opcode = (instruction >> 21) & 0xF;

        if (opcode == 0x0)
        {
            ExecuteAndRegister(instruction);
            return;
        }

        if (opcode == 0x8)
        {
            ExecuteTstRegister(instruction);
            return;
        }

        if (opcode == 0x9)
        {
            ExecuteTeqRegister(instruction);
            return;
        }

        if (opcode == 0xD)
        {
            ExecuteMovRegister(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported data processing register opcode: 0x{opcode:X}");
    }

    private void ExecuteAndRegister(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        int operandRegister = (int)(instruction & 0xF);

        uint left = GetOperandRegisterValue(sourceRegister);
        uint right = GetOperandRegisterValue(operandRegister);
        uint result = left & right;

        _registers[destinationRegister] = result;

        if (ShouldUpdateFlags(instruction))
        {
            SetNegativeAndZeroFlags(result);
        }
    }

    private void ExecuteMovRegister(uint instruction)
    {
        int destinationRegister = (int)((instruction >> 12) & 0xF);
        int sourceRegister = (int)(instruction & 0xF);

        uint operand = GetOperandRegisterValue(sourceRegister);

        _registers[destinationRegister] = operand;

        if (ShouldUpdateFlags(instruction))
        {
            SetNegativeAndZeroFlags(operand);
        }
    }

    private void ExecuteTeqRegister(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int operandRegister = (int)(instruction & 0xF);

        uint left = GetOperandRegisterValue(sourceRegister);
        uint right = GetOperandRegisterValue(operandRegister);
        uint result = left ^ right;

        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteTstRegister(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int operandRegister = (int)(instruction & 0xF);

        uint left = GetOperandRegisterValue(sourceRegister);
        uint right = GetOperandRegisterValue(operandRegister);
        uint result = left & right;

        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteBranchExchange(uint instruction)
    {
        int sourceRegister = (int)(instruction & 0xF);
        uint target = GetOperandRegisterValue(sourceRegister);

        if ((target & 1) != 0)
        {
            Cpsr |= ThumbStateFlag;
        }
        else
        {
            Cpsr &= ~ThumbStateFlag;
        }

        Pc = target & 0xFFFFFFFE;
    }

    private void ExecuteThumbPush(ushort instruction)
    {
        byte registerList = (byte)(instruction & 0xFF);
        bool pushLr = (instruction & (1 << 8)) != 0;

        int registerCount = CountBits(registerList);

        if (pushLr)
        {
            registerCount++;
        }

        uint address = _registers[13] - (uint)(registerCount * 4);

        for (int register = 0; register <= 7; register++)
        {
            if ((registerList & (1 << register)) != 0)
            {
                _bus.Write32(address, _registers[register]);
                address += 4;
            }
        }

        if (pushLr)
        {
            _bus.Write32(address, _registers[14]);
        }

        _registers[13] -= (uint)(registerCount * 4);
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

    public ushort Fetch16()
    {
        ushort instruction = _bus.Read16(Pc);
        Pc += 2;

        return instruction;
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

    private void StepThumb()
    {
        ushort instruction = Fetch16();

        if (IsThumbPush(instruction))
        {
            ExecuteThumbPush(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported Thumb instruction: 0x{instruction:X4}");
    }

    private void StepArm()
    {
        uint instruction = Fetch32();

        if (!ShouldExecute(instruction))
        {
            return;
        }

        if (IsBranchExchange(instruction))
        {
            ExecuteBranchExchange(instruction);
            return;
        }

        if (IsPsrTransfer(instruction))
        {
            ExecutePsrTransfer(instruction);
            return;
        }

        if (IsBranch(instruction))
        {
            ExecuteBranch(instruction);
            return;
        }

        if (IsHalfwordDataTransfer(instruction))
        {
            ExecuteHalfwordDataTransfer(instruction);
            return;
        }

        if (IsDataProcessingImmediate(instruction))
        {
            ExecuteDataProcessingImmediate(instruction);
            return;
        }

        if (IsDataProcessingRegister(instruction))
        {
            ExecuteDataProcessingRegister(instruction);
            return;
        }

        if (IsSingleDataTransfer(instruction))
        {
            ExecuteSingleDataTransfer(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported ARM instruction: 0x{instruction:X8}");
        
    }

    public void Step()
    {
        if (ThumbState)
        {
            StepThumb();
            return;
        }

        StepArm();
    }

}
