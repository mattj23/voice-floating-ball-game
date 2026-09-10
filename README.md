# voice-floating-ball-game

Floating ball game based on vocal airflow and volume, built for vocal motor learning research.

A participant sustains a vowel while a ball floats on screen. The target is a specified ratio of
loudness to oral airflow: 800 dB·s/L by default, the "resonant voice" target. Airflow sets the
positions of the ball and its goal box. Loudness sets how far and how fast the ball swings. The
ball's color indicates how close the ratio is to the target. Trials start and stop automatically as
the participant begins and stops voicing. The game saves a summary and per-frame data for each
trial.

## Repository layout

| Directory | What it is |
|---|---|
| `src/` | The application (.NET 8, Avalonia, ReactiveUI) |
| `tests/` | Unit tests for the game engine, scoring, calibration, and settings |
| `config/` | The default `app_settings.toml` shipped with the application |
| `build/` | Installer packaging scripts (see [Installers](#installers)) |

The application runs on Windows, Linux, and macOS. It reads airflow from a Sensirion SFM3x00 flow
meter over a Nicolay serial connector using
[NicolaySerialSFM3x00](https://www.nuget.org/packages/NicolaySerialSFM3x00).

The application replaces an earlier Windows-only WPF program built on .NET Framework 4.6.1. That
program was removed from the repository. Its source remains available in the Git history at commit
`6bbe8b7`. The earlier program measured airflow with an analog transducer connected to a sound card
line-in. The SFM3x00 is factory-calibrated and reports engineering units directly, so the current
application does not need the earlier flow calibration workflow.

## Run the application

```bash
dotnet run -c Release --project src/VoiceBallGame.App
```

To run or develop the game without attached hardware:

```bash
dotnet run -c Release --project src/VoiceBallGame.App -- --demo
```

`--demo` plays against a simulated participant who voices in bouts around the target ratio. The
same simulated inputs, and any recorded session, are also selectable from the setup screen.

Run the tests:

```bash
dotnet test
```

## Installers

Installers are built with [Velopack](https://velopack.io). Install the matching `vpk` tool once:

```bash
dotnet tool install -g vpk --version 1.2.0
```

| Platform | Command | Output in `artifacts/releases/<runtime>/` |
|---|---|---|
| Windows | `./build/pack.ps1` | `VoiceBallGame-win-Setup.exe`, portable zip |
| Linux | `./build/pack.ps1 -Runtime linux-x64` (from Windows) or `build/pack.sh` | `VoiceBallGame.AppImage` |
| macOS (Apple Silicon) | `build/pack.sh` (on a Mac only) | `.pkg` installer, portable zip |

The version comes from `<Version>` in `src/VoiceBallGame.App/VoiceBallGame.App.csproj`. Pass
`-Version` (PowerShell) or `--version` (bash) to override it. The **Build installers** GitHub
Actions workflow builds all three platforms. Run it manually for test builds, or push a tag such as
`v0.2.0` to also create a draft GitHub release.

The Windows installer needs no administrator rights and installs to `%LocalAppData%\VoiceBallGame`.
The macOS build is unsigned unless signing identities are supplied (see `build/pack.sh`). On first
launch, right-click the app and choose **Open**.

### File locations for installed copies

Updates replace the application folder, and the macOS and Linux bundles are read-only. Therefore,
an installed copy stores all writable files outside the application folder. These files survive
updates and uninstalls.

| | Settings and calibrations | Trial data (relative `output_directory`) |
|---|---|---|
| Windows | `%AppData%\VoiceBallGame\` | `Documents\VoiceBallGame\` |
| Linux | `~/.config/VoiceBallGame/` | `~/Documents/VoiceBallGame/` |
| macOS | `~/Library/Application Support/VoiceBallGame/` | `~/Documents/VoiceBallGame/` |

On first run, the installed copy creates `app_settings.toml` in its settings folder from the shipped
defaults. The application does not overwrite this file on subsequent runs. The setup screen shows
which settings file was loaded and where the application will write trials. A development build
(`dotnet run`) behaves as described below: it stores calibrations beside the executable, loads
settings from beside the executable or from `config/`, and resolves data paths relative to the
working directory.

## Setting up a session

1. **Pick the flow meter.** Serial ports are listed by name. On Linux the meter usually appears as
   `/dev/ttyUSB0`.
2. **Pick and calibrate the microphone.** A microphone reports a signal level with no absolute
   meaning, so the game cannot know how loud the participant is until one known loudness has been
   measured. Produce a steady sound for at least three seconds with a sound level meter beside the
   microphone, then enter the meter reading. The game stores the calibration for each device in
   `calibrations.json`. Calibrate again if the microphone or its gain changes.
3. **Enter the subject and session identifiers.** They name the files this session produces.
4. **Begin.** Trials record themselves whenever the participant voices.

## Data output

The game writes one JSON file and one CSV file per trial to the directory specified by
`output_directory`. Each file uses the name `{subject}_{session}_{yyyyMMdd_HHmmss}_trial{n}`. The
JSON file contains a summary (length, time on target, number of target entries, and average error)
followed by every frame. The CSV file contains only the frames for analysis.

## Configuration

All experimenter-adjustable settings are in `config/app_settings.toml`, which is copied next to the
executable at build time. The file contains comments that the game preserves because it never
rewrites the file.

The game reports all detected configuration problems together and refuses to start when those
problems could produce unintended settings. Validation includes:

- values that violate configuration constraints, such as a `lower_flow_limit` above
  `upper_flow_limit`;
- values of the wrong type, such as `write_csv = 1` when the setting requires `true` or `false`;
- **misspelled settings**, with the closest recognized setting when available. For example,
  `uper_flow_limit = 0.2` produces an error instead of being silently ignored by automatic binding.

Any omitted setting retains its built-in default.

## Platform notes

- **Linux:** your user must be in the `dialout` group to open a serial port. Add yourself with
  `sudo usermod -aG dialout $USER`, then log out and back in.
- **macOS:** the app bundle needs an `NSMicrophoneUsageDescription` entry, and the first run prompts
  for microphone access. `build/macos/Info.plist` provides it for packaged builds.
- **Windows:** no additional setup.

## Changes that affect data compatibility

The port fixed several defects. Each is documented where it was fixed, and the ones that change
recorded numbers are listed here because they affect comparability with data collected by the WPF
version.

- **The ball's colors were shifted one band.** The color lookup only held the blend zones around
  each keypoint and left the space between them empty, so a lookup falling in the gap picked up the
  previous band's color. On-target ratios rendered light blue, and white appeared roughly between
  1.05 and 1.10, outside the scoring window that counts 0.95 to 1.05 as on target. The participant's
  color feedback and the score therefore disagreed. Colors now match the bands the settings file
  documents. **Account for this visual-feedback change when you compare sessions recorded before
  and after the port.**
- **Out-of-limit error was far too large.** The loudness associated with a flow limit was computed
  as `limit / goal_ratio` instead of `goal_ratio * limit`, giving 0.000125 dB rather than 80 dB, so
  the loudness term amounted to the raw reading. Set `legacy_compat_scoring` to reproduce the old
  formula when comparing against old data. Error inside the flow limits is unaffected.
- **The recorded goal band was twice as tall as the one drawn.** The original calculation extended
  the box's full height on either side of its center instead of extending it by half its height.
- **The end-of-trial message reported "pixels"** for a value that was a ratio, and time in the goal
  and entries into the goal were computed and then discarded. The message now reports trial length,
  share of the trial on target, entries into the target, and average distance from target as a
  percentage. All of these are saved with the trial.
- **Levels depended on buffer length.** The old "RMS" summed squares without dividing by the sample
  count, so changing `buffer_ms` silently rescaled every calibration. **Microphone calibrations must
  be redone on this version**; old values are not comparable.
- **Trials twelve hours apart overwrote each other** because the file name used a 12-hour clock
  with no AM/PM marker.
- **Short trials could crash the game** because the summary calculation did not guard against an
  empty sample list or a zero-length trial.
- **Ball physics constants are now configurable** rather than buried in the code: `flow_position_scale`,
  `flow_position_offset`, `goal_half_height_factor`, `volume_floor_db` and `frequency_base`.

## Open research questions

- **Units.** The SFM3x00 reports *standard* liters per minute; the game converts to L/s. The old
  analog transducer measured at ambient conditions. If the protocol needs a BTPS correction
  (roughly 1.07 to 1.10 for exhaled air), set `flow_correction_factor`. It defaults to 1.0, which
  records the sensor's standard-conditions reading unchanged.
- **Which error measure drives feedback.** The protocol's error is in dB·s/L inside the flow limits
  but in dB plus L/s outside them, so the two branches are not on the same scale and averaging them
  mixes units. Every frame now also records `ratioError`, a dimensionless distance from target with
  a consistent meaning in both cases. The game shows this value to the participant and continues
  to record the protocol error as `error`.
- **Whether the flow limits still suit the new sensor.** `upper_flow_limit` and `lower_flow_limit`
  were tuned against the old transducer chain. They are physiological values so they probably still
  hold, but they are worth confirming.
