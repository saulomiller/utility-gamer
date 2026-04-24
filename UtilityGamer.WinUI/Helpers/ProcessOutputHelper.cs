using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UtilityGamer.WinUI.Helpers;

internal static class ProcessOutputHelper
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding LenientUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding StrictOemEncoding;
    private static readonly Encoding StrictDefaultEncoding;

    static ProcessOutputHelper()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        StrictOemEncoding = CreateStrictEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        StrictDefaultEncoding = CreateStrictEncoding(Encoding.Default.CodePage);
    }

    public static async Task<string> ReadCombinedOutputAsync(Process process)
    {
        var stdoutTask = ReadAllBytesAsync(process.StandardOutput.BaseStream);
        var stderrTask = ReadAllBytesAsync(process.StandardError.BaseStream);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());

        return JoinOutput(
            Decode(stdoutTask.Result),
            Decode(stderrTask.Result));
    }

    internal static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        if (TryDecode(bytes, StrictUtf8, out var utf8Text))
            return Normalize(utf8Text);

        if (TryDecode(bytes, StrictOemEncoding, out var oemText))
            return Normalize(oemText);

        if (StrictDefaultEncoding.CodePage != StrictOemEncoding.CodePage &&
            TryDecode(bytes, StrictDefaultEncoding, out var defaultText))
        {
            return Normalize(defaultText);
        }

        return Normalize(LenientUtf8.GetString(bytes));
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static Encoding CreateStrictEncoding(int codePage) => Encoding.GetEncoding(
        codePage,
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    private static bool TryDecode(byte[] bytes, Encoding encoding, out string decoded)
    {
        try
        {
            decoded = encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static string Normalize(string value) => value
        .TrimStart('\uFEFF')
        .Trim();

    private static string JoinOutput(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return stderr;

        if (string.IsNullOrWhiteSpace(stderr))
            return stdout;

        return $"{stdout}{Environment.NewLine}{stderr}";
    }
}
