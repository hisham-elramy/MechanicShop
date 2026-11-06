using MechanicShop.Application.Common.Interfaces;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public class UtilityServiceTests
{
    [Theory]
    [InlineData("john@example.com", "j****n@example.com")]
    [InlineData("ab@x.com", "a****b@x.com")]
    [InlineData("a@x.com", "****@x.com")]
    [InlineData("@x.com", "****@x.com")]
    public void MaskEmail_ShouldMaskTheLocalPart(string email, string expected)
    {
        // Arrange & Act & Assert
        Assert.Equal(expected, UtilityService.MaskEmail(email));
    }

    [Fact]
    public void MaskEmail_ShouldKeepTheDomain()
    {
        // Arrange & Act & Assert
        Assert.EndsWith("@example.com", UtilityService.MaskEmail("someone@example.com"));
    }

    [Fact]
    public void MaskEmail_ShouldNotRevealTheMiddleOfTheName()
    {
        // Arrange & Act & Assert
        Assert.DoesNotContain("ohn", UtilityService.MaskEmail("john@example.com"));
    }

    [Fact]
    public void MaskEmail_ShouldNotThrow_WhenThereIsNoAtSign()
    {
        // Arrange & Act
        var result = Record.Exception(() => UtilityService.MaskEmail("not-an-email"));

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void MaskEmail_ShouldThrow_WhenEmailIsNull()
    {
        // Arrange & Act & Assert
        Assert.ThrowsAny<Exception>(() => UtilityService.MaskEmail(null!));
    }
}