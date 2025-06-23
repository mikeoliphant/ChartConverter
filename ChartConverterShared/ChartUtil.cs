using SongFormat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace ChartConverter
{
    public class ChartUtil
    {
        public static void FormatVocals(List<SongVocal> vocals)
        {
            int maxCharsPerLine = 40;

            List<SongVocal> formattedVocals = new List<SongVocal>();

            int charsInLine = 0;
            int lastBreakPos = 0;

            for (int pos = 0; pos < vocals.Count; pos++)
            {
                if (vocals[pos].Vocal.EndsWith('\n'))
                {
                    charsInLine = 0;
                    lastBreakPos = pos;

                    continue;
                }

                charsInLine += vocals[pos].Vocal.Length;

                if (charsInLine > maxCharsPerLine)
                {
                    pos = lastBreakPos + 1;
                    charsInLine = 0;

                    for (; pos < vocals.Count; pos++)
                    {
                        charsInLine += vocals[pos].Vocal.Length;

                        bool isGoodBreak = (pos < (vocals.Count - 2)) && (char.IsAsciiLetterUpper(vocals[pos + 1].Vocal[0]) || ((vocals[pos + 1].TimeOffset - vocals[pos].TimeOffset) > 0.5f));

                        if ((isGoodBreak && charsInLine > 20) || (charsInLine > maxCharsPerLine))
                        {
                            vocals[pos] = new SongVocal()
                            {
                                TimeOffset = vocals[pos].TimeOffset,
                                Vocal = vocals[pos].Vocal + "\n"
                            };

                            charsInLine = 0;
                            lastBreakPos = pos;

                            break;
                        }
                    }        
                }
            }
        }
    }
}
