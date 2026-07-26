using GymMaster.API.Common;
using Xunit;

namespace GymMaster.Api.Tests;

// Phu MOI nhanh cua PersonValidation (mot nguon su that cho validate thong tin ca nhan).
public class PersonValidationTests
{
    [Fact]
    public void Valid_input_returns_null()
    {
        var error = PersonValidation.Validate(
            fullName: "Nguyen Van A",
            phone: "0900000000",
            dateOfBirth: new DateTime(1995, 5, 5),
            gender: "male",
            address: "1 Ly Thuong Kiet",
            emergencyContact: "0911000000",
            specialty: "Yoga",
            bio: "PT co kinh nghiem");

        Assert.Null(error);
    }

    [Fact]
    public void All_null_returns_null()
    {
        Assert.Null(PersonValidation.Validate());
    }

    [Fact]
    public void FullName_too_long()
        => Assert.NotNull(PersonValidation.Validate(fullName: new string('a', 151)));

    [Fact]
    public void Phone_too_long()
        => Assert.NotNull(PersonValidation.Validate(phone: new string('9', 31)));

    [Fact]
    public void DateOfBirth_in_future()
        => Assert.NotNull(PersonValidation.Validate(dateOfBirth: DateTime.UtcNow.AddDays(1)));

    [Fact]
    public void DateOfBirth_before_1900()
        => Assert.NotNull(PersonValidation.Validate(dateOfBirth: new DateTime(1899, 12, 31)));

    [Fact]
    public void Gender_too_long()
        => Assert.NotNull(PersonValidation.Validate(gender: new string('x', 21)));

    [Fact]
    public void Address_too_long()
        => Assert.NotNull(PersonValidation.Validate(address: new string('x', 256)));

    [Fact]
    public void EmergencyContact_too_long()
        => Assert.NotNull(PersonValidation.Validate(emergencyContact: new string('x', 101)));

    [Fact]
    public void Specialty_too_long()
        => Assert.NotNull(PersonValidation.Validate(specialty: new string('x', 151)));

    [Fact]
    public void Bio_too_long()
        => Assert.NotNull(PersonValidation.Validate(bio: new string('x', 1001)));

    [Theory] // Gia tri o dung bien tren -> hop le (khong loi)
    [InlineData(150)]
    [InlineData(1)]
    public void FullName_at_or_below_limit_is_valid(int length)
        => Assert.Null(PersonValidation.Validate(fullName: new string('a', length)));
}
