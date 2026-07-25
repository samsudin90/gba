using System.Text;

namespace GbaEmulator.Core;

public sealed class GbaRomHeader
{
    public const int HeaderSize = 0xC0;

    public string GameTitle { get; }
    public string GameCode { get; }
    public string MakerCode { get; }
    public byte FixedValue { get; }
    public byte HeaderChecksum { get; }
    public bool HasValidFixedValue => FixedValue == 0x96;
    public bool HasValidHeaderChecksum { get; }

    private GbaRomHeader(
        string gameTitle,
        string gameCode,
        string makerCode,
        byte fixedValue,
        byte headerChecksum,
        bool hasValidHeaderChecksum)
    {
        GameTitle = gameTitle;
        GameCode = gameCode;
        MakerCode = makerCode;
        FixedValue = fixedValue;
        HeaderChecksum = headerChecksum;
        HasValidHeaderChecksum = hasValidHeaderChecksum;
    }

    public static GbaRomHeader Parse(ReadOnlySpan<byte> rom)
    {
        if (rom.Length < HeaderSize)
        {
            throw new ArgumentException("ROM is too small to contain a GBA header.", nameof(rom));
        }

        string gameTitle = ReadAscii(rom.Slice(0xA0, 12));
        string gameCode = ReadAscii(rom.Slice(0xAC, 4));
        string makerCode = ReadAscii(rom.Slice(0xB0, 2));
        byte fixedValue = rom[0xB2];
        byte headerChecksum = rom[0xBD];
        bool hasValidHeaderChecksum = CalculateHeaderChecksum(rom) == headerChecksum;

        return new GbaRomHeader(
            gameTitle,
            gameCode,
            makerCode,
            fixedValue,
            headerChecksum,
            hasValidHeaderChecksum);
    }

    private static string ReadAscii(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
    }

    private static byte CalculateHeaderChecksum(ReadOnlySpan<byte> rom)
    {
        byte checksum = 0;

        for (int address = 0xA0; address <= 0xBC; address++)
        {
            checksum = unchecked((byte)(checksum - rom[address]));
        }

        return unchecked((byte)(checksum - 0x19));
    }
}
