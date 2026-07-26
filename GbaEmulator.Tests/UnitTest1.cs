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

    private static Arm7tdmiCpu CreateCpu(byte[] rom)
    {
        MemoryBus bus = new MemoryBus(rom);
        return new Arm7tdmiCpu(bus);
    }
}