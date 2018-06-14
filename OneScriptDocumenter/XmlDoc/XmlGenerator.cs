using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OneScriptDocumenter.XmlDoc
{
    public class XmlGenerator
    {
        public XDocument CreateDocumentation(List<string> assemblies)
        {
            XDocument result = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XElement("oscript-docs"));

            foreach (var assembly in assemblies)
            {
                var name = Path.GetFileNameWithoutExtension(assembly);
                var xmlName = Path.Combine(Path.GetDirectoryName(assembly), name + ".xml");
                if (!File.Exists(xmlName))
                {
                    Console.WriteLine("Missing xml-doc: {0}", xmlName);
                    continue;
                }

                Console.WriteLine("Processing: {0}", name);

                var docMaker = new AssemblyXmlDocProcessor();
                var asmDoc = docMaker.CreateDocumentation();
                result.Root.Add(new XElement("assembly",
                    new XAttribute("name", name),
                    asmDoc.Root));
            }
            Console.WriteLine("Done");
            return result;
        }
    }
}
