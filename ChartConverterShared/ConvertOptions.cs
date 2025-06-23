using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;


namespace ChartConverter
{
    public class ConvertOptions
    {
        public string SongOutputPath { get; set; } = "";
        public List<string> PsarcFiles { get; private set; } =  new();
        public List<string> PsarcFolders { get; private set; } = new();
        public bool CopyRockBandAudio { get; set; } = true;
        public List<string> RockBandFolders { get; private set; } = new();
        public bool ConvertPsarc { get; set; } = true;
        public bool ConvertRockBand { get; set; } = true;

        public static ConvertOptions Load(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ConvertOptions));

            using (Stream inputStream = File.OpenRead(path))
            {
                return serializer.Deserialize(inputStream) as ConvertOptions;
            }
        }

        public void Save(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ConvertOptions));

            using (Stream outputStream = File.Create(path))
            {
                serializer.Serialize(outputStream, this);
            }
        }
    }
}
