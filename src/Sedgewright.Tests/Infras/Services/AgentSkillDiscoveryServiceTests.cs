using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sedgewright.Infras.Services;
using Sedgewright.Models;

namespace Sedgewright.Tests.Infras.Services;

[TestFixture]
public sealed class AgentSkillDiscoveryServiceTests
{
    [Test]
    public void GetCommonSkillSources_ReturnsOnlyExistingProfileLocations()
    {
        string profile = CreateTemporaryDirectory();
        try
        {
            string agentsSkills = Path.Combine(profile, ".agents", "skills");
            string claudeSkills = Path.Combine(profile, ".claude", "skills");
            string codexSkills = Path.Combine(profile, ".codex", "skills");
            string copilotSkills = Path.Combine(profile, ".copilot", "skills");
            string openCodeSkills = Path.Combine(profile, ".config", "opencode", "skills");
            Directory.CreateDirectory(agentsSkills);
            Directory.CreateDirectory(claudeSkills);
            Directory.CreateDirectory(codexSkills);
            Directory.CreateDirectory(copilotSkills);
            Directory.CreateDirectory(openCodeSkills);

            AgentSkillDiscoveryService service = new(profile);
            IReadOnlyList<AgentSkillSource> sources = service.GetCommonSkillSources();

            Assert.That(sources, Is.EqualTo(new[]
            {
                new AgentSkillSource(".agents", agentsSkills),
                new AgentSkillSource(".copilot", copilotSkills),
                new AgentSkillSource(".claude", claudeSkills),
                new AgentSkillSource(".codex", codexSkills),
                new AgentSkillSource("OpenCode", openCodeSkills),
            }));
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }

    [Test]
    public void Discover_ReturnsNestedSkillFoldersWithRelativePaths()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "beta"));
            File.WriteAllText(Path.Combine(root, "beta", "SKILL.md"), "# Beta");

            Directory.CreateDirectory(Path.Combine(root, "Alpha"));
            File.WriteAllText(Path.Combine(root, "Alpha", "skill.md"), "# Alpha");

            Directory.CreateDirectory(Path.Combine(root, "ignored"));
            Directory.CreateDirectory(Path.Combine(root, "ignored", "nested"));
            File.WriteAllText(Path.Combine(root, "ignored", "nested", "SKILL.md"), "# Nested");

            AgentSkillDiscoveryService service = new();
            var result = service.Discover(root);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Select(skill => skill.Name), Is.EqualTo(["Alpha", "beta", "nested"]));
                Assert.That(result.Value.Single(skill => skill.Name == "nested").RelativePath,
                    Is.EqualTo(Path.Combine("ignored", "nested")));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Discover_IncludesSkillsAtDepthEightButNotDepthNine()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string current = root;
            for (int depth = 1; depth <= AgentSkillDiscoveryService.MaximumSkillSearchDepth + 1; depth++)
            {
                current = Path.Combine(current, $"level-{depth}");
                Directory.CreateDirectory(current);
                if (depth is AgentSkillDiscoveryService.MaximumSkillSearchDepth
                    or AgentSkillDiscoveryService.MaximumSkillSearchDepth + 1)
                {
                    File.WriteAllText(Path.Combine(current, "SKILL.md"), $"# Depth {depth}");
                }
            }

            AgentSkillDiscoveryService service = new();
            var result = service.Discover(root);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Has.Count.EqualTo(1));
                Assert.That(result.Value[0].RelativePath,
                    Is.EqualTo(Path.Combine(Enumerable.Range(1, 8).Select(depth => $"level-{depth}").ToArray())));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Discover_ReturnsFailureForMissingDirectory()
    {
        AgentSkillDiscoveryService service = new();

        var result = service.Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.That(result.IsFailure, Is.True);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sentinel-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
