using GbaEmulator.Core;

namespace GbaEmulator.Tests;

public sealed class CpuInstructionTests
{
    [Fact]
    public void MovImmediate_WritesValueToRegister()
    {
        byte[] rom =
        [
            0x05, 0x00, 0xA0, 0xE3
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();

        Assert.Equal((uint)5, cpu.GetRegister(0));
    }

    [Fact]
    public void AddImmediate_AddsOperandToSourceRegister()
    {
        byte[] rom =
        [
            0x0A, 0x10, 0xA0, 0xE3,
            0x05, 0x00, 0x81, 0xE2
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();

        Assert.Equal((uint)10, cpu.GetRegister(1));
        Assert.Equal((uint)15, cpu.GetRegister(0));
    }

    [Fact]
    public void SubImmediate_SubtractsOperandFromSourceRegister()
    {
        byte[] rom =
        [
            0x0A, 0x10, 0xA0, 0xE3,
            0x03, 0x00, 0x41, 0xE2
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();

        Assert.Equal((uint)7, cpu.GetRegister(0));
    }

    [Fact]
    public void CmpImmediate_UpdatesZeroFlagWithoutChangingRegister()
    {
        byte[] rom =
        [
            0x05, 0x00, 0xA0, 0xE3,
            0x05, 0x00, 0x50, 0xE3
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();

        Assert.Equal((uint)5, cpu.GetRegister(0));
        Assert.True(cpu.ZeroFlagSet);
        Assert.True(cpu.CarryFlagSet);
        Assert.False(cpu.NegativeFlagSet);
    }

    [Fact]
    public void Bne_LoopsUntilZeroFlagIsSet()
    {
        byte[] rom =
        [
            0x03, 0x00, 0xA0, 0xE3,
            0x01, 0x00, 0x50, 0xE2,
            0xFD, 0xFF, 0xFF, 0x1A
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();
        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.Equal((uint)0, cpu.GetRegister(0));
        Assert.True(cpu.ZeroFlagSet);
        Assert.Equal(0x08000008u, cpu.Pc);
    }

    [Fact]
    public void LdrImmediate_LoadsWordFromMemory()
    {
        byte[] rom =
        [
            0x04, 0x00, 0x91, 0xE5,
            0x78, 0x56, 0x34, 0x12
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetRegisterForTesting(1, 0x08000000);

        cpu.Step();

        Assert.Equal(0x12345678u, cpu.GetRegister(0));
    }

    [Fact]
    public void StrImmediate_StoresWordToMemory()
    {
        byte[] rom =
        [
            0x04, 0x00, 0x81, 0xE5
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        cpu.SetRegisterForTesting(0, 0x12345678);
        cpu.SetRegisterForTesting(1, 0x02000000);

        cpu.Step();

        Assert.Equal(0x12345678u, bus.Read32(0x02000004));
    }

    [Fact]
    public void StrbImmediate_StoresLowestByteToMemory()
    {
        byte[] rom =
        [
            0x04, 0x00, 0xC1, 0xE5
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        cpu.SetRegisterForTesting(0, 0x12345678);
        cpu.SetRegisterForTesting(1, 0x02000000);

        cpu.Step();

        Assert.Equal(0x78, bus.Read8(0x02000004));
    }

    [Fact]
    public void LdrbImmediate_LoadsByteAndZeroExtendsIt()
    {
        byte[] rom =
        [
            0x04, 0x00, 0xD1, 0xE5
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        bus.Write8(0x02000004, 0xFF);
        cpu.SetRegisterForTesting(1, 0x02000000);

        cpu.Step();

        Assert.Equal(0x000000FFu, cpu.GetRegister(0));
    }

    [Fact]
    public void MemoryBus_ReadsAndWritesIWRAM()
    {
        MemoryBus bus = new MemoryBus([]);

        bus.Write32(0x03000010, 0xDEADBEEF);

        Assert.Equal(0xDEADBEEFu, bus.Read32(0x03000010));
    }

    [Fact]
    public void MemoryBus_EwramMirrorsEvery256Kib()
    {
        MemoryBus bus = new MemoryBus([]);

        bus.Write8(0x02000000, 0x12);

        Assert.Equal(0x12, bus.Read8(0x02040000));
    }

    [Fact]
    public void MemoryBus_IwramMirrorsEvery32Kib()
    {
        MemoryBus bus = new MemoryBus([]);

        bus.Write8(0x03000000, 0x34);

        Assert.Equal(0x34, bus.Read8(0x03008000));
    }

    [Fact]
    public void KeyInput_DefaultsToNoButtonsPressed()
    {
        MemoryBus bus = new MemoryBus([]);

        Assert.Equal(0x03FFu, bus.Read16(0x04000130));
    }

    [Fact]
    public void KeyInput_ReleasedButtonSetsBit()
    {
        MemoryBus bus = new MemoryBus([]);

        bus.SetButtonState(GbaButton.A, pressed: true);
        bus.SetButtonState(GbaButton.A, pressed: false);

        Assert.Equal(0x03FFu, bus.Read16(0x04000130));
    }

    [Fact]
    public void Ldrh_LoadsKeyInputRegister()
    {
        byte[] rom =
        [
            0xB0, 0x00, 0xD1, 0xE1
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        bus.SetButtonState(GbaButton.A, pressed: true);
        cpu.SetRegisterForTesting(1, 0x04000130);

        cpu.Step();

        Assert.Equal(0x03FEu, cpu.GetRegister(0));
    }

    [Fact]
    public void Strh_StoresLowerHalfwordToMemory()
    {
        byte[] rom =
        [
            0xB0, 0x00, 0xC1, 0xE1
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        cpu.SetRegisterForTesting(0, 0x12345678);
        cpu.SetRegisterForTesting(1, 0x02000000);

        cpu.Step();

        Assert.Equal(0x5678u, bus.Read16(0x02000000));
    }

    [Fact]
    public void MemoryBus_ReadsBiosWhenProvided()
    {
        byte[] bios = new byte[16 * 1024];
        bios[0] = 0x12;
        bios[0x3FFF] = 0x34;

        MemoryBus bus = new MemoryBus([], bios);

        Assert.Equal(0x12, bus.Read8(0x00000000));
        Assert.Equal(0x34, bus.Read8(0x00003FFF));
    }

    [Fact]
    public void MemoryBus_RejectsInvalidBiosSize()
    {
        byte[] invalidBios = new byte[123];

        Assert.Throws<ArgumentException>(() => new MemoryBus([], invalidBios));
    }

    [Fact]
    public void Cpu_StartsAtBiosWhenSkipBiosIsFalse()
    {
        MemoryBus bus = new MemoryBus([], new byte[16 * 1024]);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus, skipBios: false);

        Assert.Equal(0x00000000u, cpu.Pc);
    }

    [Fact]
    public void Cpu_SkipsBiosByDefault()
    {
        MemoryBus bus = new MemoryBus([]);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        Assert.Equal(0x08000000u, cpu.Pc);
    }

    [Fact]
    public void MovRegister_CopiesRegisterValue()
    {
        byte[] rom =
        [
            0x01, 0x10, 0xA0, 0xE3,
            0x01, 0x20, 0xA0, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();

        Assert.Equal(1u, cpu.GetRegister(2));
    }

    [Fact]
    public void MovRegister_WhenSourceIsPcReadsCurrentInstructionAddressPlus8()
    {
        byte[] rom =
        [
            0x0F, 0xE0, 0xA0, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();

        Assert.Equal(0x08000008u, cpu.GetRegister(14));
    }

    [Fact]
    public void TeqRegister_SetsZeroFlagWhenOperandsAreEqual()
    {
        byte[] rom =
        [
            0x05, 0x00, 0xA0, 0xE3,
            0x05, 0x10, 0xA0, 0xE3,
            0x01, 0x00, 0x30, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.True(cpu.ZeroFlagSet);
    }

    [Fact]
    public void TeqRegister_ClearsZeroFlagWhenOperandsDiffer()
    {
        byte[] rom =
        [
            0x05, 0x00, 0xA0, 0xE3,
            0x07, 0x10, 0xA0, 0xE3,
            0x01, 0x00, 0x30, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void Bx_BranchesToRegisterValue()
    {
        byte[] rom =
        [
            0x11, 0xFF, 0x2F, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetRegisterForTesting(1, 0x03005D90);

        cpu.Step();

        Assert.Equal(0x03005D90u, cpu.Pc);
        Assert.False(cpu.ThumbState);
    }

    [Fact]
    public void Bx_SwitchesToThumbWhenTargetBitZeroIsSet()
    {
        byte[] rom =
        [
            0x11, 0xFF, 0x2F, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetRegisterForTesting(1, 0x03005D91);

        cpu.Step();

        Assert.Equal(0x03005D90u, cpu.Pc);
        Assert.True(cpu.ThumbState);
    }

    [Fact]
    public void Msr_SwitchesBankedStackPointersBetweenIrqAndSystemModes()
    {
        byte[] rom =
        [
            0x12, 0x00, 0xA0, 0xE3,
            0x00, 0xF0, 0x29, 0xE1,
            0x11, 0xD0, 0xA0, 0xE3,
            0x1F, 0x00, 0xA0, 0xE3,
            0x00, 0xF0, 0x29, 0xE1,
            0x22, 0xD0, 0xA0, 0xE3,
            0x12, 0x00, 0xA0, 0xE3,
            0x00, 0xF0, 0x29, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();
        Assert.Equal(0x11u, cpu.GetRegister(13));

        cpu.Step();
        cpu.Step();
        cpu.Step();
        Assert.Equal(0x22u, cpu.GetRegister(13));

        cpu.Step();
        cpu.Step();
        Assert.Equal(0x11u, cpu.GetRegister(13));
    }

    [Fact]
    public void LdrImmediate_WhenBaseIsPcUsesCurrentInstructionAddressPlus8()
    {
        byte[] rom =
        [
            0x00, 0x00, 0x9F, 0xE5,
            0x00, 0x00, 0x00, 0x00,
            0x78, 0x56, 0x34, 0x12
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();

        Assert.Equal(0x12345678u, cpu.GetRegister(0));
    }

    [Fact]
    public void TstRegister_SetsZeroFlagWhenNoBitsOverlap()
    {
        byte[] rom =
        [
            0x01, 0x00, 0xA0, 0xE3,
            0x02, 0x10, 0xA0, 0xE3,
            0x01, 0x00, 0x10, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.True(cpu.ZeroFlagSet);
    }

    [Fact]
    public void TstRegister_ClearsZeroFlagWhenBitsOverlap()
    {
        byte[] rom =
        [
            0x03, 0x00, 0xA0, 0xE3,
            0x02, 0x10, 0xA0, 0xE3,
            0x01, 0x00, 0x10, 0xE1
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void AndRegister_StoresBitwiseAndResult()
    {
        byte[] rom =
        [
            0x03, 0x00, 0xA0, 0xE3,
            0x02, 0x10, 0xA0, 0xE3,
            0x01, 0x20, 0x00, 0xE0
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);

        cpu.Step();
        cpu.Step();
        cpu.Step();

        Assert.Equal(2u, cpu.GetRegister(2));
    }

    [Fact]
    public void ThumbPush_StoresRegistersAndLinkRegisterOnStack()
    {
        byte[] rom =
        [
            0x70, 0xB5
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);

        cpu.SetRegisterForTesting(4, 0x44444444);
        cpu.SetRegisterForTesting(5, 0x55555555);
        cpu.SetRegisterForTesting(6, 0x66666666);
        cpu.SetRegisterForTesting(13, 0x03008000);
        cpu.SetRegisterForTesting(14, 0x080000F0);
        cpu.SetThumbStateForTesting(true);

        cpu.Step();

        Assert.Equal(0x03007FF0u, cpu.GetRegister(13));
        Assert.Equal(0x44444444u, bus.Read32(0x03007FF0));
        Assert.Equal(0x55555555u, bus.Read32(0x03007FF4));
        Assert.Equal(0x66666666u, bus.Read32(0x03007FF8));
        Assert.Equal(0x080000F0u, bus.Read32(0x03007FFC));
    }

    [Fact]
    public void ThumbBl_SetsLinkRegisterAndBranches()
    {
        byte[] rom =
        [
            0x00, 0xF0,
            0x00, 0xF8
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.Step();

        Assert.Equal(0x08000005u, cpu.GetRegister(14));
        Assert.Equal(0x08000004u, cpu.Pc);
    }

    [Fact]
    public void ThumbPcRelativeLoad_LoadsWordFromAlignedPcPlusImmediate()
    {
        byte[] rom =
        [
            0x00, 0x48,
            0x00, 0x00,
            0x78, 0x56, 0x34, 0x12
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.Step();

        Assert.Equal(0x12345678u, cpu.GetRegister(0));
    }

    [Fact]
    public void ThumbMovImmediate_WritesImmediateToRegisterAndUpdatesFlags()
    {
        byte[] rom =
        [
            0x00, 0x21
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.Step();

        Assert.Equal(0u, cpu.GetRegister(1));
        Assert.True(cpu.ZeroFlagSet);
        Assert.False(cpu.NegativeFlagSet);
    }

    [Fact]
    public void ThumbStoreHalfwordImmediate_StoresLowerHalfword()
    {
        byte[] rom =
        [
            0x11, 0x80
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);
        cpu.SetThumbStateForTesting(true);

        cpu.SetRegisterForTesting(1, 0x12345678);
        cpu.SetRegisterForTesting(2, 0x02000000);

        cpu.Step();

        Assert.Equal(0x5678u, bus.Read16(0x02000000));
    }

    [Fact]
    public void ThumbLoadHalfwordImmediate_LoadsHalfword()
    {
        byte[] rom =
        [
            0x4A, 0x89
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);
        cpu.SetThumbStateForTesting(true);

        cpu.SetRegisterForTesting(1, 0x02000000);
        bus.Write16(0x0200000A, 0xBEEF);

        cpu.Step();

        Assert.Equal(0xBEEFu, cpu.GetRegister(2));
    }

    [Fact]
    public void ThumbAddImmediate_AddsSmallImmediate()
    {
        byte[] rom =
        [
            0x20, 0x1C
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(4, 0x12345678);

        cpu.Step();

        Assert.Equal(0x12345678u, cpu.GetRegister(0));
        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void ThumbAluAnd_StoresBitwiseAndResult()
    {
        byte[] rom =
        [
            0x10, 0x40
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.SetRegisterForTesting(0, 0b1010);
        cpu.SetRegisterForTesting(2, 0b0110);

        cpu.Step();

        Assert.Equal(0b0010u, cpu.GetRegister(0));
        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void ThumbAddImmediateToRegister_AddsImmediateAndUpdatesFlags()
    {
        byte[] rom =
        [
            0x0C, 0x31
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(1, 4);

        cpu.Step();

        Assert.Equal(16u, cpu.GetRegister(1));
        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void ThumbPop_RestoresRegistersAndProgramCounter()
    {
        byte[] rom =
        [
            0x10, 0xBD
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(13, 0x03007FF8);

        bus.Write32(0x03007FF8, 0x44444444);
        bus.Write32(0x03007FFC, 0x08000021);

        cpu.Step();

        Assert.Equal(0x44444444u, cpu.GetRegister(4));
        Assert.Equal(0x08000020u, cpu.Pc);
        Assert.True(cpu.ThumbState);
        Assert.Equal(0x03008000u, cpu.GetRegister(13));
    }

    [Fact]
    public void ThumbSwiRegisterRamReset_ClearsEwramWhenBitZeroIsSet()
    {
        byte[] rom =
        [
            0x01, 0xDF
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);
        cpu.SetThumbStateForTesting(true);

        bus.Write32(0x02000000, 0x12345678);
        cpu.SetRegisterForTesting(0, 1);

        cpu.Step();

        Assert.Equal(0u, bus.Read32(0x02000000));
    }

    [Fact]
    public void ThumbSwiRegisterRamReset_DoesNotClearTopOfIwram()
    {
        byte[] rom =
        [
            0x01, 0xDF
        ];

        MemoryBus bus = new MemoryBus(rom);
        Arm7tdmiCpu cpu = new Arm7tdmiCpu(bus);
        cpu.SetThumbStateForTesting(true);

        bus.Write32(0x03000000, 0x11111111);
        bus.Write32(0x03007F00, 0x22222222);
        cpu.SetRegisterForTesting(0, 2);

        cpu.Step();

        Assert.Equal(0u, bus.Read32(0x03000000));
        Assert.Equal(0x22222222u, bus.Read32(0x03007F00));
    }

    [Fact]
    public void ThumbBx_BranchesToRegisterValueAndKeepsThumbWhenBitZeroIsSet()
    {
        byte[] rom =
        [
            0x70, 0x47
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(14, 0x08001235);

        cpu.Step();

        Assert.Equal(0x08001234u, cpu.Pc);
        Assert.True(cpu.ThumbState);
    }

    [Fact]
    public void ThumbBx_SwitchesToArmWhenBitZeroIsClear()
    {
        byte[] rom =
        [
            0x70, 0x47
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(14, 0x08001234);

        cpu.Step();

        Assert.Equal(0x08001234u, cpu.Pc);
        Assert.False(cpu.ThumbState);
    }

    [Fact]
    public void ThumbLslImmediate_ShiftsValueLeft()
    {
        byte[] rom =
        [
            0xC9, 0x04
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(1, 1);

        cpu.Step();

        Assert.Equal(0x00080000u, cpu.GetRegister(1));
        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void ThumbAluOrr_StoresBitwiseOrResult()
    {
        byte[] rom =
        [
            0x11, 0x43
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.SetRegisterForTesting(1, 0b1000);
        cpu.SetRegisterForTesting(2, 0b0011);

        cpu.Step();

        Assert.Equal(0b1011u, cpu.GetRegister(1));
        Assert.False(cpu.ZeroFlagSet);
    }

    [Fact]
    public void ThumbCmpImmediate_SetsZeroFlagWhenEqual()
    {
        byte[] rom =
        [
            0x00, 0x29
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(1, 0);

        cpu.Step();

        Assert.True(cpu.ZeroFlagSet);
        Assert.True(cpu.CarryFlagSet);
        Assert.False(cpu.NegativeFlagSet);
    }

    [Fact]
    public void ThumbCmpImmediate_ClearsZeroFlagWhenDifferent()
    {
        byte[] rom =
        [
            0x00, 0x29
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetRegisterForTesting(1, 5);

        cpu.Step();

        Assert.False(cpu.ZeroFlagSet);
        Assert.True(cpu.CarryFlagSet);
    }

    [Fact]
    public void ThumbConditionalBranch_BeqBranchesWhenZeroFlagSet()
    {
        byte[] rom =
        [
            0x02, 0xD0
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetZeroFlagForTesting(true);

        cpu.Step();

        Assert.Equal(0x08000006u, cpu.Pc);
    }

    [Fact]
    public void ThumbConditionalBranch_BeqDoesNotBranchWhenZeroFlagClear()
    {
        byte[] rom =
        [
            0x02, 0xD0
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);
        cpu.SetZeroFlagForTesting(false);

        cpu.Step();

        Assert.Equal(0x08000002u, cpu.Pc);
    }

    [Fact]
    public void ThumbUnconditionalBranch_BranchesForward()
    {
        byte[] rom =
        [
            0x09, 0xE0
        ];

        Arm7tdmiCpu cpu = CreateCpu(rom);
        cpu.SetThumbStateForTesting(true);

        cpu.Step();

        Assert.Equal(0x08000014u, cpu.Pc);
    }

    private static Arm7tdmiCpu CreateCpu(byte[] rom)
    {
        MemoryBus bus = new MemoryBus(rom);
        return new Arm7tdmiCpu(bus);
    }
}