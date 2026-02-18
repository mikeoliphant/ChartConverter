using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PsarcUtil;
using SongFormat;

namespace ChartConverter
{
    public enum EConvertOption
    {
        Continue,
        Skip,
        Abort
    }

    public class PsarcExporter
    {
        public Func<string, string, string, EConvertOption> UpdateAction { get; set; }
        public bool OverwriteAudio { get; set; } = false;
        public bool OverwriteData { get; set; } = true;

        string destPath;
        
        JsonSerializerOptions indentedSerializerOptions = new JsonSerializerOptions()
        {
            Converters = {
               new JsonStringEnumConverter()
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { DefaultValueModifier }
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
        };

        JsonSerializerOptions condensedSerializerOptions = new JsonSerializerOptions()
        {
            Converters = {
               new JsonStringEnumConverter()
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { DefaultValueModifier }
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        private static void DefaultValueModifier(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;

            foreach (var property in typeInfo.Properties)
                if (property.PropertyType == typeof(int))
                {
                    property.ShouldSerialize = (_, val) => ((int)val != -1);
                }
        }

        public PsarcExporter(string destPath)
        {
            this.destPath = destPath;
        }

        public bool ConvertFolder(string path)
        {
            foreach (string folder in Directory.GetDirectories(path))
            {
                ConvertFolder(folder);
            }

            foreach (string psarcPath in Directory.GetFiles(path, "*.psarc"))
            {
                try
                {
                    if (!ConvertPsarc(psarcPath))
                        return false;
                }
                catch { }
            }

            return true;
        }

        public bool ConvertPsarc(string psarcPath)
        {
            PsarcDecoder decoder = new PsarcDecoder(psarcPath);

            PsarcDecoder songsDecoder = decoder;

            // This file has song entries, but audio exists in songs.psarc, which should be one directory up
            if (Path.GetFileName(psarcPath) == "rs1compatibilitydlc_p.psarc")
            {
                songsDecoder = new PsarcDecoder(Path.Combine(Path.GetDirectoryName(psarcPath), "..", "songs.psarc"));
            }

            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            foreach (PsarcSongEntry songEntry in decoder.AllSongs)
            {
                SongData songData = PsarcConverter.GetSongData(songEntry);

                string artistDir = Path.Combine(destPath, SerializationUtil.GetSafeFilename(songData.ArtistName));

                if (!Directory.Exists(artistDir))
                {
                    Directory.CreateDirectory(artistDir);
                }

                string songDir = Path.Combine(artistDir, SerializationUtil.GetSafeFilename(songData.SongName));

                if (UpdateAction != null)
                {
                    var convertOption = UpdateAction(songData.ArtistName, songData.SongName, songDir);

                    if (convertOption == EConvertOption.Abort)
                        return false;

                    if (convertOption == EConvertOption.Skip)
                        continue;
                }

                if (!Directory.Exists(songDir))
                {
                    Directory.CreateDirectory(songDir);
                }
                else
                {
                    if (!OverwriteData)
                        continue;
                }

                string songPath = Path.Combine(songDir, "song.json");

                if (File.Exists(songPath))
                {
                    try
                    {
                        using (FileStream stream = File.OpenRead(songPath))
                        {
                            var data = JsonSerializer.Deserialize(stream, typeof(SongData), SerializationUtil.IndentedSerializerOptions) as SongData;

                            if (data != null)
                            {
                                if (data.A440CentsOffset == 0)
                                    data.A440CentsOffset = songData.A440CentsOffset;

                                songData = data;
                            }
                        }
                    }
                    catch { }
                }

                SongStructure songStructure = new SongStructure();

                foreach (string partName in songEntry.Arrangements.Keys)
                {
                    try
                    {
                        var part = PsarcConverter.GetInstrumentPart(decoder, songEntry, partName);

                        if (part.HasValue)
                        {
                            List<SongSection> partSections = new List<SongSection>();

                            if (part.Value.SongStructure.Beats.Count > songStructure.Beats.Count)
                                songStructure = part.Value.SongStructure;

                            if (part.Value.Vocals != null)
                            {
                                using (FileStream stream = File.Create(Path.Combine(songDir, partName + ".json")))
                                {
                                    JsonSerializer.Serialize(stream, part.Value.Vocals, condensedSerializerOptions);
                                }
                            }

                            if (part.Value.Notes != null)
                            {
                                using (FileStream stream = File.Create(Path.Combine(songDir, partName + ".json")))
                                {
                                    JsonSerializer.Serialize(stream, part.Value.Notes, condensedSerializerOptions);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.ToString());
                    }
                }

                using (FileStream stream = File.Create(Path.Combine(songDir, "song.json")))
                {
                    JsonSerializer.Serialize(stream, songData, indentedSerializerOptions);
                }

                using (FileStream stream = File.Create(Path.Combine(songDir, "arrangement.json")))
                {
                    JsonSerializer.Serialize(stream, songStructure, indentedSerializerOptions);
                }

                try
                {
                    using (Stream outputStream = File.Create(Path.Combine(songDir, "albumart.png")))
                    {
                        PsarcConverter.WriteAlbumArtToStream(decoder, songEntry, outputStream);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error createing album art: " + ex.ToString());
                }

                string audioFile = Path.Combine(songDir, "song.ogg");

                if (OverwriteAudio || !File.Exists(audioFile))
                {
                    try
                    {
                        using (Stream outputStream = File.Create(audioFile))
                        {
                            PsarcConverter.WriteOggToStream(songsDecoder, songEntry, outputStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to create audio [" + audioFile + "] - " + ex.ToString());
                    }
                }
            }

            return true;
        }
    }
}
