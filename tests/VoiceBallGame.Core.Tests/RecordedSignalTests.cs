using VoiceBallGame.Core.Replay;

namespace VoiceBallGame.Core.Tests;

public class RecordedSignalTests
{
    [Fact]
    public void ARecordingSurvivesARoundTrip()
    {
        var header = new SignalHeader
        {
            Kind = SignalKind.Flow,
            Units = "L/s",
            Source = "SFM3300-D on /dev/ttyUSB0",
            Recorded = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
            Note = "Pilot participant",
        };

        var points = new List<RecordedPoint>
        {
            new(0, 0.0),
            new(50.5, 0.0821),
            new(100, 0.09134),
        };

        using var writer = new StringWriter();
        RecordedSignal.Write(writer, header, points);
        var (readHeader, readPoints) = RecordedSignal.Parse(writer.ToString());

        Assert.Equal(SignalKind.Flow, readHeader.Kind);
        Assert.Equal("L/s", readHeader.Units);
        Assert.Equal("SFM3300-D on /dev/ttyUSB0", readHeader.Source);
        Assert.Equal(header.Recorded, readHeader.Recorded);
        Assert.Equal(points, readPoints);
    }

    [Fact]
    public void EachSampleIsItsOwnLine()
    {
        // Line-per-sample is what lets a recording be written as a session runs and still be
        // readable if the program is killed partway through.
        using var writer = new StringWriter();
        RecordedSignal.Write(writer, new SignalHeader { Kind = SignalKind.Volume, Units = "dB SPL" },
            [new RecordedPoint(0, 60), new RecordedPoint(50, 61)]);

        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Contains("\"kind\"", lines[0]);
        Assert.Contains("\"t_ms\"", lines[1]);
    }

    [Fact]
    public void ATruncatedRecordingKeepsEverythingBeforeTheCut()
    {
        using var writer = new StringWriter();
        RecordedSignal.Write(writer, new SignalHeader { Kind = SignalKind.Flow },
            Enumerable.Range(0, 20).Select(i => new RecordedPoint(i * 50, 0.09)));

        var full = writer.ToString();
        var truncated = full[..(full.Length / 2)];
        truncated = truncated[..truncated.LastIndexOf('\n')];

        var (_, points) = RecordedSignal.Parse(truncated);

        Assert.NotEmpty(points);
        Assert.All(points, p => Assert.Equal(0.09, p.Value));
    }

    [Fact]
    public void BlankLinesAreIgnored()
    {
        var (_, points) = RecordedSignal.Parse("""
        {"kind":"Flow","units":"L/s"}

        {"t_ms":0,"v":0.05}

        {"t_ms":50,"v":0.06}
        """);

        Assert.Equal(2, points.Count);
    }

    [Fact]
    public void AFileWithNoHeaderIsRejected()
    {
        var error = Assert.Throws<ReplayFormatException>(() =>
            RecordedSignal.Parse("   \n  \n"));

        Assert.Contains("no header", error.Message);
    }

    [Fact]
    public void ABadLineNamesItsLineNumber()
    {
        var error = Assert.Throws<ReplayFormatException>(() => RecordedSignal.Parse("""
        {"kind":"Flow"}
        {"t_ms":0,"v":0.05}
        {"t_ms": oops}
        """));

        Assert.Contains("line 3", error.Message);
    }

    [Fact]
    public void ValuesKeepTheirPrecision()
    {
        // Flow values are small numbers where a rounded serialization would visibly change the
        // ratio the game computes from them.
        var points = new List<RecordedPoint> { new(16.666666, 0.0812345678) };

        using var writer = new StringWriter();
        RecordedSignal.Write(writer, new SignalHeader { Kind = SignalKind.Flow }, points);
        var (_, read) = RecordedSignal.Parse(writer.ToString());

        Assert.Equal(0.0812345678, read[0].Value, 9);
        Assert.Equal(16.666666, read[0].TimeMs, 6);
    }
}
