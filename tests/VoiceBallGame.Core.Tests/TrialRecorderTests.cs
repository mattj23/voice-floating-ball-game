using System.Text.Json;
using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Recording;

namespace VoiceBallGame.Core.Tests;

public class TrialRecorderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"ballgame-tests-{Guid.NewGuid():N}");

    private GameSettings Settings(bool csv = true) => new()
    {
        OutputDirectory = _directory,
        WriteCsv = csv,
    };

    private static TrialResult Result(int number = 1, string? subject = "P07", string? session = "baseline") => new()
    {
        TrialNumber = number,
        SubjectId = subject,
        SessionId = session,
        StartedAt = new DateTimeOffset(2026, 3, 4, 14, 5, 9, TimeSpan.Zero),
        DurationSeconds = 2.0,
        SampleCount = 2,
    };

    private static List<TrialSample> Samples() =>
    [
        new() { Time = 0.0, Volume = 72, Flow = 0.09, RatioFraction = 1.0, InGoal = true, Error = 0 },
        new()
        {
            Time = 0.05, Volume = 85, Flow = 0.12, RatioFraction = 0.885, Error = 5.02,
            FlowError = 0.02, VolumeError = 5.0, FlowOutOfLimits = true,
        },
    ];

    [Fact]
    public void TheFileNameCarriesTheSubjectSessionTimeAndTrialNumber()
    {
        var recorder = new TrialRecorder(Settings());

        var path = recorder.Save(Result(number: 3), Samples());

        Assert.Equal("P07_baseline_20260304_140509_trial003.json", Path.GetFileName(path));
    }

    [Fact]
    public void TrialsTwelveHoursApartDoNotCollide()
    {
        // The original formatted the hour as "hh" with no meridiem, so a morning and an afternoon
        // trial produced the same name and the second overwrote the first.
        var recorder = new TrialRecorder(Settings());

        var morning = Result();
        morning.StartedAt = new DateTimeOffset(2026, 3, 4, 9, 15, 0, TimeSpan.Zero);
        var evening = Result(number: 2);
        evening.StartedAt = new DateTimeOffset(2026, 3, 4, 21, 15, 0, TimeSpan.Zero);

        var first = recorder.Save(morning, Samples());
        var second = recorder.Save(evening, Samples());

        Assert.NotEqual(Path.GetFileName(first), Path.GetFileName(second));
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void TheOutputDirectoryIsCreatedIfItIsMissing()
    {
        Assert.False(Directory.Exists(_directory));

        new TrialRecorder(Settings()).Save(Result(), Samples());

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public void ARelativeOutputDirectoryIsResolvedAgainstTheBaseDirectory()
    {
        var settings = new GameSettings { OutputDirectory = "data" };

        var recorder = new TrialRecorder(settings, _directory);

        Assert.Equal(Path.Combine(_directory, "data"), recorder.OutputDirectory);
    }

    [Fact]
    public void AnAbsoluteOutputDirectoryIgnoresTheBaseDirectory()
    {
        var recorder = new TrialRecorder(Settings(), Path.GetTempPath());

        Assert.Equal(_directory, recorder.OutputDirectory);
    }

    [Fact]
    public void TheSavedFileHoldsBothTheSummaryAndTheSamples()
    {
        var recorder = new TrialRecorder(Settings());
        var result = Result();

        var path = recorder.Save(result, Samples());

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("P07", root.GetProperty("summary").GetProperty("subjectId").GetString());
        Assert.Equal(2, root.GetProperty("samples").GetArrayLength());
        Assert.Equal(0.09, root.GetProperty("samples")[0].GetProperty("flow").GetDouble(), 9);
        Assert.Equal(path, result.DataFile);
    }

    [Fact]
    public void ACsvIsWrittenAlongsideWhenAskedFor()
    {
        var recorder = new TrialRecorder(Settings());

        var path = recorder.Save(Result(), Samples());
        var csvPath = Path.ChangeExtension(path, ".csv");

        Assert.True(File.Exists(csvPath));

        var lines = File.ReadAllLines(csvPath);
        Assert.StartsWith("time,volume,flow", lines[0]);
        Assert.Equal(3, lines.Length);

        // The out-of-limit sample carries its components; the in-limit one leaves them empty.
        Assert.Contains(",0.02,5,", lines[2]);
        Assert.Contains(",,,", lines[1]);
    }

    [Fact]
    public void NoCsvIsWrittenWhenItIsTurnedOff()
    {
        var recorder = new TrialRecorder(Settings(csv: false));

        var path = recorder.Save(Result(), Samples());

        Assert.False(File.Exists(Path.ChangeExtension(path, ".csv")));
    }

    [Fact]
    public void ATrialWithNoSubjectStillGetsAUsableName()
    {
        var recorder = new TrialRecorder(Settings());

        var path = recorder.Save(Result(subject: null, session: null), Samples());

        Assert.Equal("20260304_140509_trial001.json", Path.GetFileName(path));
    }

    [Fact]
    public void IdentifiersThatWouldBreakAFileNameAreMadeSafe()
    {
        var recorder = new TrialRecorder(Settings());

        var path = recorder.Save(Result(subject: "P 07/A", session: "pre:test"), Samples());

        Assert.Equal("P-07-A_pre-test_20260304_140509_trial001.json", Path.GetFileName(path));
    }

    [Fact]
    public void NumbersAreWrittenInAnInvariantFormat()
    {
        // A locale that uses a comma for the decimal separator would otherwise produce a CSV
        // that no analysis script could read.
        var original = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var path = new TrialRecorder(Settings()).Save(Result(), Samples());
            var csv = File.ReadAllText(Path.ChangeExtension(path, ".csv"));

            Assert.Contains("0.09", csv);
            Assert.DoesNotContain("0,09", csv);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
