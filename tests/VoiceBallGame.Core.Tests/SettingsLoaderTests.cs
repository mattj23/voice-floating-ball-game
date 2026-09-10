using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.Core.Tests;

public class SettingsLoaderTests
{
    [Fact]
    public void CommentsAreAccepted()
    {
        // TOML comments let the settings file document every parameter inline.
        const string toml = """
        # The target ratio
        goal_ratio = 750        # dB per L/s
        upper_flow_limit = 0.12
        """;

        var settings = SettingsLoader.Parse(toml);

        Assert.Equal(750, settings.GoalRatio);
        Assert.Equal(0.12, settings.UpperFlowLimit);
    }

    [Fact]
    public void WholeNumbersAreAcceptedWhereADecimalIsExpected()
    {
        // TOML separates integers from floats. The loader gives "goal_ratio = 800" the same
        // meaning as 800.0.
        var settings = SettingsLoader.Parse("goal_ratio = 800\nball_size = 30");

        Assert.Equal(800.0, settings.GoalRatio);
        Assert.Equal(30.0, settings.BallSize);
    }

    [Fact]
    public void EveryKindOfSettingIsRead()
    {
        const string toml = """
        trial_start_threshold = 0.005
        flow_correction_factor = 1.08
        legacy_compat_scoring = true
        output_directory = "trials"
        history_window = 7
        """;

        var settings = SettingsLoader.Parse(toml);

        Assert.Equal(0.005, settings.TrialStartThreshold);
        Assert.Equal(1.08, settings.FlowCorrectionFactor);
        Assert.True(settings.LegacyCompatScoring);
        Assert.Equal("trials", settings.OutputDirectory);
        Assert.Equal(7, settings.HistoryWindow);
    }

    [Fact]
    public void MissingKeysKeepTheirDefaults()
    {
        var settings = SettingsLoader.Parse("goal_ratio = 900");

        Assert.Equal(900, settings.GoalRatio);
        Assert.Equal(new GameSettings().HistoryWindow, settings.HistoryWindow);
        Assert.Equal(new GameSettings().BallColorScale.Length, settings.BallColorScale.Length);
    }

    [Fact]
    public void TheColorScaleIsReadFromItsSections()
    {
        const string toml = """
        color_blend_zone = 0.01

        [[ball_color_scale]]
        ratio = 0.95
        rgb = [0.5, 0.5, 1.0]

        [[ball_color_scale]]
        ratio = 1.05
        rgb = [1, 1, 1]
        """;

        var settings = SettingsLoader.Parse(toml);

        Assert.Equal(2, settings.BallColorScale.Length);
        Assert.Equal(0.95, settings.BallColorScale[0].Ratio);
        Assert.Equal([0.5, 0.5, 1.0], settings.BallColorScale[0].Rgb);

        // Whole numbers in the rgb list are also accepted as decimal values.
        Assert.Equal([1.0, 1.0, 1.0], settings.BallColorScale[1].Rgb);
    }

    [Fact]
    public void ReplayProvidersAreReadFromTheirSections()
    {
        const string toml = """
        [[replay_providers]]
        name = "Pilot participant"
        flow_file = "recordings/flow.jsonl"
        volume_file = "recordings/volume.jsonl"
        """;

        var provider = Assert.Single(SettingsLoader.Parse(toml).ReplayProviders);

        Assert.Equal("Pilot participant", provider.Name);
        Assert.Equal("recordings/flow.jsonl", provider.FlowFile);
        Assert.Equal("recordings/volume.jsonl", provider.VolumeFile);
    }

    [Fact]
    public void TheShippedSettingsFileLoads()
    {
        var settings = SettingsLoader.Load(FindShippedSettings());

        Assert.Equal(800, settings.GoalRatio);
        Assert.Equal(0.1, settings.UpperFlowLimit);
        Assert.Equal(5, settings.BallColorScale.Length);
        Assert.False(settings.LegacyCompatScoring);
        Assert.Equal("data", settings.OutputDirectory);
    }

    [Fact]
    public void TheShippedColorScaleAgreesWithTheShippedScoringWindow()
    {
        // The settings file specifies that the ball is white throughout the scoring window. If
        // someone edits one without the other, this catches it.
        var settings = SettingsLoader.Load(FindShippedSettings());
        var ordered = settings.BallColorScale.OrderBy(k => k.Ratio).ToArray();

        var whiteBand = ordered.Single(k => k.Rgb is [1, 1, 1]);
        var bandBelow = ordered[Array.IndexOf(ordered, whiteBand) - 1];

        Assert.Equal(settings.ScoringRatioMin, bandBelow.Ratio);
        Assert.Equal(settings.ScoringRatioMax, whiteBand.Ratio);
    }

