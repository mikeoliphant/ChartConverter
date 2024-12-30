# ChartConverter

This app provides a simple interface for converting Rocksmith PSARC and Rock band files to [OpenSongChart](https://github.com/mikeoliphant/OpenSongChart) format.

It uses [PsarcUtil](https://github.com/mikeoliphant/PsarcUtil) and [RockBandUtil](https://github.com/mikeoliphant/RockBandUtil) under the hood.

# Downloading

The latest version can be found in the releases section, [here](https://github.com/mikeoliphant/ChartConverter/releases/latest).

# Running

The following external dependencies must be installed:

- libogg
- libvorbis
- libgdiplus (on non-Windows platforms)
- Zenity (on non-Windows platforms)

On Windows, extract the downladed .zip file and run "ChartConverter".

On other platforms, extract the downloaded .zip file and run "ChartConverterGL". You will likely have to make it executable first:

```
chmod u+x ChartConverterGL
```
