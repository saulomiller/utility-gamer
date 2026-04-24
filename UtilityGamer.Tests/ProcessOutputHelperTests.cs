using System.Globalization;
using System.Text;
using UtilityGamer.WinUI.Helpers;

namespace UtilityGamer.Tests;

[TestClass]
public sealed class ProcessOutputHelperTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
        => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [TestMethod]
    public void Decode_PrefersUtf8_WhenProcessOutputIsUtf8()
    {
        var expected = "GUID: 11111111-1111-1111-1111-111111111111 (Desempenho M\u00E1ximo)";
        var bytes = Encoding.UTF8.GetBytes(expected);

        var decoded = ProcessOutputHelper.Decode(bytes);

        Assert.AreEqual(expected, decoded);
    }

    [TestMethod]
    public void Decode_FallsBackToOemCodePage_WhenUtf8IsInvalid()
    {
        var expected = "Desempenho M\u00E1ximo";
        var oemEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var bytes = oemEncoding.GetBytes(expected);

        var decoded = ProcessOutputHelper.Decode(bytes);

        Assert.AreEqual(expected, decoded);
    }
}
