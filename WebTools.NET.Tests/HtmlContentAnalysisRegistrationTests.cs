using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using WebTools.NET.ContentAnalysis.Abstractions;
using WebTools.NET.ContentAnalysis.Analysis;

using Xunit;

namespace WebTools.NET.Tests;

public class HtmlContentAnalysisRegistrationTests
{
    [Fact]
    public void AddWebToolsCore_RegistersHtmlContentAnalyzerAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWebToolsCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var first = provider.GetRequiredService<IHtmlContentAnalyzer>();
        var second = provider.GetRequiredService<IHtmlContentAnalyzer>();

        // Assert
        first.Should().BeOfType<HtmlContentAnalyzer>();
        second.Should().BeSameAs(first);
    }
}
