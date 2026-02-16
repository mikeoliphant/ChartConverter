using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PsarcUtil;
using SongFormat;
using Rocksmith2014PsarcLib.Psarc.Asset;
using Rocksmith2014PsarcLib.Psarc.Models.Json;
using Rocksmith2014PsarcLib.Psarc.Models.Sng;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using System.Xml.Linq;
using Rocksmith2014PsarcLib.Psarc;

namespace ChartConverter
{
    public enum EConvertOption
    {
        Continue,
        Skip,
        Abort
    }

    public class PsarcConverterFiles
    {
        public SongData Song { get; set; } // song.json
        public SongStructure Arrangement { get; set; } // arrangement.json
        public List<SongVocal> Vocals { get; set; } // vocal.json
        public Dictionary<string, SongInstrumentNotes> Parts { get; set; } = []; // [part].json
        public byte[] AlbumArt { get; set; } // albumart.png
        public byte[] AudioFile { get; set; } // song.ogg 
    }

    public class PsarcConverter
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

        public PsarcConverter()
        {
        }

        public PsarcConverter(string destPath)
        {
            this.destPath = destPath;
        }

        public bool ConvertFolder(string path)
        {
            if (string.IsNullOrEmpty(destPath)) {
                throw new ArgumentNullException(nameof(destPath), "Destination path must be set before converting a folder");
            }

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
            if (string.IsNullOrEmpty(destPath)) {
                throw new ArgumentNullException(nameof(destPath), "Destination path must be set before converting a psarc");
            }

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
                SongData songData = GetSongData(songEntry);

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
                                songData = data;
                            }
                        }
                    }
                    catch { }
                }

                ProcessArrangements(songEntry, decoder, songData, out SongStructure songStructure,
                    saveVocals: (string partName, List<SongVocal> vocals) =>
                    {
                        using (FileStream stream = File.Create(Path.Combine(songDir, partName + ".json")))
                        {
                            JsonSerializer.Serialize(stream, vocals, condensedSerializerOptions);
                        }
                    },
                    saveNotes: (string partName, SongInstrumentNotes notes) =>
                    {
                        using (FileStream stream = File.Create(Path.Combine(songDir, partName + ".json")))
                        {
                            JsonSerializer.Serialize(stream, notes, condensedSerializerOptions);
                        }
                    }
                );

                using (FileStream stream = File.Create(Path.Combine(songDir, "song.json")))
                {
                    JsonSerializer.Serialize(stream, songData, indentedSerializerOptions);
                }

                using (FileStream stream = File.Create(Path.Combine(songDir, "arrangement.json")))
                {
                    JsonSerializer.Serialize(stream, songStructure, indentedSerializerOptions);
                }

                SaveAlbumArt(songEntry, decoder,
                    save: (System.Drawing.Bitmap bitmap) =>
                    { 
                        string albumPath = Path.Combine(songDir, "albumart.png");
                        bitmap.Save(albumPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                );

                string audioFile = Path.Combine(songDir, "song.ogg");
                
                if (OverwriteAudio || !File.Exists(audioFile)) {
                    SaveAudioFile(songEntry, songsDecoder,
                        save: (byte[] data) =>
                        {
                            File.WriteAllBytes(audioFile, data);
                        }
                    );
                }
            }

            return true;
        }

        public List<PsarcConverterFiles> ConvertPsarc(Stream psarcStream, Stream songsStream)
        {
            var result = new List<PsarcConverterFiles>();

            PsarcDecoder decoder = new PsarcDecoder(psarcStream);

            PsarcDecoder songsDecoder = songsStream == null ? decoder : new PsarcDecoder(songsStream);

            foreach (PsarcSongEntry songEntry in decoder.AllSongs)
            {
                PsarcConverterFiles psarcConverterFiles = new PsarcConverterFiles();
                result.Add(psarcConverterFiles);

                SongData songData = GetSongData(songEntry);

                ProcessArrangements(songEntry, decoder, songData, out SongStructure songStructure,
                    saveVocals: (string partName, List<SongVocal> vocals) =>
                    {
                        psarcConverterFiles.Vocals = vocals;
                    }, 
                    saveNotes: (string partName, SongInstrumentNotes notes) =>
                    {
                        psarcConverterFiles.Parts.Add(partName, notes);
                    }
                );

                psarcConverterFiles.Song = songData;

                psarcConverterFiles.Arrangement = songStructure;

                SaveAlbumArt(songEntry, decoder,
                    save: (System.Drawing.Bitmap bitmap) =>
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            psarcConverterFiles.AlbumArt = ms.ToArray();
                        }
                    }
                );

                SaveAudioFile(songEntry, songsDecoder,
                    save: (byte[] data) =>
                    {
                        psarcConverterFiles.AudioFile = data;
                    }
                );
            }

            return result;
        }

        SongInstrumentPart CreateInstrumentPart(string partName, List<SongSection> partSections, SongArrangement arrangement, SngAsset songAsset, PsarcDecoder decoder, Action<string, List<SongVocal>> saveVocals, Action<string, SongInstrumentNotes> saveNotes)
        {
            float songDifficulty = Math.Min(arrangement.Attributes.SongDifficulty * 5, 5);

            songDifficulty = (int)(songDifficulty * 10) / 10.0f;

            SongInstrumentPart part = new SongInstrumentPart()
            {
                InstrumentName = partName,
                SongDifficulty = songDifficulty
            };

            if (arrangement.Attributes.ArrangementProperties == null)
            {
                part.InstrumentType = ESongInstrumentType.Vocals;
            }
            else if (arrangement.Attributes.ArrangementProperties.PathLead == 1)
            {
                part.InstrumentType = ESongInstrumentType.LeadGuitar;
            }
            else if (arrangement.Attributes.ArrangementProperties.PathRhythm == 1)
            {
                part.InstrumentType = ESongInstrumentType.RhythmGuitar;
            }
            else if(arrangement.Attributes.ArrangementProperties.PathBass == 1)
            {
                part.InstrumentType = ESongInstrumentType.BassGuitar;
            }

            if (part.InstrumentType == ESongInstrumentType.Vocals)
            {
                List<SongVocal> vocals = new List<SongVocal>();

                if (songAsset.Vocals != null)
                {
                    foreach (Vocal vocal in songAsset.Vocals)
                    {
                        vocals.Add(new SongVocal()
                        {
                            Vocal = vocal.Lyric.Replace('+', '\n'),
                            TimeOffset = vocal.Time
                        });
                    }

                    ChartUtil.FormatVocals(vocals);

                    saveVocals(partName, vocals);
                }
            }
            else
            {
                if (arrangement.Attributes.Tuning != null)
                {
                    part.Tuning = new StringTuning()
                    {
                        StringSemitoneOffsets = new List<int> { arrangement.Attributes.Tuning.String0, arrangement.Attributes.Tuning.String1, arrangement.Attributes.Tuning.String2, arrangement.Attributes.Tuning.String3, arrangement.Attributes.Tuning.String4, arrangement.Attributes.Tuning.String5 }
                    };
                }

                part.CapoFret = (int)arrangement.Attributes.CapoFret;

                SongInstrumentNotes notes = new SongInstrumentNotes();

                notes.Sections = partSections;

                foreach (Chord chord in songAsset.Chords)
                {
                    SongChord songChord = new SongChord()
                    {
                        Name = chord.Name,
                        Fingers = new List<int>(chord.Fingers.Select(f => (int)((sbyte)f))),
                        Frets = new List<int>(chord.Frets.Select(f => (int)((sbyte)f)))
                    };

                    notes.Chords.Add(songChord);
                }

                Note lastNote = new Note();

                foreach (var phrase in songAsset.PhraseIterations.Select((x, i) => new { x, i }))
                {
                    foreach (var arrange in songAsset.Arrangements.OrderByDescending(x => x.Difficulty))
                    {
                        var phraseNotes = arrange.Notes.Where(x => x.PhraseIterationId == phrase.i).ToArray();

                        int lastChordID = -1;

                        if (phraseNotes.Length > 0)
                        {
                            foreach (var note in phraseNotes)
                            {
                                int chordID = -1;
                                float duration = 0;

                                if (note.FingerPrintId[0] != -1)
                                {
                                    chordID = arrange.Fingerprints1[note.FingerPrintId[0]].ChordId;
                                    duration = (arrange.Fingerprints1[note.FingerPrintId[0]].EndTime - arrange.Fingerprints1[note.FingerPrintId[0]].StartTime);
                                }

                                if (note.FingerPrintId[1] != -1)
                                {
                                    chordID = arrange.Fingerprints2[note.FingerPrintId[1]].ChordId;
                                    duration = (arrange.Fingerprints2[note.FingerPrintId[1]].EndTime - arrange.Fingerprints2[note.FingerPrintId[1]].StartTime);
                                }

                                lastChordID = chordID;

                                SongNote songNote = new SongNote()
                                {
                                    TimeOffset = note.Time,
                                    TimeLength = note.Sustain,
                                    Fret = (sbyte)note.FretId,
                                    String = (sbyte)note.StringIndex,
                                    Techniques = ConvertTechniques((NoteMaskFlag)note.NoteMask),
                                    HandFret = (sbyte)note.AnchorFretId,
                                    SlideFret = (sbyte)note.SlideTo,
                                    ChordID = note.ChordId
                                };

                                if ((chordID != -1) && (chordID != songNote.ChordID))
                                {
                                    songNote.FingerID = chordID;

                                    //if (lastChordID != chordID)
                                    //{
                                    //    note.NoteMask |= (uint)NoteMaskFlag.CHORD;
                                    //    note.Sustain = duration;
                                    //}
                                }

                                // Set chord flag on first note of arpeggiated section
                                //if (((NoteMaskFlag)note.NoteMask).HasFlag(NoteMaskFlag.ARPEGGIO) && (!((NoteMaskFlag)lastNote.NoteMask).HasFlag(NoteMaskFlag.ARPEGGIO) || (lastNote.ChordId != note.ChordId)))
                                //{
                                //    songNote.Techniques |= ESongNoteTechnique.Chord;
                                //}

                                //if (songNote.Techniques.HasFlag(ESongNoteTechnique.Continued))
                                //{
                                //    if ((lastNote.ChordId == -1) && ((sbyte)lastNote.SlideTo != note.FretId) && (lastNote.FretId != note.FretId))
                                //    {

                                //    }
                                //}

                                lastNote = note;

                                if (songNote.SlideFret <= 0)
                                {
                                    songNote.SlideFret = (sbyte)note.SlideUnpitchTo;
                                }

                                if ((note.BendData != null) && (note.BendData.Length > 0))
                                {
                                    songNote.CentsOffsets = new CentsOffset[note.BendData.Length];

                                    for (int i = 0; i < note.BendData.Length; i++)
                                    {
                                        songNote.CentsOffsets[i] = new CentsOffset
                                        {
                                            TimeOffset = note.BendData[i].Time,
                                            Cents = (int)(note.BendData[i].Step * 100)
                                        };
                                    };
                                }

                                if (note.ChordNotesId != -1)
                                {
                                    ChordNotes chordNotes = songAsset.ChordNotes[note.ChordNotesId];

                                    SongChord chord = notes.Chords[note.ChordId];

                                    List<SongNote> notesToAdd = new List<SongNote>();

                                    for (int str = 0; str < 6; str++)
                                    {
                                        //if ((chordNotes.BendData[str].UsedCount > 0) || (chordNotes.NoteMask[str] != 0) || (chordNotes.Vibrato[str] > 0) || (((sbyte)chordNotes.SlideTo[str]) != -1) || (((sbyte)chordNotes.SlideUnpitchTo[str]) != -1))
                                        if (chord.Frets[str] != -1)
                                        {
                                            SongNote chordNote = songNote;

                                            chordNote.String = str;
                                            chordNote.Fret = notes.Chords[songNote.ChordID].Frets[str];
                                            chordNote.ChordID = note.ChordId;
                                            chordNote.FingerID = songNote.FingerID;

                                            if (chordNotes.BendData[str].UsedCount > 0)
                                            {
                                                chordNote.CentsOffsets = new CentsOffset[chordNotes.BendData[str].UsedCount];

                                                for (int i = 0; i < chordNotes.BendData[str].UsedCount; i++)
                                                {
                                                    chordNote.CentsOffsets[i] = new CentsOffset()
                                                    {
                                                        TimeOffset = chordNotes.BendData[str].BendData32[i].Time,
                                                        Cents = (int)(chordNotes.BendData[str].BendData32[i].Step * 100)
                                                    };
                                                }
                                            }

                                            chordNote.Techniques = ConvertTechniques((NoteMaskFlag)chordNotes.NoteMask[str]);
                                            chordNote.Techniques |= ESongNoteTechnique.ChordNote;
                                            chordNote.SlideFret = (sbyte)chordNotes.SlideTo[str];

                                            if (chordNote.SlideFret <= 0)
                                            {
                                                chordNote.SlideFret = (sbyte)chordNotes.SlideUnpitchTo[str];
                                            }

                                            notesToAdd.Add(chordNote);
                                        }
                                    }

                                    if (notesToAdd.Count > 0)
                                    {
                                        bool haveNotes = false;

                                        for (int i = 0; i < notesToAdd.Count; i++)
                                        {
                                            if ((notesToAdd[i].Techniques != notesToAdd[0].Techniques) || (notesToAdd[i].SlideFret != -1) || (notesToAdd[i].CentsOffsets != null))
                                            {
                                                haveNotes = true;

                                                break;
                                            }
                                        }

                                        if (haveNotes)
                                        {
                                            foreach (SongNote toAdd in notesToAdd)
                                            {
                                                notes.Notes.Add(toAdd);
                                            }

                                            songNote.Techniques |= ESongNoteTechnique.ChordNote;
                                            songNote.TimeLength = 0;
                                        }
                                        else
                                        {
                                            // No distinct information in the chord notes, but add any techniques they share
                                            songNote.Techniques |= notesToAdd[0].Techniques;
                                            // Except ChordNote
                                            songNote.Techniques &= ~ESongNoteTechnique.ChordNote;
                                        }
                                    }
                                }

                                notes.Notes.Add(songNote);
                            }

                            break;
                        }
                    }
                }

                saveNotes(partName, notes);
            }

            return part;
        }

        static ESongNoteTechnique ConvertTechniques(NoteMaskFlag noteMask)
        {
            ESongNoteTechnique technique = new ESongNoteTechnique();

            if (noteMask.HasFlag(NoteMaskFlag.HAMMERON))
                technique |= ESongNoteTechnique.HammerOn;

            if (noteMask.HasFlag(NoteMaskFlag.PULLOFF))
                technique |= ESongNoteTechnique.PullOff;

            if (noteMask.HasFlag(NoteMaskFlag.ACCENT))
                technique |= ESongNoteTechnique.Accent;

            if (noteMask.HasFlag(NoteMaskFlag.PALMMUTE))
                technique |= ESongNoteTechnique.PalmMute;

            if (noteMask.HasFlag(NoteMaskFlag.MUTE) && !noteMask.HasFlag(NoteMaskFlag.FRETHANDMUTE))
                technique |= ESongNoteTechnique.PalmMute;

            if (noteMask.HasFlag(NoteMaskFlag.FRETHANDMUTE))
                technique |= ESongNoteTechnique.FretHandMute;

            if (noteMask.HasFlag(NoteMaskFlag.SLIDE) || noteMask.HasFlag(NoteMaskFlag.SLIDEUNPITCHEDTO))
                technique |= ESongNoteTechnique.Slide;

            if (noteMask.HasFlag(NoteMaskFlag.TREMOLO))
                technique |= ESongNoteTechnique.Tremolo;

            if (noteMask.HasFlag(NoteMaskFlag.VIBRATO))
                technique |= ESongNoteTechnique.Vibrato;

            if (noteMask.HasFlag(NoteMaskFlag.HARMONIC))
                technique |= ESongNoteTechnique.Harmonic;

            if (noteMask.HasFlag(NoteMaskFlag.PINCHHARMONIC))
                technique |= ESongNoteTechnique.PinchHarmonic;

            if (noteMask.HasFlag(NoteMaskFlag.TAP))
                technique |= ESongNoteTechnique.Tap;

            if (noteMask.HasFlag(NoteMaskFlag.SLAP))
                technique |= ESongNoteTechnique.Slap;

            if (noteMask.HasFlag(NoteMaskFlag.POP))
                technique |= ESongNoteTechnique.Pop;

            if (noteMask.HasFlag(NoteMaskFlag.CHORD))
                technique |= ESongNoteTechnique.Chord;

            if (noteMask.HasFlag(NoteMaskFlag.ARPEGGIO))
                technique |= ESongNoteTechnique.Arpeggio;

            if (noteMask.HasFlag(NoteMaskFlag.BEND))
                technique |= ESongNoteTechnique.Bend;

            if (noteMask.HasFlag(NoteMaskFlag.CHILD))
                technique |= ESongNoteTechnique.Continued;

            return technique;
        }

        private SongData GetSongData(PsarcSongEntry songEntry) {
            return new SongData() {
                SongName = songEntry.SongName,
                SongYear = songEntry.SongYear,
                SongLengthSeconds = songEntry.SongLengthSeconds,
                ArtistName = songEntry.ArtistName,
                AlbumName = songEntry.AlbumName,
            };
        }

        void ProcessArrangements(PsarcSongEntry songEntry, PsarcDecoder decoder, SongData songData, out SongStructure songStructure, Action<string, List<SongVocal>> saveVocals, Action<string, SongInstrumentNotes> saveNotes) {
            songStructure = new SongStructure();

            foreach (string arrangementName in songEntry.Arrangements.Keys) {
                try {
                    SongArrangement arrangement = songEntry.Arrangements[arrangementName];

                    SngAsset asset = decoder.GetSongAsset(songEntry.SongKey, arrangementName);

                    List<SongSection> partSections = new List<SongSection>();

                    if (asset != null) {
                        if ((asset.BPMs != null) && (asset.BPMs.Length > songStructure.Beats.Count)) {
                            songStructure.Beats.Clear();

                            foreach (Bpm bpm in asset.BPMs) {
                                songStructure.Beats.Add(new SongBeat {
                                    TimeOffset = bpm.Time,
                                    IsMeasure = (bpm.Mask > 0),
                                });
                            }
                        }

                        if (asset.PhraseIterations != null) {
                            foreach (PhraseIteration it in asset.PhraseIterations) {
                                Phrase phrase = asset.Phrases[it.PhraseId];

                                SongSection songSection = new SongSection {
                                    Name = phrase.Name,
                                    StartTime = it.StartTime,
                                    EndTime = it.NextPhraseTime
                                };

                                partSections.Add(songSection);
                            }
                        }

                        //if (asset.Sections != null)
                        //{
                        //    songStructure.Sections.Clear();

                        //    foreach (Section section in asset.Sections)
                        //    {
                        //        SongSection songSection = new SongSection
                        //        {
                        //            Name = section.Name,
                        //            StartTime = section.StartTime,
                        //            EndTime = section.EndTime
                        //        };

                        //        partSections.Add(songSection);
                        //    }
                        //}

                        SongInstrumentPart part = CreateInstrumentPart(arrangementName, partSections, arrangement, asset, decoder, saveVocals, saveNotes);

                        songData.AddOrReplacePart(part);

                        if (songData.A440CentsOffset == 0)
                            songData.A440CentsOffset = arrangement.Attributes.CentOffset;
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Error: " + ex.ToString());
                }
            }
        }

        private void SaveAlbumArt(PsarcSongEntry songEntry, PsarcDecoder decoder, Action<System.Drawing.Bitmap> save) {
            try {
                DdsAsset albumArt = decoder.GetAlbumArtAsset(songEntry.SongKey, 256);

                if (albumArt != null) {
                    save(albumArt.Bitmap);
                }
            } catch (Exception ex) {
                Console.WriteLine("Error createing album art: " + ex.ToString());
            }
        }

        private void SaveAudioFile(PsarcSongEntry songEntry, PsarcDecoder songsDecoder, Action<byte[]> save) {
            PsarcTOCEntry bankEntry = songsDecoder.GetTOCEntry(songEntry.SongBank);

            TextWriter consoleOut = Console.Out;

            if (bankEntry != null) {
                try {
                    using (MemoryStream outputStream = new MemoryStream()) {
                        // Suppress Ww2ogg logging
                        Console.SetOut(TextWriter.Null);

                        songsDecoder.WriteOgg(songEntry.SongKey, outputStream, bankEntry);
                        save(outputStream.ToArray());

                        Console.SetOut(consoleOut);
                    }
                } catch (Exception ex) {
                    Console.SetOut(consoleOut);

                    Console.WriteLine("Failed to create audio - " + ex.ToString());
                }
            }
        }
    }
}
