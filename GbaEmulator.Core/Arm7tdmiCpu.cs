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
    private uint _irqSpsr;
    private uint _supervisorSpsr;

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

    private static bool IsThumbMovImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x2000;
    }

    private static bool IsThumbStoreHalfwordImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x8000;
    }

    private static bool IsThumbLoadHalfwordImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x8800;
    }

    private static bool IsThumbAddSubtract(ushort instruction)
    {
        return (instruction & 0xF800) == 0x1800;
    }

    private static bool IsThumbAluOperation(ushort instruction)
    {
        return (instruction & 0xFC00) == 0x4000;
    }

    private static bool IsThumbAddImmediateToRegister(ushort instruction)
    {
        return (instruction & 0xF800) == 0x3000;
    }

    private static bool IsThumbPop(ushort instruction)
    {
        return (instruction & 0xFE00) == 0xBC00;
    }

    private static bool IsThumbSoftwareInterrupt(ushort instruction)
    {
        return (instruction & 0xFF00) == 0xDF00;
    }

    private static bool IsThumbBranchExchange(ushort instruction)
    {
        return (instruction & 0xFF87) == 0x4700;
    }

    private static bool IsThumbMoveShiftedRegister(ushort instruction)
    {
        return (instruction & 0xE000) == 0x0000;
    }

    private static bool IsThumbCmpImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x2800;
    }

    private static bool IsThumbConditionalBranch(ushort instruction)
    {
        return (instruction & 0xF000) == 0xD000
            && (instruction & 0x0F00) != 0x0F00;
    }

    private static bool IsThumbUnconditionalBranch(ushort instruction)
    {
        return (instruction & 0xF800) == 0xE000;
    }

    private static bool IsThumbLoadByteImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x7800;
    }

    private static bool IsThumbStoreByteImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x7000;
    }

    private static bool IsThumbSubImmediateFromRegister(ushort instruction)
    {
        return (instruction & 0xF800) == 0x3800;
    }

    private static bool IsThumbAddSubtractStackPointer(ushort instruction)
    {
        return (instruction & 0xFF00) == 0xB000;
    }

    private static bool IsThumbSpRelativeStore(ushort instruction)
    {
        return (instruction & 0xF800) == 0x9000;
    }

    private static bool IsThumbStoreWordImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x6000;
    }

    private static bool IsThumbLoadWordImmediate(ushort instruction)
    {
        return (instruction & 0xF800) == 0x6800;
    }

    private static bool IsThumbStoreMultipleIncrementAfter(ushort instruction)
    {
        return (instruction & 0xF800) == 0xC000;
    }

    private static bool IsThumbLoadAddressFromStackPointer(ushort instruction)
    {
        return (instruction & 0xF800) == 0xA800;
    }

    private static bool IsThumbLoadSignedHalfwordRegisterOffset(ushort instruction)
    {
        return (instruction & 0xFE00) == 0x5E00;
    }

    private static bool IsThumbStoreWordRegisterOffset(ushort instruction)
    {
        return (instruction & 0xFE00) == 0x5000;
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

    private uint GetCurrentSpsr()
    {
        return CurrentMode switch
        {
            IrqMode => _irqSpsr,
            SupervisorMode => _supervisorSpsr,
            _ => 0
        };
    }

    private void SetCurrentSpsr(uint value)
    {
        switch (CurrentMode)
        {
            case IrqMode:
                _irqSpsr = value;
                break;

            case SupervisorMode:
                _supervisorSpsr = value;
                break;
        }
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

    private static bool IsThumbBlPrefix(ushort instruction)
    {
        return (instruction & 0xF800) == 0xF000;
    }

    private static bool IsThumbPcRelativeLoad(ushort instruction)
    {
        return (instruction & 0xF800) == 0x4800;
    }

    private static bool IsThumbHighRegisterOperation(ushort instruction)
    {
        return (instruction & 0xFC00) == 0x4400;
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

    private static bool IsBlockDataTransfer(uint instruction)
    {
        return (instruction & 0x0E000000) == 0x08000000;
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
        uint left = GetOperandRegisterValue(sourceRegister);
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
        uint left = GetOperandRegisterValue(sourceRegister);
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
        uint left = GetOperandRegisterValue(sourceRegister);
        uint result = left - operand;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForSubtraction(left, operand);
        SetOverflowFlagForSubtraction(left, operand, result);
    }

    private void ExecuteTstImmediate(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);
        uint result = GetOperandRegisterValue(sourceRegister) & operand;

        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteTeqImmediate(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        uint operand = DecodeImmediateOperand(instruction);
        uint result = GetOperandRegisterValue(sourceRegister) ^ operand;

        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteDataProcessingImmediate(uint instruction)
    {
        uint opcode = (instruction >> 21) & 0xF;

        if (opcode == 0xA)
        {
            ExecuteCmpImmediate(instruction);
            return;
        }

        if (opcode == 0x8)
        {
            ExecuteTstImmediate(instruction);
            return;
        }

        if (opcode == 0x9)
        {
            ExecuteTeqImmediate(instruction);
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

        throw new NotSupportedException($"Unsupported data processing immediate opcode: 0x{opcode:X} in instruction 0x{instruction:X8}");
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

    private void ExecuteBlockDataTransfer(uint instruction)
    {
        bool preIndex = (instruction & (1u << 24)) != 0;
        bool addOffset = (instruction & (1u << 23)) != 0;
        bool psrAndForceUser = (instruction & (1u << 22)) != 0;
        bool writeBack = (instruction & (1u << 21)) != 0;
        bool isLoad = (instruction & (1u << 20)) != 0;

        if (psrAndForceUser)
        {
            throw new NotSupportedException($"Unsupported block data transfer with S bit: 0x{instruction:X8}");
        }

        int baseRegister = (int)((instruction >> 16) & 0xF);
        ushort registerList = (ushort)(instruction & 0xFFFF);
        int registerCount = CountBits((byte)(registerList & 0xFF)) + CountBits((byte)(registerList >> 8));
        uint baseAddress = _registers[baseRegister];
        uint address;
        uint finalBase;

        if (addOffset)
        {
            address = preIndex ? baseAddress + 4 : baseAddress;
            finalBase = baseAddress + (uint)(registerCount * 4);
        }
        else
        {
            address = preIndex
                ? baseAddress - (uint)(registerCount * 4)
                : baseAddress - (uint)((registerCount - 1) * 4);
            finalBase = baseAddress - (uint)(registerCount * 4);
        }

        for (int register = 0; register <= 15; register++)
        {
            if ((registerList & (1 << register)) == 0)
            {
                continue;
            }

            if (isLoad)
            {
                _registers[register] = _bus.Read32(address);
            }
            else
            {
                uint value = register == 15 ? Pc + 4 : _registers[register];
                _bus.Write32(address, value);
            }

            address += 4;
        }

        if (writeBack)
        {
            _registers[baseRegister] = finalBase;
        }
    }
    
    private void ExecutePsrTransfer(uint instruction)
    {
        bool isImmediateOperand = (instruction & (1u << 25)) != 0;
        bool useSpsr = (instruction & (1u << 22)) != 0;

        if (isImmediateOperand)
        {
            throw new NotSupportedException($"Unsupported PSR transfer: 0x{instruction:X8}");
        }

        int sourceRegister = (int)(instruction & 0xF);
        uint fieldMask = (instruction >> 16) & 0xF;
        uint value = _registers[sourceRegister];
        uint newPsr = useSpsr ? GetCurrentSpsr() : Cpsr;

        if ((fieldMask & 0x1) != 0)
        {
            newPsr = (newPsr & 0xFFFFFF00) | (value & 0x000000FF);
        }

        if ((fieldMask & 0x8) != 0)
        {
            newPsr = (newPsr & 0x0FFFFFFF) | (value & 0xF0000000);
        }

        if (useSpsr)
        {
            SetCurrentSpsr(newPsr);
            return;
        }

        SetCpsr(newPsr);
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

        if (opcode == 0xA)
        {
            ExecuteCmpRegister(instruction);
            return;
        }

        if (opcode == 0xD)
        {
            ExecuteMovRegister(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported data processing register opcode: 0x{opcode:X} in instruction 0x{instruction:X8}");
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

    private void ExecuteCmpRegister(uint instruction)
    {
        int sourceRegister = (int)((instruction >> 16) & 0xF);
        int operandRegister = (int)(instruction & 0xF);

        uint left = GetOperandRegisterValue(sourceRegister);
        uint right = GetOperandRegisterValue(operandRegister);
        uint result = left - right;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForSubtraction(left, right);
        SetOverflowFlagForSubtraction(left, right, result);
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

    private void ExecuteThumbBl(ushort firstHalfword)
    {
        ushort secondHalfword = Fetch16();

        if ((secondHalfword & 0xF800) != 0xF800)
        {
            throw new NotSupportedException(
                $"Unsupported Thumb BL suffix: 0x{secondHalfword:X4}");
        }

        int offsetHigh = firstHalfword & 0x07FF;
        int offsetLow = secondHalfword & 0x07FF;

        int offset = (offsetHigh << 12) | (offsetLow << 1);

        if ((offset & (1 << 22)) != 0)
        {
            offset |= unchecked((int)0xFF800000);
        }

        uint returnAddress = Pc | 1u;
        uint target = (uint)((int)Pc + offset);

        _registers[14] = returnAddress;
        Pc = target & 0xFFFFFFFE;
    }

    private void ExecuteThumbPcRelativeLoad(ushort instruction)
    {
        int destinationRegister = (instruction >> 8) & 0x7;
        uint immediate = (uint)(instruction & 0xFF) * 4;
        uint pcBase = (Pc + 2) & 0xFFFFFFFC;
        uint address = pcBase + immediate;

        _registers[destinationRegister] = _bus.Read32(address);
    }

    private void ExecuteThumbMovImmediate(ushort instruction)
    {
        int destinationRegister = (instruction >> 8) & 0x7;
        uint immediate = (uint)(instruction & 0xFF);

        _registers[destinationRegister] = immediate;

        SetNegativeAndZeroFlags(immediate);
    }

    private void ExecuteThumbStoreHalfwordImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int sourceRegister = instruction & 0x7;

        uint offset = (uint)immediate5 * 2;
        uint address = _registers[baseRegister] + offset;
        ushort value = (ushort)(_registers[sourceRegister] & 0xFFFF);

        _bus.Write16(address, value);
    }

    private void ExecuteThumbLoadHalfwordImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        uint offset = (uint)immediate5 * 2;
        uint address = _registers[baseRegister] + offset;

        _registers[destinationRegister] = _bus.Read16(address);
    }

    private void ExecuteThumbAddSubtract(ushort instruction)
    {
        bool useImmediate = (instruction & (1 << 10)) != 0;
        bool isSubtract = (instruction & (1 << 9)) != 0;

        int operand = (instruction >> 6) & 0x7;
        int sourceRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        uint left = _registers[sourceRegister];
        uint right = useImmediate ? (uint)operand : _registers[operand];
        uint result;

        if (isSubtract)
        {
            result = left - right;
            _registers[destinationRegister] = result;

            SetNegativeAndZeroFlags(result);
            SetCarryFlagForSubtraction(left, right);
            SetOverflowFlagForSubtraction(left, right, result);
        }
        else
        {
            result = left + right;
            _registers[destinationRegister] = result;

            SetNegativeAndZeroFlags(result);
            SetCarryFlagForAddition(left, right, result);
            SetOverflowFlagForAddition(left, right, result);
        }
    }

    private void ExecuteThumbAluOperation(ushort instruction)
    {
        int opcode = (instruction >> 6) & 0xF;
        int sourceRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        if (opcode == 0x0)
        {
            uint result = _registers[destinationRegister] & _registers[sourceRegister];

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0x1)
        {
            uint result = _registers[destinationRegister] ^ _registers[sourceRegister];

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0xC)
        {
            uint result = _registers[destinationRegister] | _registers[sourceRegister];

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0xA)
        {
            uint left = _registers[destinationRegister];
            uint right = _registers[sourceRegister];
            uint result = left - right;

            SetNegativeAndZeroFlags(result);
            SetCarryFlagForSubtraction(left, right);
            SetOverflowFlagForSubtraction(left, right, result);
            return;
        }

        if (opcode == 0x9)
        {
            uint left = 0;
            uint right = _registers[sourceRegister];
            uint result = left - right;

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            SetCarryFlagForSubtraction(left, right);
            SetOverflowFlagForSubtraction(left, right, result);
            return;
        }

        if (opcode == 0x8)
        {
            uint result = _registers[destinationRegister] & _registers[sourceRegister];

            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0xD)
        {
            uint result = unchecked(_registers[destinationRegister] * _registers[sourceRegister]);

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0xE)
        {
            uint result = _registers[destinationRegister] & ~_registers[sourceRegister];

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        if (opcode == 0xF)
        {
            uint result = ~_registers[sourceRegister];

            _registers[destinationRegister] = result;
            SetNegativeAndZeroFlags(result);
            return;
        }

        throw new NotSupportedException(
            $"Unsupported Thumb ALU opcode: 0x{opcode:X} in instruction 0x{instruction:X4}");
    }

    private void ExecuteThumbAddImmediateToRegister(ushort instruction)
    {
        int destinationRegister = (instruction >> 8) & 0x7;
        uint immediate = (uint)(instruction & 0xFF);
        uint left = _registers[destinationRegister];
        uint result = left + immediate;

        _registers[destinationRegister] = result;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForAddition(left, immediate, result);
        SetOverflowFlagForAddition(left, immediate, result);
    }

    private void ExecuteThumbPop(ushort instruction)
    {
        byte registerList = (byte)(instruction & 0xFF);
        bool popPc = (instruction & (1 << 8)) != 0;

        uint address = _registers[13];

        for (int register = 0; register <= 7; register++)
        {
            if ((registerList & (1 << register)) != 0)
            {
                _registers[register] = _bus.Read32(address);
                address += 4;
            }
        }

        if (popPc)
        {
            uint target = _bus.Read32(address);
            address += 4;

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

        _registers[13] = address;
    }

    private void ExecuteThumbSoftwareInterrupt(ushort instruction)
    {
        byte comment = (byte)(instruction & 0xFF);

        if (comment == 0x01)
        {
            ExecuteRegisterRamReset();
            return;
        }

        if (comment == 0x0B)
        {
            ExecuteCpuSet();
            return;
        }

        if (comment == 0x06)
        {
            ExecuteDiv();
            return;
        }

        if (comment == 0x05)
        {
            return;
        }

        throw new NotSupportedException($"Unsupported BIOS SWI: 0x{comment:X2}");
    }

    private void ExecuteRegisterRamReset()
    {
        uint flags = _registers[0];

        if ((flags & (1u << 0)) != 0)
        {
            _bus.ClearEwram();
        }

        if ((flags & (1u << 1)) != 0)
        {
            _bus.ClearIwram();
        }
    }

    private void ExecuteCpuSet()
    {
        uint source = _registers[0];
        uint destination = _registers[1];
        uint mode = _registers[2];
        int count = (int)(mode & 0x001FFFFF);
        bool copy32Bit = (mode & (1u << 24)) != 0;
        bool fixedSource = (mode & (1u << 26)) != 0;

        if (copy32Bit)
        {
            uint fixedValue = fixedSource ? _bus.Read32(source) : 0;

            for (int i = 0; i < count; i++)
            {
                uint value = fixedSource ? fixedValue : _bus.Read32(source);
                _bus.Write32(destination, value);

                if (!fixedSource)
                {
                    source += 4;
                }

                destination += 4;
            }
        }
        else
        {
            ushort fixedValue = fixedSource ? _bus.Read16(source) : (ushort)0;

            for (int i = 0; i < count; i++)
            {
                ushort value = fixedSource ? fixedValue : _bus.Read16(source);
                _bus.Write16(destination, value);

                if (!fixedSource)
                {
                    source += 2;
                }

                destination += 2;
            }
        }
    }

    private void ExecuteDiv()
    {
        int numerator = unchecked((int)_registers[0]);
        int denominator = unchecked((int)_registers[1]);

        if (denominator == 0)
        {
            _registers[0] = 0;
            _registers[1] = (uint)numerator;
            _registers[3] = 0;
            return;
        }

        int quotient = numerator / denominator;
        int remainder = numerator % denominator;

        _registers[0] = unchecked((uint)quotient);
        _registers[1] = unchecked((uint)remainder);
        _registers[3] = quotient == int.MinValue ? 0x80000000u : (uint)Math.Abs(quotient);
    }

    private void ExecuteThumbBranchExchange(ushort instruction)
    {
        int sourceRegister = ((instruction >> 3) & 0xF);
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

    private void ExecuteThumbMoveShiftedRegister(ushort instruction)
    {
        int opcode = (instruction >> 11) & 0x3;
        int offset = (instruction >> 6) & 0x1F;
        int sourceRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        if (opcode == 0x0)
        {
            ExecuteThumbLslImmediate(destinationRegister, sourceRegister, offset);
            return;
        }

        if (opcode == 0x1)
        {
            ExecuteThumbLsrImmediate(destinationRegister, sourceRegister, offset);
            return;
        }

        if (opcode == 0x2)
        {
            ExecuteThumbAsrImmediate(destinationRegister, sourceRegister, offset);
            return;
        }

        throw new NotSupportedException(
            $"Unsupported Thumb shift opcode: 0x{opcode:X} in instruction 0x{instruction:X4}");
    }

    private void ExecuteThumbLslImmediate(int destinationRegister, int sourceRegister, int offset)
    {
        uint value = _registers[sourceRegister];
        uint result;

        if (offset == 0)
        {
            result = value;
        }
        else
        {
            uint carryOut = (value >> (32 - offset)) & 1u;
            result = value << offset;

            if (carryOut != 0)
            {
                Cpsr |= CarryFlag;
            }
            else
            {
                Cpsr &= ~CarryFlag;
            }
        }

        _registers[destinationRegister] = result;
        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteThumbCmpImmediate(ushort instruction)
    {
        int sourceRegister = (instruction >> 8) & 0x7;
        uint immediate = (uint)(instruction & 0xFF);
        uint left = _registers[sourceRegister];
        uint result = left - immediate;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForSubtraction(left, immediate);
        SetOverflowFlagForSubtraction(left, immediate, result);
    }

    private void ExecuteThumbConditionalBranch(ushort instruction)
    {
        uint condition = (uint)((instruction >> 8) & 0xF);

        if (!ShouldExecuteCondition(condition))
        {
            return;
        }

        int offset = instruction & 0xFF;

        if ((offset & 0x80) != 0)
        {
            offset |= unchecked((int)0xFFFFFF00);
        }

        offset <<= 1;

        Pc = (uint)((int)Pc + 2 + offset);
    }

    private void ExecuteThumbUnconditionalBranch(ushort instruction)
    {
        int offset = instruction & 0x07FF;

        if ((offset & 0x0400) != 0)
        {
            offset |= unchecked((int)0xFFFFF800);
        }

        offset <<= 1;

        Pc = (uint)((int)Pc + 2 + offset);
    }

    private void ExecuteThumbLoadByteImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        uint address = _registers[baseRegister] + (uint)immediate5;

        _registers[destinationRegister] = _bus.Read8(address);
    }

    private void ExecuteThumbStoreByteImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int sourceRegister = instruction & 0x7;

        uint address = _registers[baseRegister] + (uint)immediate5;
        byte value = (byte)(_registers[sourceRegister] & 0xFF);

        _bus.Write8(address, value);
    }

    private void ExecuteThumbAddSubtractStackPointer(ushort instruction)
    {
        bool subtract = (instruction & (1 << 7)) != 0;
        uint offset = (uint)(instruction & 0x7F) * 4;

        if (subtract)
        {
            _registers[13] -= offset;
        }
        else
        {
            _registers[13] += offset;
        }
    }

    private void ExecuteThumbSpRelativeStore(ushort instruction)
    {
        int sourceRegister = (instruction >> 8) & 0x7;
        uint offset = (uint)(instruction & 0xFF) * 4;
        uint address = _registers[13] + offset;

        _bus.Write32(address, _registers[sourceRegister]);
    }

    private void ExecuteThumbHighRegisterOperation(ushort instruction)
    {
        int opcode = (instruction >> 8) & 0x3;
        int highDestinationBit = (instruction >> 7) & 0x1;
        int highSourceBit = (instruction >> 6) & 0x1;
        int sourceRegister = ((highSourceBit << 3) | ((instruction >> 3) & 0x7));
        int destinationRegister = ((highDestinationBit << 3) | (instruction & 0x7));

        if (opcode == 0x2)
        {
            uint value = GetOperandRegisterValue(sourceRegister);
            _registers[destinationRegister] = value;
            return;
        }

        if (opcode == 0x3)
        {
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
            return;
        }

        throw new NotSupportedException(
            $"Unsupported Thumb high register opcode: 0x{opcode:X} in instruction 0x{instruction:X4}");
    }

    private void ExecuteThumbStoreWordImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int sourceRegister = instruction & 0x7;

        uint offset = (uint)immediate5 * 4;
        uint address = _registers[baseRegister] + offset;

        _bus.Write32(address, _registers[sourceRegister]);
    }

    private void ExecuteThumbLoadWordImmediate(ushort instruction)
    {
        int immediate5 = (instruction >> 6) & 0x1F;
        int baseRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;

        uint offset = (uint)immediate5 * 4;
        uint address = _registers[baseRegister] + offset;

        _registers[destinationRegister] = _bus.Read32(address);
    }

    private void ExecuteThumbStoreMultipleIncrementAfter(ushort instruction)
    {
        int baseRegister = (instruction >> 8) & 0x7;
        byte registerList = (byte)(instruction & 0xFF);
        uint address = _registers[baseRegister];

        for (int register = 0; register <= 7; register++)
        {
            if ((registerList & (1 << register)) != 0)
            {
                _bus.Write32(address, _registers[register]);
                address += 4;
            }
        }

        _registers[baseRegister] = address;
    }

    private void ExecuteThumbLoadAddressFromStackPointer(ushort instruction)
    {
        int destinationRegister = (instruction >> 8) & 0x7;
        uint offset = (uint)(instruction & 0xFF) * 4;

        _registers[destinationRegister] = _registers[13] + offset;
    }

    private void ExecuteThumbLoadSignedHalfwordRegisterOffset(ushort instruction)
    {
        int offsetRegister = (instruction >> 6) & 0x7;
        int baseRegister = (instruction >> 3) & 0x7;
        int destinationRegister = instruction & 0x7;
        uint address = _registers[baseRegister] + _registers[offsetRegister];
        short value = unchecked((short)_bus.Read16(address));

        _registers[destinationRegister] = unchecked((uint)value);
    }

    private void ExecuteThumbStoreWordRegisterOffset(ushort instruction)
    {
        int offsetRegister = (instruction >> 6) & 0x7;
        int baseRegister = (instruction >> 3) & 0x7;
        int sourceRegister = instruction & 0x7;
        uint address = _registers[baseRegister] + _registers[offsetRegister];

        _bus.Write32(address, _registers[sourceRegister]);
    }

    private void ExecuteThumbSubImmediateFromRegister(ushort instruction)
    {
        int destinationRegister = (instruction >> 8) & 0x7;
        uint immediate = (uint)(instruction & 0xFF);
        uint left = _registers[destinationRegister];
        uint result = left - immediate;

        _registers[destinationRegister] = result;

        SetNegativeAndZeroFlags(result);
        SetCarryFlagForSubtraction(left, immediate);
        SetOverflowFlagForSubtraction(left, immediate, result);
    }

    private void ExecuteThumbLsrImmediate(int destinationRegister, int sourceRegister, int offset)
    {
        uint value = _registers[sourceRegister];
        uint result;
        uint carryOut;

        if (offset == 0)
        {
            carryOut = (value >> 31) & 1u;
            result = 0;
        }
        else
        {
            carryOut = (value >> (offset - 1)) & 1u;
            result = value >> offset;
        }

        if (carryOut != 0)
        {
            Cpsr |= CarryFlag;
        }
        else
        {
            Cpsr &= ~CarryFlag;
        }

        _registers[destinationRegister] = result;
        SetNegativeAndZeroFlags(result);
    }

    private void ExecuteThumbAsrImmediate(int destinationRegister, int sourceRegister, int offset)
    {
        uint value = _registers[sourceRegister];
        uint result;
        uint carryOut;

        if (offset == 0)
        {
            carryOut = (value >> 31) & 1u;
            result = carryOut != 0 ? 0xFFFFFFFFu : 0;
        }
        else
        {
            carryOut = (value >> (offset - 1)) & 1u;
            result = unchecked((uint)((int)value >> offset));
        }

        if (carryOut != 0)
        {
            Cpsr |= CarryFlag;
        }
        else
        {
            Cpsr &= ~CarryFlag;
        }

        _registers[destinationRegister] = result;
        SetNegativeAndZeroFlags(result);
    }

    private static bool ShouldUpdateFlags(uint instruction)
    {
        return (instruction & (1u << 20)) != 0;
    }

    private bool ShouldExecuteCondition(uint condition)
    {
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
        return ShouldExecuteCondition(condition);
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

        if (IsThumbBlPrefix(instruction))
        {
            ExecuteThumbBl(instruction);
            return;
        }
        
        if (IsThumbPush(instruction))
        {
            ExecuteThumbPush(instruction);
            return;
        }

        if (IsThumbPcRelativeLoad(instruction))
        {
            ExecuteThumbPcRelativeLoad(instruction);
            return;
        }

        if (IsThumbMovImmediate(instruction))
        {
            ExecuteThumbMovImmediate(instruction);
            return;
        }

        if (IsThumbStoreHalfwordImmediate(instruction))
        {
            ExecuteThumbStoreHalfwordImmediate(instruction);
            return;
        }

        if (IsThumbLoadHalfwordImmediate(instruction))
        {
            ExecuteThumbLoadHalfwordImmediate(instruction);
            return;
        }

        if (IsThumbAddSubtract(instruction))
        {
            ExecuteThumbAddSubtract(instruction);
            return;
        }

        if (IsThumbAluOperation(instruction))
        {
            ExecuteThumbAluOperation(instruction);
            return;
        }

        if (IsThumbAddImmediateToRegister(instruction))
        {
            ExecuteThumbAddImmediateToRegister(instruction);
            return;
        }

        if (IsThumbPop(instruction))
        {
            ExecuteThumbPop(instruction);
            return;
        }

        if (IsThumbSoftwareInterrupt(instruction))
        {
            ExecuteThumbSoftwareInterrupt(instruction);
            return;
        }

        if (IsThumbMoveShiftedRegister(instruction))
        {
            ExecuteThumbMoveShiftedRegister(instruction);
            return;
        }

        if (IsThumbCmpImmediate(instruction))
        {
            ExecuteThumbCmpImmediate(instruction);
            return;
        }

        if (IsThumbConditionalBranch(instruction))
        {
            ExecuteThumbConditionalBranch(instruction);
            return;
        }

        if (IsThumbUnconditionalBranch(instruction))
        {
            ExecuteThumbUnconditionalBranch(instruction);
            return;
        }

        if (IsThumbLoadByteImmediate(instruction))
        {
            ExecuteThumbLoadByteImmediate(instruction);
            return;
        }

        if (IsThumbStoreByteImmediate(instruction))
        {
            ExecuteThumbStoreByteImmediate(instruction);
            return;
        }

        if (IsThumbSubImmediateFromRegister(instruction))
        {
            ExecuteThumbSubImmediateFromRegister(instruction);
            return;
        }

        if (IsThumbAddSubtractStackPointer(instruction))
        {
            ExecuteThumbAddSubtractStackPointer(instruction);
            return;
        }

        if (IsThumbSpRelativeStore(instruction))
        {
            ExecuteThumbSpRelativeStore(instruction);
            return;
        }

        if (IsThumbHighRegisterOperation(instruction))
        {
            ExecuteThumbHighRegisterOperation(instruction);
            return;
        }

        if (IsThumbBranchExchange(instruction))
        {
            ExecuteThumbBranchExchange(instruction);
            return;
        }

        if (IsThumbStoreWordImmediate(instruction))
        {
            ExecuteThumbStoreWordImmediate(instruction);
            return;
        }

        if (IsThumbLoadWordImmediate(instruction))
        {
            ExecuteThumbLoadWordImmediate(instruction);
            return;
        }

        if (IsThumbStoreMultipleIncrementAfter(instruction))
        {
            ExecuteThumbStoreMultipleIncrementAfter(instruction);
            return;
        }

        if (IsThumbLoadAddressFromStackPointer(instruction))
        {
            ExecuteThumbLoadAddressFromStackPointer(instruction);
            return;
        }

        if (IsThumbLoadSignedHalfwordRegisterOffset(instruction))
        {
            ExecuteThumbLoadSignedHalfwordRegisterOffset(instruction);
            return;
        }

        if (IsThumbStoreWordRegisterOffset(instruction))
        {
            ExecuteThumbStoreWordRegisterOffset(instruction);
            return;
        }


        ushort previous = _bus.Read16(Pc - 4);
        ushort current = _bus.Read16(Pc - 2);
        ushort next = _bus.Read16(Pc);

        throw new NotSupportedException(
            $"Unsupported Thumb instruction: 0x{instruction:X4} at PC=0x{Pc - 2:X8}, " +
            $"prev=0x{previous:X4}, current=0x{current:X4}, next=0x{next:X4}");
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

        if (IsBlockDataTransfer(instruction))
        {
            ExecuteBlockDataTransfer(instruction);
            return;
        }

        throw new NotSupportedException($"Unsupported ARM instruction: 0x{instruction:X8}");
        
    }

    public void Step()
    {
        if (ThumbState)
        {
            StepThumb();
        }
        else
        {
            StepArm();
        }

        _bus.Tick(1);
    }

}
