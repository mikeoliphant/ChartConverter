using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PsarcUtil;
using Rocksmith2014PsarcLib.Psarc;
using Rocksmith2014PsarcLib.Psarc.Asset;
using Rocksmith2014PsarcLib.Psarc.Models.Json;
using Rocksmith2014PsarcLib.Psarc.Models.Sng;
using SongFormat;

namespace ChartConverter
{
    public class PsarcConverter
    {
        public static SongData GetSongData(PsarcSongEntry songEntry)
        {
            SongData songData = new SongData()
            {
                SongName = songEntry.SongName,
                SongYear = songEntry.SongYear,
                SongLengthSeconds = songEntry.SongLengthSeconds,
                ArtistName = songEntry.ArtistName,
                AlbumName = songEntry.AlbumName
            };

            foreach (var arrangement in songEntry.Arrangements.Values)
            {
                if (arrangement.Attributes.CentOffset != 0)
                {
                    songData.A440CentsOffset = arrangement.Attributes.CentOffset;

                    break;
                }
            }

            return songData;
        }

        public static void WriteAlbumArtToStream(PsarcDecoder decoder, PsarcSongEntry songEntry, Stream outputStream)
        {
            DdsAsset albumArt = decoder.GetAlbumArtAsset(songEntry.SongKey, 256);

            if (albumArt != null)
            {
                albumArt.Bitmap.Save(outputStream, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static void WriteOggToStream(PsarcDecoder songsDecoder, PsarcSongEntry songEntry, Stream outputStream)
        {
            PsarcTOCEntry bankEntry = songsDecoder.GetTOCEntry(songEntry.SongBank);

            TextWriter consoleOut = Console.Out;

            if (bankEntry != null)
            {
                try
                {
                    // Suppress Ww2ogg logging
                    Console.SetOut(TextWriter.Null);

                    songsDecoder.WriteOgg(songEntry.SongKey, outputStream, bankEntry);

                    Console.SetOut(consoleOut);
                }
                catch (Exception ex)
                {
                    Console.SetOut(consoleOut);

                    Console.WriteLine("Failed to create audio: " + ex.ToString());
                }
            }
        }

        public static (SongInstrumentPart Part, SongStructure SongStructure, SongInstrumentNotes Notes, List<SongVocal> Vocals)?
            GetInstrumentPart(PsarcDecoder decoder, PsarcSongEntry songEntry, string partName)
        {
            List<SongVocal> vocals = null;
            SongInstrumentNotes notes = null;

            SongArrangement arrangement = songEntry.Arrangements[partName];

            List<SongSection> partSections = new List<SongSection>();

            SngAsset songAsset = decoder.GetSongAsset(songEntry.SongKey, partName);

            if (songAsset == null)
                return null;

            SongStructure songStructure = new();

            if (songAsset.BPMs != null)
            {
                foreach (Bpm bpm in songAsset.BPMs)
                {
                    songStructure.Beats.Add(new SongBeat
                    {
                        TimeOffset = bpm.Time,
                        IsMeasure = (bpm.Mask > 0),
                    });
                }
            }

            if (songAsset.PhraseIterations != null)
            {
                foreach (PhraseIteration it in songAsset.PhraseIterations)
                {
                    Phrase phrase = songAsset.Phrases[it.PhraseId];

                    SongSection songSection = new SongSection
                    {
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
            else if (arrangement.Attributes.ArrangementProperties.PathBass == 1)
            {
                part.InstrumentType = ESongInstrumentType.BassGuitar;
            }

            if (part.InstrumentType == ESongInstrumentType.Vocals)
            {
                vocals = new List<SongVocal>();

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

                notes = new SongInstrumentNotes();

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
                                    }
                                    ;
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
            }

            return (part, songStructure, notes, vocals);
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
    }
}
