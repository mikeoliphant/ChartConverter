using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Midi;
using SongFormat;

namespace ChartConverter
{
    public class RockBandIni
    {
        public string Song { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int MidiDelay { get; set; } = 0;
        public bool IsRockBand4 { get; set; } = false;
    }

    public enum EFretsOnFireDifficulty
    {
        Easy,
        Medium,
        Hard,
        Expert
    }

    public class RockBandConverter
    {
        public Func<string, bool> UpdateAction { get; set; }

        string destPath;
        bool convertAudio;

        public RockBandConverter(string destPath, bool convertAudio)
        {
            this.destPath = destPath;
            this.convertAudio = convertAudio;
        }

        public static int GetNoteOctave(int midiNoteNum)
        {
            return (midiNoteNum / 12) - 1;
        }

        public RockBandIni LoadSongIni(string filename)
        {
            if (File.Exists(filename))
            {
                RockBandIni ini = new RockBandIni();

                using (StreamReader reader = new StreamReader(filename))
                {
                    string line = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("["))
                            continue;

                        string[] values = line.Split('=');

                        if (values.Length < 2)
                            continue;

                        string key = values[0].Trim().ToLower();

                        string value = string.Join("=", values, 1, values.Length - 1).Trim();

                        if (key == "name")
                        {
                            ini.Song = value;
                        }
                        else if (key == "artist")
                        {
                            ini.Artist = value;
                        }
                        else if (key == "album")
                        {
                            ini.Album = value;
                        }
                        else if (key == "delay")
                        {
                            int delay = 0;

                            if (int.TryParse(value, out delay))
                            {
                                ini.MidiDelay = delay;
                            }
                        }
                        else if (key == "icon")
                        {
                            if (value.ToLower() == "rb4")
                            {
                                ini.IsRockBand4 = true;
                            }
                        }
                    }
                }

                return ini;
            }

            return null;
        }

