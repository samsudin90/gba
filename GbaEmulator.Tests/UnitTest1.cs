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

    private static Arm7tdmiCpu CreateCpu(byte[] rom)
    {
        MemoryBus bus = new MemoryBus(rom);
        return new Arm7tdmiCpu(bus);
    }
}