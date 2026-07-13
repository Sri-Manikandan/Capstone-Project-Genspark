using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EMSTests.Configuration;

[TestFixture]
public class WorkersConfigurationTests
{
    private static IConfiguration Build(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Test]
    public void WorkersEnabled_ShouldDefaultToTrue_WhenKeyAbsent()
    {
        var config = Build();

        config.GetValue("Workers:Enabled", true).Should().BeTrue();
    }

    [Test]
    public void WorkersEnabled_ShouldBeFalse_WhenSetToFalse()
    {
        var config = Build(("Workers:Enabled", "false"));

        config.GetValue("Workers:Enabled", true).Should().BeFalse();
    }

    [Test]
    public void WorkersEnabled_ShouldBeTrue_WhenSetToTrue()
    {
        var config = Build(("Workers:Enabled", "true"));

        config.GetValue("Workers:Enabled", false).Should().BeTrue();
    }
}