        public bool ConvertSong(string songFolder)
        {
            string relativeSongFolder = Path.GetRelativePath(destPath, songFolder);

            RockBandIni ini = LoadSongIni(Path.Combine(songFolder, "song.ini"));

            SongData songData = new SongData()
            {
                SongName = ini.Song,
                ArtistName = ini.Artist,
                AlbumName = ini.Album
            };

            MidiFile midiFile = new MidiFile(Path.Combine(songFolder, "notes.mid"), strictChecking: false);

            string artistDir = Path.Combine(destPath, SerializationUtil.GetSafeFilename(songData.ArtistName));

            if (!Directory.Exists(artistDir))
            {
                Directory.CreateDirectory(artistDir);
            }

            string songDir = Path.Combine(artistDir, SerializationUtil.GetSafeFilename(songData.SongName));

            if (!Directory.Exists(songDir))
            {
                Directory.CreateDirectory(songDir);
            }

            if (UpdateAction != null)
            {
                if (!UpdateAction(songData.ArtistName + " - " + songData.SongName))
                {
                    return false;
                }
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

            int ticksPerBeat = midiFile.DeltaTicksPerQuarterNote;

            List<(long Tick, int Tempo)> tempoMap = new List<(long Tick, int Tempo)>();
            bool haveTempoMap = false;

            SongStructure songStructure = new SongStructure();
            SongDrumNotes drumNotes = new SongDrumNotes();
            SongKeyboardNotes keyboardNotes = new SongKeyboardNotes();
            List<SongVocal> vocals = new List<SongVocal>();
            SongSection lastSection = null;

            bool tom1 = false;
            bool tom2 = false;
            bool tom3 = false;
            bool haveHiHatAnim = false;
            bool haveOpenHiHatAnim = false;
            bool haveRideAnim = false;
            bool haveTom1Anim = false;
            bool haveSnareAnim = false;
            bool haveCrash1Anim = false;
            bool haveCrash2Anim = false;
            bool haveChoke1Anim = false;
            bool haveChoke2Anim = false;
            bool havePercussionAnim = false;
            bool doFlam = false;
            bool doChoke = false;
            Dictionary<int, int> noteHash = new Dictionary<int, int>();
            List<SongDrumNote> noteEvents = new List<SongDrumNote>();

            foreach (var track in midiFile.Events)
            {
                Dictionary<int, long> noteDict = new Dictionary<int, long>();

                bool isDrums = false;
                bool doDiscoFlip = false;
                bool isKeys = false;
                bool isVocals = false;
                bool isEvents = false;
                bool isBeats = false;

                bool tempoMapEnded = false;

                int currentMicrosecondsPerQuarterNote = 0;
                long currentMicrosecond = 0;
                long currentTick = 0;

                var tempoEnumerator = tempoMap.GetEnumerator();

                if (tempoMap.Count > 0)
                    haveTempoMap = true;

                if (haveTempoMap)
                {
                    tempoEnumerator.MoveNext();
                    currentMicrosecondsPerQuarterNote = tempoEnumerator.Current.Tempo;
                }

                for (int e = 0; e < track.Count; e++)
                {
                    MidiEvent midiEvent = track[e];

                    if (haveTempoMap)
                    {
                        long ticksLeft = midiEvent.DeltaTime;

                        while (ticksLeft > 0)
                        {
                            if (!tempoMapEnded && tempoEnumerator.Current.Tick < (currentTick + ticksLeft))
                            {
                                // We're out of ticks at our current tempo, so do a delta and update the tempo
                                long delta = tempoEnumerator.Current.Tick - currentTick;

                                currentMicrosecond += ((long)delta * (long)currentMicrosecondsPerQuarterNote) / (long)ticksPerBeat;

                                currentTick += delta;
                                ticksLeft -= delta;

                                currentMicrosecondsPerQuarterNote = tempoEnumerator.Current.Tempo;

                                if (!tempoEnumerator.MoveNext())
                                {
                                    tempoMapEnded = true;
                                }

                                continue;
                            }
                            else
                            {
                                // The current event fits without updating tempo
                                currentMicrosecond += ((long)ticksLeft * (long)currentMicrosecondsPerQuarterNote) / (long)ticksPerBeat;

                                currentTick += ticksLeft;
                                ticksLeft = 0;
                            }
                        }
                    }

                    if (midiEvent is MetaEvent)
                    {
                        MetaEvent metaEvent = midiEvent as MetaEvent;

                        if (metaEvent.MetaEventType == MetaEventType.SequenceTrackName)
                        {
                            TextEvent textEvent = metaEvent as TextEvent;

                            string lower = textEvent.Text.ToLower();

                            if (lower.EndsWith("drums"))
                            {
                                isDrums = true;

                                songData.AddOrReplacePart(new SongInstrumentPart
                                {
                                    InstrumentName = "drums",
                                    InstrumentType = ESongInstrumentType.Drums,
                                    SongAudio = relativeSongFolder,
                                    ArrangementName = "rbarrangement"
                                });
                            }
                            else if (lower.EndsWith("beat"))
                            {
                                isBeats = true;
                            }
                            else if (lower.EndsWith("vocals"))
                            {
                                isVocals = true;

                                songData.AddOrReplacePart(new SongInstrumentPart
                                {
                                    InstrumentName = "rbvocals",
                                    InstrumentType = ESongInstrumentType.Vocals,
                                    SongAudio = relativeSongFolder,
                                    ArrangementName = "rbarrangement"
                                });
                            }
                            //else if (lower.Contains("harm"))
                            //{
                            //    isVocals = true;

                            //    vocalVoice = (lower[lower.Length - 1] - '1') + 1;
                            //}
                            else if (lower.EndsWith("real_bass"))
                            {
                            }
                            else if (lower.EndsWith("real_keys_x"))
                            {
                                isKeys = true;

                                songData.AddOrReplacePart(new SongInstrumentPart
                                {
                                    InstrumentName = "keys",
                                    InstrumentType = ESongInstrumentType.Keys,
                                    SongAudio = relativeSongFolder,
                                    ArrangementName = "rbarrangement"
                                });
                            }
                            else if (lower.EndsWith("events"))
                            {
                                isEvents = true;
                            }
                        }
                        else if (metaEvent.MetaEventType == MetaEventType.SetTempo)
                        {
                            TempoEvent tempoEvent = metaEvent as TempoEvent;

                            tempoMap.Add((tempoEvent.AbsoluteTime, tempoEvent.MicrosecondsPerQuarterNote));
                        }
                        else if (midiEvent is TextEvent)
                        {
                            if (isDrums)
                            {
                                TextEvent textEvent = midiEvent as TextEvent;

                                string lower = textEvent.Text.ToLower();

                                if (Regex.IsMatch(lower, @"\[mix 3 drums.d\]"))
                                {
                                    doDiscoFlip = true;
                                }
                                else if (Regex.IsMatch(lower, @"\[mix 3 drums.\]"))
                                {
                                    doDiscoFlip = false;
                                }
                            }
                            else if (isEvents)
                            {
                                TextEvent textEvent = midiEvent as TextEvent;

                                string lower = textEvent.Text.ToLower();

                                Match match = Regex.Match(lower, @"\[section (\w+)\]");

                                if (match.Success)
                                {
                                    Capture c = match.Groups[1].Captures[0];

                                    float time = (float)((double)currentMicrosecond / 1000000.0);

                                    if (lastSection != null)
                                        lastSection.EndTime = time;

                                    var section = new SongSection()
                                    {
                                        Name = c.Value,
                                        StartTime = time
                                    };

                                    songStructure.Sections.Add(section);

                                    lastSection = section;
                                }
                            }
                            else if (isVocals)
                            {
                                string text = (midiEvent as TextEvent).Text;

                                if ((text == "+") || text.StartsWith("["))
                                {

                                }
                                else
                                {
                                    if (text.EndsWith("#") || text.EndsWith("^") || text.EndsWith("="))
                                    {
                                        text = text.Substring(0, text.Length - 1);
                                    }

                                    vocals.Add(new SongVocal()
                                    {
                                        TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                        Vocal = text
                                    });
                                }
                            }
                        }
                    }
                    else if (midiEvent is NoteEvent)
                    {
                        if (!haveTempoMap)
                            throw new InvalidOperationException("Can't have a midi note before tempo map is established");

                        NoteEvent noteEvent = midiEvent as NoteEvent;

                        if (isDrums)
                        {
                            int vel = (noteEvent.CommandCode == MidiCommandCode.NoteOn) ? noteEvent.Velocity : 0;

                            int nn = noteEvent.NoteNumber;

                            if (doDiscoFlip && !tom1)
                            {
                                if (nn == 97)
                                    nn = 98;
                                else if (nn == 98)
                                    nn = 97;
                            }

                            if (noteHash.ContainsKey(nn))
                            {
                                noteHash[nn] = Math.Max(vel, noteHash[nn]);
                            }
                            else
                            {
                                noteHash[nn] = vel;
                            }
                        }
                        else if (isKeys)
                        {
                            if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                            {
                                noteDict[noteEvent.NoteNumber] = currentMicrosecond;
                            }
                            else if (noteEvent.CommandCode == MidiCommandCode.NoteOff)
                            {
                                if (!noteDict.ContainsKey(noteEvent.NoteNumber))
                                {
                                    throw new InvalidDataException("Note off without previous note on: " + noteEvent.ToString());
                                }

                                if (noteEvent.NoteNumber > 0)
                                {
                                    long start = noteDict[noteEvent.NoteNumber];

                                    int ticks = (int)(((currentMicrosecond - start) * 960) / currentMicrosecondsPerQuarterNote);

                                    SongKeyboardNote note = new SongKeyboardNote()
                                    {
                                        TimeOffset = (float)((double)start / 1000000.0),
                                        //TimeLength =  (ticks > 240) ? (float)((double)(currentMicrosecond - start) / 1000000.0) : 0,
                                        TimeLength = (float)((double)(currentMicrosecond - start) / 1000000.0),
                                        Note = noteEvent.NoteNumber,
                                        Velocity = noteEvent.Velocity
                                    };

                                    keyboardNotes.Notes.Add(note);
                                }

                                noteDict.Remove(noteEvent.NoteNumber);
                            }
                        }
                        else if (isBeats)
                        {
                            if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                            {
                                // 12 is downbeat, 13 is other beat
                                if ((noteEvent.NoteNumber == 12) || (noteEvent.NoteNumber == 13))
                                {
                                    songStructure.Beats.Add(new SongBeat()
                                    {
                                        TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                        IsMeasure = (noteEvent.NoteNumber == 12)
                                    });
                                }
                            }
                        }
                    }

                    if ((e == (track.Count - 1)) || (track[e + 1].DeltaTime > 0))
                    {
                        bool flamVal = false;
                        int flamVelocity = 0;
                        EFretsOnFireDifficulty difficulty = EFretsOnFireDifficulty.Easy;

                        // first do a pass of modifier notes
                        foreach (int noteNumber in noteHash.Keys)
                        {
                            int velocity = noteHash[noteNumber];
                            bool val = (velocity > 0);

                            switch (noteNumber)
                            {
                                case 25:
                                    haveOpenHiHatAnim = val;
                                    break;

                                case 30:
                                case 31:
                                    haveHiHatAnim = val;
                                    break;

                                case 32:
                                    havePercussionAnim = val;
                                    break;

                                case 26:
                                case 27:
                                case 28:
                                case 29:
                                    haveSnareAnim = val;
                                    break;

                                case 34:
                                case 35:
                                case 36:
                                case 37:
                                    haveCrash1Anim = val;
                                    break;

                                case 38:
                                case 39:
                                case 44:
                                case 45:
                                    haveCrash2Anim = val;
                                    break;

                                case 40:
                                    haveChoke1Anim = val;
                                    break;

                                case 41:
                                    haveChoke2Anim = val;
                                    break;

                                case 42:
                                case 43:
                                    haveRideAnim = val;
                                    break;

                                case 46:
                                case 47:
                                    haveTom1Anim = val;
                                    break;

                                case 110:
                                    tom1 = val;
                                    break;

                                case 111:
                                    tom2 = val;
                                    break;

                                case 112:
                                    tom3 = val;
                                    break;
                            }
                        }

                        // now do actual notes
                        foreach (int noteNumber in noteHash.Keys)
                        {
                            int velocity = noteHash[noteNumber];
                            bool val = (velocity > 0);

                            int octave = GetNoteOctave(noteNumber);

                            switch (octave)
                            {
                                case 4:
                                    difficulty = EFretsOnFireDifficulty.Easy;
                                    break;

                                case 5:
                                    difficulty = EFretsOnFireDifficulty.Medium;
                                    break;

                                case 6:
                                    difficulty = EFretsOnFireDifficulty.Hard;
                                    break;

                                case 7:
                                    difficulty = EFretsOnFireDifficulty.Expert;
                                    break;
                            }

                            //currentTimeline = fretsOnFireTimeline[(int)difficulty];

                            EDrumKitPiece kitPiece = EDrumKitPiece.None;
                            EDrumArticulation articulation = EDrumArticulation.None;

                            switch (noteNumber)
                            {
                                // Kick
                                case 60:
                                case 72:
                                case 84:
                                case 96:
                                    kitPiece = EDrumKitPiece.Kick;

                                    break;

                                // Snare
                                case 61:
                                case 73:
                                case 85:
                                case 97:
                                    kitPiece = EDrumKitPiece.Snare;
                                    break;

                                // HiHat or Tom1
                                case 62:
                                case 74:
                                case 86:
                                case 98:
                                    if (tom1)
                                    {

                                        kitPiece = EDrumKitPiece.Tom1;
                                    }
                                    else
                                    {
                                        kitPiece = EDrumKitPiece.HiHat;

                                        if (haveOpenHiHatAnim && !haveRideAnim)
                                            articulation = EDrumArticulation.HiHatOpen;
                                    }

                                    // Do a flam if there is no tom playing in the animation, and we have a snare hit (but don't worry about snare animation)
                                    if ((kitPiece == EDrumKitPiece.Tom1) && noteHash.ContainsKey(noteNumber - 1) && (noteHash[noteNumber - 1] > 0)) // && (!haveTom1Anim) && (haveSnareAnim))
                                    {
                                        kitPiece = EDrumKitPiece.None;

                                        doFlam = true;
                                        flamVal = val;
                                        flamVelocity = velocity;
                                    }

                                    if (kitPiece == EDrumKitPiece.HiHat)
                                    {
                                        // Swtich hi hat to crash2 if we have no hihat anim and have both crash 1&2 anim
                                        if (!haveHiHatAnim)
                                        {
                                            if (haveCrash1Anim && haveCrash2Anim)
                                            {
                                                kitPiece = EDrumKitPiece.Crash2;
                                            }
                                            else if (haveCrash1Anim)
                                            {
                                                // If we have a crash, make it crash2
                                                if (((difficulty == EFretsOnFireDifficulty.Easy) && noteHash.ContainsKey(64) && (noteHash[64] > 0)) ||
                                                    ((difficulty == EFretsOnFireDifficulty.Medium) && noteHash.ContainsKey(76) && (noteHash[76] > 0)) ||
                                                    ((difficulty == EFretsOnFireDifficulty.Hard) && noteHash.ContainsKey(88) && (noteHash[88] > 0)) ||
                                                    ((difficulty == EFretsOnFireDifficulty.Expert) && noteHash.ContainsKey(100) && (noteHash[100] > 0)))
                                                {
                                                    kitPiece = EDrumKitPiece.Crash2;
                                                }
                                                else
                                                {
                                                    kitPiece = EDrumKitPiece.Crash;
                                                }
                                            }
                                            else if (haveCrash2Anim)
                                            {
                                                kitPiece = EDrumKitPiece.Crash2;
                                            }
                                        }
                                    }
                                    break;

                                // Ride or Tom2
                                case 63:
                                case 75:
                                case 87:
                                case 99:
                                    if (tom2)
                                    {
                                        kitPiece = EDrumKitPiece.Tom2;
                                    }
                                    else
                                    {
                                        kitPiece = EDrumKitPiece.Ride;
                                    }

                                    // If there is no ride animation, see if we have something else in the animation
                                    if ((kitPiece == EDrumKitPiece.Ride) && !haveRideAnim && val)
                                    {
                                        // If there is an open hi hat, do that
                                        if (haveHiHatAnim && haveOpenHiHatAnim)
                                        {
                                            kitPiece = EDrumKitPiece.HiHat;
                                            articulation = EDrumArticulation.HiHatOpen;
                                        }
                                        // If there is crash, switch to that
                                        else if (haveCrash1Anim)
                                        {
                                            // If we have a crash, make it crash2
                                            if (((difficulty == EFretsOnFireDifficulty.Easy) && noteHash.ContainsKey(64) && (noteHash[64] > 0)) ||
                                                ((difficulty == EFretsOnFireDifficulty.Medium) && noteHash.ContainsKey(76) && (noteHash[76] > 0)) ||
                                                ((difficulty == EFretsOnFireDifficulty.Hard) && noteHash.ContainsKey(88) && (noteHash[88] > 0)) ||
                                                ((difficulty == EFretsOnFireDifficulty.Expert) && noteHash.ContainsKey(100) && (noteHash[100] > 0)))
                                            {
                                                kitPiece = EDrumKitPiece.Crash2;
                                            }
                                            else
                                            {
                                                kitPiece = EDrumKitPiece.Crash2;
                                            }
                                        }
                                        else if (haveCrash2Anim)
                                        {
                                            kitPiece = EDrumKitPiece.Crash2;
                                        }
                                    }
                                    break;

                                // Crash or Tom3
                                case 64:
                                case 76:
                                case 88:
                                case 100:
                                    kitPiece = tom3 ? EDrumKitPiece.Tom3 : EDrumKitPiece.Crash;

                                    // Switch to crash2 if we only have a crash2 anim
                                    if ((kitPiece == EDrumKitPiece.Crash) && !haveCrash1Anim && haveCrash2Anim)
                                    {
                                        kitPiece = EDrumKitPiece.Crash2;
                                    }
                                    break;
                                default:
                                    break;
                            }

                            if ((kitPiece != EDrumKitPiece.None) && (difficulty == EFretsOnFireDifficulty.Expert))
                            {
                                if (val && (SongDrumNote.GetKitPieceType(kitPiece) == EDrumKitPieceType.Crash) && (haveChoke1Anim || haveChoke2Anim))
                                {
                                    doChoke = true;
                                }

                                if (val)
                                {
                                    noteEvents.Add(new SongDrumNote()
                                    {
                                        TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                        KitPiece = kitPiece,
                                        Articulation = articulation
                                    });
                                }
                            }
                        }

                        if (noteEvents.Count > 0)
                        {
                            drumNotes.Notes.AddRange(noteEvents);

                            noteEvents.Clear();
                        }

                        if (doFlam)
                        {
                            noteEvents.Add(new SongDrumNote()
                            {
                                TimeOffset = (float)((double)currentMicrosecond / 1000000.0) + (currentMicrosecondsPerQuarterNote * .1f),
                                KitPiece = EDrumKitPiece.Snare
                            });
                        }

                        if (doChoke)
                        {
                            noteEvents.Add(new SongDrumNote()
                            {
                                TimeOffset = (float)((double)currentMicrosecond / 1000000.0),
                                KitPiece = EDrumKitPiece.Crash,
                                Articulation = EDrumArticulation.CymbalChoke
                            });

                            doChoke = false;
                        }

                        doFlam = false;

                        noteHash.Clear();
                    }
                }
                
                if (isDrums)
                {
                    drumNotes.Notes.Sort((a, b) => ((a.TimeOffset == b.TimeOffset) ? (b.KitPiece.CompareTo(a.KitPiece)) : a.TimeOffset.CompareTo(b.TimeOffset)));

                    using (FileStream stream = File.Create(Path.Combine(songDir, "drums.json")))
                    {
                        JsonSerializer.Serialize(stream, drumNotes, SerializationUtil.CondensedSerializerOptions);
                    }
                }
                else if (isKeys)
                {
                    using (FileStream stream = File.Create(Path.Combine(songDir, "keys.json")))
                    {
                        JsonSerializer.Serialize(stream, keyboardNotes, SerializationUtil.CondensedSerializerOptions);
                    }
                }
                else if (isVocals)
                {
                    if (vocals.Count > 0)
                    {
                        ChartUtil.FormatVocals(vocals);

                        using (FileStream stream = File.Create(Path.Combine(songDir, "rbvocals.json")))
                        {
                            JsonSerializer.Serialize(stream, vocals, SerializationUtil.CondensedSerializerOptions);
                        }
                    }
                }
                else if (isEvents)
                {
                    if (lastSection != null)
                        lastSection.EndTime = (float)((double)currentMicrosecond / 1000000.0);
                }
            }

            string albumPath = Path.Combine(songDir, "albumart.png");

            if (!File.Exists(albumPath))
            {
                File.Copy(Path.Combine(songFolder, "album.png"), albumPath);
            }

            using (FileStream stream = File.Create(Path.Combine(songDir, "song.json")))
            {
                JsonSerializer.Serialize(stream, songData, SerializationUtil.IndentedSerializerOptions);
            }

            using (FileStream stream = File.Create(Path.Combine(songDir, "rbarrangement.json")))
            {
                JsonSerializer.Serialize(stream, songStructure, SerializationUtil.CondensedSerializerOptions);
            }

            if (convertAudio)
            {
            }

            return true;
        }

        public bool ConvertAll(string path)
        {
            if (File.Exists(Path.Combine(path, "song.ini")))
            {
                try
                {
                    if (!ConvertSong(path))
                        return false;
                }
                catch (Exception ex)
                {

                }
            }

            foreach (string folder in Directory.GetDirectories(path))
            {
                if (!ConvertAll(folder))
                    return false;
            }

            return true;
        }
    }
}