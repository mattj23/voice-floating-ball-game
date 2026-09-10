using VoiceBallGame.Core.Calibration;

namespace VoiceBallGame.Core.Tests;

public class MicCalibrationTests
{
    private static MicCalibration Reference(double level = 0.05, double db = 75) =>
        MicCalibration.FromReference("0:Test mic", "Test mic", level, db);

    [Fact]
    public void TheReferenceLevelMapsBackToItsOwnLoudness()
    {
        var calibration = Reference(level: 0.05, db: 75);

        Assert.Equal(75.0, calibration.ToDecibels(0.05), 9);
    }

    [Fact]
    public void DoublingTheLevelAddsSixDecibels()
    {
        var calibration = Reference(level: 0.05, db: 75);

        Assert.Equal(75.0 + 20 * Math.Log10(2), calibration.ToDecibels(0.10), 9);
        Assert.Equal(81.02, calibration.ToDecibels(0.10), 2);
    }

    [Fact]
    public void HalvingTheLevelSubtractsSixDecibels()
    {
        var calibration = Reference(level: 0.05, db: 75);

        Assert.Equal(75.0 - 20 * Math.Log10(2), calibration.ToDecibels(0.025), 9);
    }

    [Fact]
    public void SilenceGivesAFiniteValueWellBelowAnythingTheGameReactsTo()
    {
        // Taking the logarithm of zero would otherwise produce negative infinity, which would
        // spread through the moving average and the ratio for the rest of the session.
        var calibration = Reference(db: 75);

        double result = calibration.ToDecibels(0.0);

        Assert.True(double.IsFinite(result));
        Assert.True(result < 0);
    }

    [Fact]
    public void ASilentRecordingCannotBecomeACalibration()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MicCalibration.FromReference("k", "n", level: 0, db: 75));

        Assert.Contains("silent", error.Message);
    }

    [Fact]
    public void AnEmptyCalibrationIsNotUsable()
    {
        Assert.False(new MicCalibration().IsUsable);
        Assert.True(Reference().IsUsable);
    }
}

public class CalibrationStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"calibrations-{Guid.NewGuid():N}.json");

    [Fact]
    public void ACalibrationSurvivesBeingReloaded()
    {
        var store = new CalibrationStore(_path);
        store.Save(MicCalibration.FromReference("0:Blue Yeti", "Blue Yeti", 0.042, 72));

        var reloaded = new CalibrationStore(_path).Find("0:Blue Yeti");

        Assert.NotNull(reloaded);
        Assert.Equal(0.042, reloaded!.ReferenceLevel, 9);
        Assert.Equal(72, reloaded.ReferenceDb, 9);
        Assert.Equal("Blue Yeti", reloaded.DeviceName);
    }

    [Fact]
    public void CalibrationsAreKeptPerDevice()
    {
        var store = new CalibrationStore(_path);
        store.Save(MicCalibration.FromReference("0:Mic A", "Mic A", 0.01, 70));
        store.Save(MicCalibration.FromReference("0:Mic B", "Mic B", 0.02, 80));

        Assert.Equal(70, store.Find("0:Mic A")!.ReferenceDb);
        Assert.Equal(80, store.Find("0:Mic B")!.ReferenceDb);
        Assert.Null(store.Find("0:Mic C"));
    }

    [Fact]
    public void RecalibratingReplacesTheEarlierCalibration()
    {
        var store = new CalibrationStore(_path);
        store.Save(MicCalibration.FromReference("0:Mic", "Mic", 0.01, 70));
        store.Save(MicCalibration.FromReference("0:Mic", "Mic", 0.03, 78));

        Assert.Equal(78, new CalibrationStore(_path).Find("0:Mic")!.ReferenceDb);
    }

    [Fact]
    public void ACorruptFileDoesNotStopTheGameFromStarting()
    {
        File.WriteAllText(_path, "this is not json");

        var store = new CalibrationStore(_path);

        Assert.Null(store.Find("anything"));

        // Saving a valid calibration replaces the corrupt file and restores normal loading.
        store.Save(MicCalibration.FromReference("0:Mic", "Mic", 0.01, 70));
        Assert.NotNull(new CalibrationStore(_path).Find("0:Mic"));
    }

    [Fact]
    public void AMissingFileIsSimplyAnEmptyStore()
    {
        Assert.Null(new CalibrationStore(_path).Find("0:Mic"));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

public class PeakEstimatorTests
{
    /// <summary>Builds the calibration-recording sequence: quiet, a held level, and then quiet.</summary>
    private static List<double> Recording(double held, int heldCount = 300, double quiet = 0.002)
    {
        var values = new List<double>();
        values.AddRange(Enumerable.Repeat(quiet, 60));

        // A short ramp in and out, as the operator brings the sound up and takes it away.
        for (int i = 1; i <= 10; i++) values.Add(quiet + (held - quiet) * i / 10.0);
        values.AddRange(Enumerable.Range(0, heldCount).Select(i => held + 0.0004 * Math.Sin(i / 3.0)));
        for (int i = 10; i >= 1; i--) values.Add(quiet + (held - quiet) * i / 10.0);

        values.AddRange(Enumerable.Repeat(quiet, 60));
        return values;
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.12)]
    [InlineData(0.30)]
    public void TheHeldLevelIsRecovered(double held)
    {
        var level = PeakEstimator.FindLevel(Recording(held));

        Assert.NotNull(level);
        Assert.Equal(held, level!.Value, 2);
    }

    [Fact]
    public void TheHeldLevelIsFoundRatherThanTheMean()
    {
        // The quiet periods on either side reduce the recording's mean below the level that the
        // operator held. The estimator must identify the held level.
        var recording = Recording(0.20);
        var level = PeakEstimator.FindLevel(recording)!.Value;

        Assert.True(level > recording.Average() * 1.1,
            $"The estimate {level:F4} should sit above the recording's mean of {recording.Average():F4}.");
        Assert.Equal(0.20, level, 2);
    }

    [Fact]
    public void AnEmptyRecordingFindsNothing()
    {
        Assert.Null(PeakEstimator.FindLevel([]));
    }

    [Fact]
    public void ASilentRecordingFindsNothingUsable()
    {
        Assert.Null(PeakEstimator.FindLevel(Enumerable.Repeat(0.0, 200).ToList()));
    }

    [Fact]
    public void SeveralHeldLevelsAreReportedStrongestFirst()
    {
        // The original's screen collected three levels this way. Only one is needed now, but the
        // estimator still separates them, which is what makes the strongest one meaningful.
        // Clusters have to be of comparable size to both be seen, because the threshold sits at
        // half the densest cluster.
        var values = new List<double>();
        values.AddRange(Enumerable.Repeat(0.30, 400));
        values.AddRange(Enumerable.Repeat(0.10, 320));

        var levels = PeakEstimator.FindLevels(values);

        Assert.True(levels.Count >= 2, $"Expected at least two levels but found {levels.Count}.");
        Assert.Equal(0.30, levels[0], 2);
    }

    [Fact]
    public void AClusterFarSmallerThanTheStrongestIsNotReported()
    {
        // A consequence of the half-of-peak threshold, and the reason a calibration recording
        // should hold one level rather than several of uneven length.
        var values = new List<double>();
        values.AddRange(Enumerable.Repeat(0.30, 400));
        values.AddRange(Enumerable.Repeat(0.10, 40));

        var levels = PeakEstimator.FindLevels(values);

        Assert.Single(levels);
        Assert.Equal(0.30, levels[0], 2);
    }
}
