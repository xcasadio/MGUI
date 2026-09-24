using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Every sample's markup is loaded at startup from an embedded resource (<c>SampleBase</c> reads
/// <c>MGUI.Samples.&lt;folder&gt;.&lt;name&gt;.xaml</c>), and <c>MGUI.Samples.csproj</c> lists those resources one by
/// one. A <c>.xaml</c> file left out of that list still builds, then crashes the whole samples application when the
/// compendium constructs its sample (it happened to <c>Features\BoundImages.xaml</c>). MGUI.Tests carries no reference
/// to MGUI.Samples, so this reads the project file and the folder from disk.</summary>
public class SampleXamlEmbeddingTests
{
    [Fact]
    public void EveryXamlFileOfTheSamples_IsAnEmbeddedResource()
    {
        string samplesRoot = TestRepository.Combine("MGUI.Samples");
        var embedded = XDocument.Load(Path.Combine(samplesRoot, "MGUI.Samples.csproj"))
            .Descendants()
            .Where(element => element.Name.LocalName == "EmbeddedResource")
            .Select(element => (string)element.Attribute("Include"))
            .Where(include => include != null)
            .Select(include => include.Replace('/', '\\'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notEmbedded = Directory.EnumerateFiles(samplesRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(samplesRoot, path).Replace('/', '\\'))
            .Where(relative => !relative.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase)
                && !relative.StartsWith("obj\\", StringComparison.OrdinalIgnoreCase))
            .Where(relative => !embedded.Contains(relative))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();

        Assert.True(notEmbedded.Count == 0,
            "MGUI.Samples.csproj does not embed: " + string.Join(", ", notEmbedded));
    }
}
