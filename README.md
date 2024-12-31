# ChartConverter

This app provides a simple interface for converting Rocksmith PSARC and Rock band files to [OpenSongChart](https://github.com/mikeoliphant/OpenSongChart) format.

It uses [PsarcUtil](https://github.com/mikeoliphant/PsarcUtil) and [RockBandUtil](https://github.com/mikeoliphant/RockBandUtil) under the hood.

## Supported chart formats

ChartConverter currently supports the following formats:

- Rocksmith (original and CDLC) PSARC files for Guitar, Bass and Vocals
- RockBand (Phase Shift format) for Drums, Keys and Vocals

## Downloading

The latest version can be found in the releases section, [here](https://github.com/mikeoliphant/ChartConverter/releases/latest).

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