    // ---- Invalid settings -----------------------------------------------------------------

    [Fact]
    public void AMisspelledSettingIsRefusedRatherThanIgnored()
    {
        // Manual mapping detects this key. Automatic binding would ignore it and silently use the
        // default target ratio for the session.
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("goal_ration = 900"));

        Assert.Contains("goal_ration", error.Message);
        Assert.Contains("not a recognized setting", error.Message);
    }

    [Fact]
    public void AMisspelledSettingSuggestsTheRealOne()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("uper_flow_limit = 0.2"));

        Assert.Contains("Did you mean 'upper_flow_limit'", error.Message);
    }

    [Fact]
    public void AValueOfTheWrongTypeNamesTheSettingAndWhatWasExpected()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("goal_ratio = \"loud\""));

        Assert.Contains("goal_ratio", error.Message);
        Assert.Contains("must be a number", error.Message);
    }

    [Fact]
    public void ATrueOrFalseSettingGivenANumberIsRefused()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("write_csv = 1"));

        Assert.Contains("write_csv", error.Message);
        Assert.Contains("true or false", error.Message);
    }

    [Fact]
    public void EveryProblemIsReportedAtOnce()
    {
        // Report all three errors together so the experimenter can correct them in one attempt.
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""
        goal_ratio = "loud"
        write_csv = 1
        nonsense_key = 3
        """));

        Assert.Contains("goal_ratio", error.Message);
        Assert.Contains("write_csv", error.Message);
        Assert.Contains("nonsense_key", error.Message);
    }

    [Fact]
    public void AProblemInsideAColorKeypointSaysWhichOne()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""
        [[ball_color_scale]]
        ratio = 0.95
        rgb = [0.5, 0.5, 1.0]

        [[ball_color_scale]]
        ratio = 1.05
        colour = [1, 1, 1]
        """));

        Assert.Contains("ball_color_scale[1]", error.Message);
        Assert.Contains("colour", error.Message);
    }

    [Fact]
    public void FlowLimitsInTheWrongOrderAreRejected()
    {
        var error = Assert.Throws<SettingsException>(() =>
            SettingsLoader.Parse("lower_flow_limit = 0.2\nupper_flow_limit = 0.1"));

        Assert.Contains("lower_flow_limit", error.Message);
    }

    [Fact]
    public void OverlappingColorBlendZonesAreRejected()
    {
        // The original settings file warned about this in a comment and then behaved in an
        // undefined way if it happened. Now it is caught, with the largest workable value named.
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("color_blend_zone = 0.5"));

        Assert.Contains("overlap", error.Message);
        Assert.Contains("color_blend_zone", error.Message);
    }

    [Theory]
    [InlineData("goal_ratio = 0", "goal_ratio")]
    [InlineData("history_window = 0", "history_window")]
    [InlineData("engine_tick_ms = -5", "engine_tick_ms")]
    [InlineData("flow_correction_factor = 0.0", "flow_correction_factor")]
    [InlineData("scoring_ratio_min = 1.2", "scoring_ratio_min")]
    public void UnusableValuesAreNamedInTheError(string toml, string expectedKey)
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse(toml));
        Assert.Contains(expectedKey, error.Message);
    }

    [Fact]
    public void AColorKeypointWithTheWrongNumberOfComponentsIsRejected()
    {
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""
        [[ball_color_scale]]
        ratio = 1.0
        rgb = [1, 0]
        """));

        Assert.Contains("three rgb values", error.Message);
    }

    [Fact]
    public void AMissingFileSaysWhereItLooked()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-such-settings-file.toml");

        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Load(path));

        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void MalformedTomlIsReportedAgainstTheFileWithItsLocation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, "goal_ratio = \n");

        try
        {
            var error = Assert.Throws<SettingsException>(() => SettingsLoader.Load(path));

            Assert.Contains(path, error.Message);
            Assert.Contains("could not be read", error.Message);

            // Preserve the line and column that Tomlyn reports so the operator can locate the error.
            Assert.Contains("(1,", error.Message);
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
            var candidate = Path.Combine(directory.FullName, "config", SettingsLoader.FileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find config/{SettingsLoader.FileName} above the test output directory.");
    }
}
