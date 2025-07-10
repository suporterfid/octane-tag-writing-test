using NUnit.Framework;
using Impinj.TagUtils;


namespace TagUtils.Tests;

public class Sgtin96Tests
{
    [Test]
    public void FromGTIN_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        TDTEngine _tdtEngine = new();
        // Arrange
        string originalGtin = "07891033586424";
        int companyPrefixLength = 6;

        // Act
        string epcIdentifier = @"gtin=" + originalGtin + ";serial=" + 0;
        string parameterList = @"filter=1;gs1companyprefixlength=6;tagLength=96";
        string binary = _tdtEngine.Translate(epcIdentifier, parameterList, @"BINARY");
        string epcHex = _tdtEngine.BinaryToHex(binary);

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

        // Assert
        Assert.AreEqual(originalGtin, tagDataKey);
    }
}
