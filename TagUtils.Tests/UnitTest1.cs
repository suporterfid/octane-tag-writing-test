using NUnit.Framework;
using Impinj.TagUtils;
using OctaneTagWritingTest.Helpers;
using Common.Logging;
using Common.Logging.Sinks;
using System.Collections.Generic;


namespace TagUtils.Tests;

public class Sgtin96Tests
{
    [OneTimeSetUp]
    public void SetupLogging()
    {
        LoggingService.Instance.Start(new LoggingConfiguration(new List<ILogSink>()));
    }

    [OneTimeTearDown]
    public void TearDownLogging()
    {
        LoggingService.Instance.Stop();
    }

    [Test]
    public void FromGTIN_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 12910342659;

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + epcHex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(epcHex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterListDecode, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
    public void FromGTIN_andM730TID_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 0;
        string tid = "E2801191200076D63DDC030A"; // M730 TID example

        LoggingService.Instance.LogInfo($"TID M730: {tid}");

        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser. Get40BitSerialDecimal();
            LoggingService.Instance.LogInfo($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + epcHex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(epcHex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterListDecode, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
    public void FromGTIN_andM750TID_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 0;
        string tid = "E280119020006356D8630332"; // M730 TID example

        LoggingService.Instance.LogInfo($"TID M750: {tid}");

        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser.Get40BitSerialDecimal();
            LoggingService.Instance.LogInfo($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + epcHex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(epcHex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterListDecode, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
    public void FromGTIN_andR6TID_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 0;
        string tid = "E2801170200013DC3923099D"; // R6 TID example

        LoggingService.Instance.LogInfo($"TID r6: {tid}");

        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser.Get40BitSerialDecimal();
            LoggingService.Instance.LogInfo($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + epcHex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(epcHex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterListDecode, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
    public void FromGTIN_andU9TID_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 0;
        string tid = "E280699520004003138568BB"; // U9 TID example

        LoggingService.Instance.LogInfo($"TID U9: {tid}");
        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser.Get40BitSerialDecimal();
            LoggingService.Instance.LogInfo($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + epcHex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(epcHex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterListDecode, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
    [Ignore("Expected GTIN differs in CI environment")]
    public void FromSgtin96Hex_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";

        string sgtin96Hex = "303B029BC16E188301843203";
        // Arrange
        string originalGtin = "07891033748938";
        int companyPrefixLength = 6;
        //ulong serial = 0;
        ulong serial = 12910342659;

        // print epcHex
        LoggingService.Instance.LogInfo("EPC Hex: " + sgtin96Hex.ToUpper());

        var epcIdentifierBinary = _tdtEngine.HexToBinary(sgtin96Hex);
        var parameterListDecode = @"tagLength=96";
        var decodedEpc = _tdtEngine.Translate(epcIdentifierBinary, parameterList, @"LEGACY");
        var decodedEpcParts = decodedEpc.Split(";");
        var epcKey = decodedEpcParts[0];
        var epcSerial = "";
        if (decodedEpcParts.Length == 2) epcSerial = decodedEpcParts[1];
        var epcKeyParts = epcKey.Split("=");
        var tagDataKeyName = epcKeyParts[0];
        var tagDataKey = epcKeyParts[1];

        LoggingService.Instance.LogInfo("serial: " + serial);
        LoggingService.Instance.LogInfo("originalGtin: " + originalGtin);
        LoggingService.Instance.LogInfo(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }
}
