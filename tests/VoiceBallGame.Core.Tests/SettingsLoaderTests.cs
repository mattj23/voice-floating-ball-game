using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Tests;

public class SettingsLoaderTests
{
    [Fact]
    public void CommentsAndTrailingCommasAreAccepted()
    {
        // The settings file documents every parameter inline, so the parser has to tolerate the
        // comments that carry that documentation.
        const string json = """
        {
            // The target ratio
            "goal_ratio": 750, // dB per L/s
            "upper_flow_limit": 0.12,
        }
        """;

        var settings = SettingsLoader.Parse(json);

        Assert.Equal(750, settings.GoalRatio);
        Assert.Equal(0.12, settings.UpperFlowLimit);
    }

    [Fact]
    public void KeysAreReadInSnakeCase()
    {
        const string json = """
        {
            "trial_start_threshold": 0.005,
            "flow_correction_factor": 1.08,
            "legacy_compat_scoring": true,
            "output_directory": "trials"
        }
        """;

        var settings = SettingsLoader.Parse(json);

        Assert.Equal(0.005, settings.TrialStartThreshold);
        Assert.Equal(1.08, settings.FlowCorrectionFactor);
        Assert.True(settings.LegacyCompatScoring);
        Assert.Equal("trials", settings.OutputDirectory);
    }

    [Fact]
    public void MissingKeysKeepTheirDefaults()
    {
        var settings = SettingsLoader.Parse("{ \"goal_ratio\": 900 }");

        Assert.Equal(900, settings.GoalRatio);
        Assert.Equal(new GameSettings().HistoryWindow, settings.HistoryWindow);
        Assert.Equal(new GameSettings().BallColorScale.Length, settings.BallColorScale.Length);
    }

    [Fact]
    public void TheShippedSettingsFileLoads()
    {
        var path = FindShippedSettings();
        var settings = SettingsLoader.Load(path);

        Assert.Equal(800, settings.GoalRatio);
        Assert.Equal(0.1, settings.UpperFlowLimit);
        Assert.Equal(5, settings.BallColorScale.Length);
        Assert.False(settings.LegacyCompatScoring);
    }

    [Fact]
    public void TheShippedColorScaleAgreesWithTheShippedScoringWindow()
    {
        // The settings file promises that the ball is white throughout the scoring window. If
        // someone edits one without the other, this catches it.
        var settings = SettingsLoader.Load(FindShippedSettings());
        var ordered = settings.BallColorScale.OrderBy(k => k.Ratio).ToArray();

        var whiteBand = ordered.Single(k => k.Rgb is [1, 1, 1]);
        var bandBelow = ordered[Array.IndexOf(ordered, whiteBand) - 1];

        Assert.Equal(settings.ScoringRatioMin, bandBelow.Ratio);
        Assert.Equal(settings.ScoringRatioMax, whiteBand.Ratio);
    }

    [Fact]
    public void FlowLimitsInTheWrongOrderAreRejected()
    {
        var error = Assert.Throws<SettingsException>(() =>
            SettingsLoader.Parse("{ \"lower_flow_limit\": 0.2, \"upper_flow_limit\": 0.1 }"));

        Assert.Contains("lower_flow_limit", error.Message);
    }

    [Fact]
    public void OverlappingColorBlendZonesAreRejected()
    {
        // The original settings file warned about this in a comment and then behaved in an
        // undefined way if it happened. Now it is caught, with the largest workable value named.
        var error = Assert.Throws<SettingsException>(() =>
            SettingsLoader.Parse("{ \"color_blend_zone\": 0.5 }"));

        Assert.Contains("overlap", error.Message);
        Assert.Contains("color_blend_zone", error.Message);
    }

    [Theory]
    [InlineData("{ \"goal_ratio\": 0 }", "goal_ratio")]
    [InlineData("{ \"history_window\": 0 }", "history_window")]
    [InlineData("{ \"engine_tick_ms\": -5 }", "engine_tick_ms")]
    [InlineData("{ \"flow_correction_factor\": 0 }", "flow_correction_factor")]
    [InlineData("{ \"scoring_ratio_min\": 1.2 }", "scoring_ratio_min")]
    public void UnusableValuesAreNamedInTheError(string json, string expectedKey)
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse(json));
        Assert.Contains(expectedKey, error.Message);
    }

    [Fact]
    public void AColorKeypointWithTheWrongNumberOfComponentsIsRejected()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""
        { "ball_color_scale": [ { "ratio": 1.0, "rgb": [ 1, 0 ] } ] }
        """));

        Assert.Contains("three rgb values", error.Message);
    }

    [Fact]
    public void AMissingFileSaysWhereItLooked()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-such-settings-file.json");

        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Load(path));

        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void MalformedJsonIsReportedAgainstTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ \"goal_ratio\": }");

        try
        {
            var error = Assert.Throws<SettingsException>(() => SettingsLoader.Load(path));
            Assert.Contains("not valid JSON", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindShippedSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "config", "app_settings.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find config/app_settings.json above the test output directory.");
    }
}
