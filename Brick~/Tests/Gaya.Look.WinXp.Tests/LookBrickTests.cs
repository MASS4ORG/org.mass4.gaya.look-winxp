using Gaya.Host;
using Gaya.Packages;
using Gaya.Packages.Editor;
using Gaya.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gaya.Look.Tests;

/// <summary>Checks this look brick: it verifies, applies with its own theme and is offered as a look.</summary>
public sealed class LookBrickTests
{
    const string Id = "org.mass4.gaya.look-winxp";
    const string Look = "org.mass4.look.winxp";
    const string DarkTheme = "org.mass4.theme.luna";

    static readonly string Folder = Find();

    /// <summary>The brick passes <c>theme verify</c> with no errors.</summary>
    [Fact]
    public void BrickVerifiesWithoutErrors()
    {
        var findings = ThemeVerifier.Verify(Folder);

        Assert.DoesNotContain(findings, static finding => finding.Severity == ThemeFindingSeverity.Error);
    }

    /// <summary>The look applies as the base sheet and pairs with its own dark and light themes.</summary>
    [Fact]
    public void LookAppliesAndPairsWithItsThemes()
    {
        var service = new ThemeService(NullLogger.Instance,
            new ThemeCatalog(NullLogger.Instance, Path.Combine(Path.GetTempPath(), $"no-themes-{Guid.NewGuid():N}")),
            Path.Combine(Path.GetTempPath(), $"no-user-{Guid.NewGuid():N}.pss"));
        service.SetBrickFolders([Path.Combine(Folder, "Themes")]);

        try
        {
            service.ApplyLook(Look);

            Assert.NotNull(service.Look);
            Assert.Null(service.Diagnostic);
            Assert.Equal(DarkTheme, service.DefaultColorThemeFor(Look));
            Assert.All(service.Looks.Concat(service.ColorThemes), static info => Assert.NotNull(info));
        }
        finally
        {
            ThemeTokens.Current = ThemeTokens.Default;
        }
    }

    /// <summary>The studio's catalog lists the brick under looks, and adding it needs no source.</summary>
    [Fact]
    public void StudioCatalogOffersTheBrickAsALook()
    {
        var packages = Directory.CreateTempSubdirectory("gaya-look-packages-").FullName;
        try
        {
            var copy = Path.Combine(packages, Id);
            Directory.CreateDirectory(Path.Combine(copy, "Themes"));
            File.Copy(Path.Combine(Folder, PackageManifest.FileName), Path.Combine(copy, PackageManifest.FileName));
            foreach (var sheet in Directory.GetFiles(Path.Combine(Folder, "Themes")))
            {
                File.Copy(sheet, Path.Combine(copy, "Themes", Path.GetFileName(sheet)));
            }

            var studio = new StudioBricks(Path.Combine(Path.GetTempPath(), $"studio-{Guid.NewGuid():N}"),
                PackagedPlugins.DefaultHosts, builtinDirectory: packages);
            var catalog = BrickCatalog.Local([], studio.BuiltinDirectory, null, studio.ReservedCategoryPrefixes,
                PackageScope.Studio);

            var looks = BrickCatalog.Filter(catalog, BrickFilter.BuiltIn, null, BrickCategoryFilter.Looks);

            Assert.Equal([Id], looks.Select(static b => b.Id));
        }
        finally
        {
            Directory.Delete(packages, recursive: true);
        }
    }

    static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var manifest = Path.Combine(directory.FullName, PackageManifest.FileName);
            if (File.Exists(manifest) && File.ReadAllText(manifest).Contains($"\"{Id}\"", StringComparison.Ordinal))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"The {Id} checkout is not an ancestor of {AppContext.BaseDirectory}.");
    }
}
