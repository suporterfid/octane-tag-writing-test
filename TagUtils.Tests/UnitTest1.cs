using NUnit.Framework;
using Impinj.TagUtils;
using OctaneTagWritingTest.Helpers;


namespace TagUtils.Tests;

public class Sgtin96Tests
{
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
        Console.WriteLine("EPC Hex: " + epcHex.ToUpper());

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

        Console.WriteLine("serial: " + serial);
        Console.WriteLine("originalGtin: " + originalGtin);
        Console.WriteLine(" decodedGtin: " + tagDataKey);
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

        Console.WriteLine($"TID M730: {tid}");

        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser. Get40BitSerialDecimal();
            Console.WriteLine($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        Console.WriteLine("EPC Hex: " + epcHex.ToUpper());

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

        Console.WriteLine("serial: " + serial);
        Console.WriteLine("originalGtin: " + originalGtin);
        Console.WriteLine(" decodedGtin: " + tagDataKey);
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

        Console.WriteLine($"TID r6: {tid}");

        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser.Get40BitSerialDecimal();
            Console.WriteLine($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        Console.WriteLine("EPC Hex: " + epcHex.ToUpper());

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

        Console.WriteLine("serial: " + serial);
        Console.WriteLine("originalGtin: " + originalGtin);
        Console.WriteLine(" decodedGtin: " + tagDataKey);
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

        Console.WriteLine($"TID U9: {tid}");
        using (var parser = new TagTidParser(tid))
        {
            string tidSuffix = parser.Get40BitSerialHex();
            serial = parser.Get40BitSerialDecimal();
            Console.WriteLine($"Serial extraído: {tidSuffix} = {serial}");
        }

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + serial;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);
        // print epcHex
        Console.WriteLine("EPC Hex: " + epcHex.ToUpper());

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

        Console.WriteLine("serial: " + serial);
        Console.WriteLine("originalGtin: " + originalGtin);
        Console.WriteLine(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }

    [Test]
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
        Console.WriteLine("EPC Hex: " + sgtin96Hex.ToUpper());

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

        Console.WriteLine("serial: " + serial);
        Console.WriteLine("originalGtin: " + originalGtin);
        Console.WriteLine(" decodedGtin: " + tagDataKey);
        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }
}
