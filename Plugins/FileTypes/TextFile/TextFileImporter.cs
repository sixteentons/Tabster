#region

using System.IO;
using Tabster.Core.Types;
using Tabster.Data;
using Tabster.Data.Processing;

#endregion

namespace TextFile
{
    public class TextFileImporter : ITablatureFileImporter
    {
        public TextFileImporter()
        {
            FileType = new FileType("Text File", ".txt");
        }

        #region Implementation of ITablatureDocumentImporter

        public FileType FileType { get; private set; }

        public AttributedTablature Import(string fileName)
        {
            var contents = File.ReadAllText(fileName);
            var doc = new AttributedTablature {Contents = contents};
            return doc;
        }

        public AttributedTablature Import(string fileName, string artist, string title, TablatureType type)
        {
            var doc = Import(fileName);
            doc.Artist = artist;
            doc.Title = title;
            doc.Type = type;
            return doc;
        }

        #endregion
    }
}