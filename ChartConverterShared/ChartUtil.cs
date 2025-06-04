using SongFormat;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ChartConverter
{
    public class ChartUtil
    {
        public static void FormatVocals(List<SongVocal> vocals)
        {
            List<SongVocal> formattedVocals = new List<SongVocal>();

            int charsInLine = 0;
            float lastTimeOffset = 0f;

            for (int pos = 0; pos < vocals.Count; pos++)
            {
                bool isGoodBreak = char.IsAsciiLetterUpper(vocals[pos].Vocal[0]) || (vocals[pos].TimeOffset - lastTimeOffset > 0.5f);

                if ((isGoodBreak && charsInLine > 20) || (charsInLine > 35))
                {
                    vocals[pos] = new SongVocal()
                    {
                        TimeOffset = vocals[pos].TimeOffset,
                        Vocal = vocals[pos].Vocal + "\n"
                    };

                    charsInLine = 0;
                }

                charsInLine += vocals[pos].Vocal.Length;
                lastTimeOffset = vocals[pos].TimeOffset;
        }
        }
    }
}
