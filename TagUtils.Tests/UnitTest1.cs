using NUnit.Framework;
using Impinj.TagUtils;

namespace TagUtils.Tests;

public class Sgtin96Tests
{
    [Test]
    public void FromGTIN_ShouldPreserveOriginalGTIN_WhenDecodedBack()
    {
        // Arrange
        string originalGtin = "07891033586424";
        int companyPrefixLength = 6;

        // Act
        var sgtin = Sgtin96.FromGTIN(originalGtin, companyPrefixLength);
        sgtin.SerialNumber = 0;
        string epcHex = sgtin.ToEpc();
        var decodedSgtin = Sgtin96.FromString(epcHex);
        string reconstructedGtin = "0" + decodedSgtin.ToUpc();

        // Assert
        Assert.AreEqual(originalGtin, reconstructedGtin);
    }
}
