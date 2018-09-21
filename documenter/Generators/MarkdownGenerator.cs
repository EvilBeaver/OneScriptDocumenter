
using System;
using System.IO;
using System.Xml.Linq;

namespace documenter.Generators
{
    internal class MarkdownGenerator : IDocsGenerator
    {
        public string BaseUrl { get; set; }
        public DocumentationProject Project { get; set; }

        public void DumpTo(string destination, XDocument metadata)
        {
            if (File.Exists(destination))
            {
                throw new Exception($"Markdown must be placed in an existing directory ({destination})");
            }

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            Dump(destination, metadata);
        }

        private void Dump(string destination, XDocument metadata)
        {
            using (var writer = new StreamWriter(Path.Combine(destination, "index.md")))
            {
                WriteTableOfContents(writer, metadata);
            }
        }

        private void WriteTableOfContents(StreamWriter writer, XDocument metadata)
        {
            foreach (var section in Project.Sections)
            {
                WriteContentsSection(section, metadata);
            }
        }

        private void WriteContentsSection(Section section, XDocument metadata)
        {
            throw new NotImplementedException();
        }
    }
}