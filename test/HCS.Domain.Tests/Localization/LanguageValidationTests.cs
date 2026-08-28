using System;
using HCS.Localization;
using Shouldly;
using Xunit;

namespace HCS.LocalizationTests;

public class LanguageValidationTests
{
    [Fact]
    public void Should_Reject_Empty_Culture_Name()
    {
        Should.Throw<ArgumentException>(() => new Language(Guid.NewGuid(), " ", "Invalid"));
    }

    [Fact]
    public void Should_Reject_Overlong_Translation()
    {
        Should.Throw<ArgumentException>(() => new LanguageText(
            Guid.NewGuid(), "HCS", "en", "Greeting", new string('x', LanguageConsts.MaxTextValueLength + 1)));
    }
}
