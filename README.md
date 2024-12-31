# ChartConverter

This app provides a simple interface for converting Rocksmith PSARC and Rock band files to [OpenSongChart](https://github.com/mikeoliphant/OpenSongChart) format.

It uses [PsarcUtil](https://github.com/mikeoliphant/PsarcUtil) and [RockBandUtil](https://github.com/mikeoliphant/RockBandUtil) under the hood.

## Downloading

The latest version can be found in the releases section, [here](https://github.com/mikeoliphant/ChartConverter/releases/latest).

## Supported chart formats

ChartConverter currently supports the following formats:

- Rocksmith (original and CDLC) PSARC files for Guitar, Bass and Vocals
- RockBand (Phase Shift format) for Drums, Keys and Vocals

## Where To Get Charts

If you own Rocksmith, you can covert the psarc charts from the game iteself.

CDLC psarc charts are available from the [Ignition song database](https://ignition4.customsforge.com/).

RockBand Phase Shift songs can be found in various places:

- https://rhythmverse.co/songfiles/game/ps
- https://www.fretsonfire.org/forums/viewforum.php?f=5&sid=6ca91ebdb016bc22de5c4d4a3f582e64

## What Is Phase Shift format?

"Phase Shift" was a PC based rhythm game that playing Rock Band charts. Currently, the format used by Phase Shift is what is supported by ChartConverter.

You can tell a song is in the Phase Shift format if it has a "notes.mid" file. Support for songs with "notes.chart" will likely come soon.

## Running

The following external dependencies must be installed:

- libogg
- libvorbis
- libgdiplus (on non-Windows platforms)

On Windows, extract the downladed .zip file and run "ChartConverter".

On other platforms, you will likely have to make it executable first:

```
chmod u+x ChartConverter
```
