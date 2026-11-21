using BarberBook.Web.Common;
using FluentAssertions;
using Xunit;

namespace BarberBook.Tests;

public class OfflinePlaceholderImageTests
{
    [Theory]
    [InlineData("Anton", "/images/barbers/anton.jpg")]
    [InlineData("Carlos", "/images/barbers/carlos.jpg")]
    [InlineData("Danilo", "/images/barbers/danilo.jpg")]
    [InlineData("Dennis", "/images/barbers/dennis.jpg")]
    [InlineData("Miguel", "/images/barbers/miguel.jpg")]
    [InlineData("Rafael", "/images/barbers/rafael.jpg")]
    [InlineData("Ramon", "/images/barbers/ramon.jpg")]
    [InlineData("Vicente", "/images/barbers/vicente.jpg")]
    public void GetBarberImage_WithKnownCraftsman_ReturnsLocalOfflineJpg(string barberName, string expectedPath)
    {
        var result = ImageHelpers.GetBarberImage(barberName);
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetBarberImage_WithNullOrUnknown_ReturnsDefaultBarberJpg()
    {
        ImageHelpers.GetBarberImage(null).Should().Be("/images/barbers/default-barber.jpg");
        ImageHelpers.GetBarberImage("").Should().Be("/images/barbers/default-barber.jpg");
        ImageHelpers.GetBarberImage("Unknown Stylist").Should().Be("/images/barbers/default-barber.jpg");
    }

    [Fact]
    public void GetBarberImage_WithExistingLocalUrl_PreservesLocalUrl()
    {
        var result = ImageHelpers.GetBarberImage("Anton", "/uploads/custom-anton.jpg");
        result.Should().Be("/uploads/custom-anton.jpg");
    }

    [Theory]
    [InlineData("Haircut", "/images/services/haircut.jpg")]
    [InlineData("Gentleman's Cut", "/images/services/haircut.jpg")]
    [InlineData("Haircut + Shave", "/images/services/haircut-shave.jpg")]
    [InlineData("Beard Trim & Lineup", "/images/services/beard-trim.jpg")]
    [InlineData("Hair Color & Treatment", "/images/services/hair-color.jpg")]
    public void GetServiceImage_WithGroomingServices_ReturnsMatchingOfflinePhotography(string serviceName, string expectedPath)
    {
        var result = ImageHelpers.GetServiceImage(serviceName);
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetServiceImage_WithNullOrEmpty_ReturnsDefaultServiceJpg()
    {
        ImageHelpers.GetServiceImage(null).Should().Be("/images/services/default-service.jpg");
        ImageHelpers.GetServiceImage("").Should().Be("/images/services/default-service.jpg");
    }

    [Theory]
    [InlineData("PRD-POM-01", "Classic High-Shine Pomade (100g)", "Styling", "/images/products/pomade.jpg")]
    [InlineData("PRD-BOIL-02", "Artisanal Cedarwood Beard Oil (50ml)", "Beard Care", "/images/products/beard-oil.jpg")]
    [InlineData("PRD-CLAY-03", "Matte Texture Clay (85g)", "Styling", "/images/products/matte-clay.jpg")]
    [InlineData("PRD-SHAV-04", "Eucalyptus Hot Towel Shave Cream (150ml)", "Shaving", "/images/products/shave-cream.jpg")]
    [InlineData("PRD-TON-05", "Bay Rum Soothing Aftershave Tonic (120ml)", "Aftercare", "/images/products/aftershave-tonic.jpg")]
    public void GetProductImage_WithApothecaryCatalog_ReturnsMatchingOfflineProductJpg(string sku, string name, string category, string expectedPath)
    {
        var result = ImageHelpers.GetProductImage(sku, name, category);
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetProductImage_WithUnknownItem_ReturnsDefaultProductJpg()
    {
        ImageHelpers.GetProductImage(null).Should().Be("/images/products/default-product.jpg");
        ImageHelpers.GetProductImage("SKU-XYZ", "Special Comb", "Accessories").Should().Be("/images/products/default-product.jpg");
    }

    [Theory]
    [InlineData("Manila Main Branch", "/images/branches/manila-main.jpg")]
    [InlineData("BGC High Street Branch", "/images/branches/bgc-branch.jpg")]
    [InlineData("Makati Poblacion Branch", "/images/branches/makati-branch.jpg")]
    [InlineData("Ortigas Center Branch", "/images/branches/ortigas-branch.jpg")]
    [InlineData("QC Tomas Morato Branch", "/images/branches/qc-branch.jpg")]
    public void GetBranchImage_WithShopBranches_ReturnsMatchingOfflineParlorPhotography(string branchName, string expectedPath)
    {
        var result = ImageHelpers.GetBranchImage(branchName);
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GetBranchImage_WithNullOrUnknown_ReturnsDefaultBranchJpg()
    {
        ImageHelpers.GetBranchImage(null).Should().Be("/images/branches/default-branch.jpg");
        ImageHelpers.GetBranchImage("Unknown Branch").Should().Be("/images/branches/default-branch.jpg");
    }
}
