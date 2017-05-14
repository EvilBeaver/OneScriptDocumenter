using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace OneScriptDocumenter
{
    class DocumentBlocks
    {
        public StringBuilder TextGlobalContext;
        public StringBuilder TextContextDescription;
        public StringBuilder TextEnumsDescription;

        public DocumentBlocks()
        {
            TextGlobalContext = new StringBuilder();
            TextContextDescription = new StringBuilder();
            TextEnumsDescription = new StringBuilder();
        }
    }

    class Documenter
    {
        internal XDocument CreateDocumentation(List<string> assemblies)
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

                var docMaker = new AssemblyDocumenter(assembly, xmlName);
                var asmDoc = docMaker.CreateDocumentation();
                result.Root.Add(new XElement("assembly",
                    new XAttribute("name", name),
                    asmDoc.Root));
            }
            Console.WriteLine("Done");
            return result;
        }
        internal string CreateDocumentationJSON(string pathOutput, List<string> assemblies)
        {
            using (StreamWriter sbJSON = new StreamWriter(pathOutput))
            {

                DocumentBlocks textBlocks = new DocumentBlocks();

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

                    var docMaker = new AssemblyDocumenter(assembly, xmlName);
                    docMaker.CreateDocumentationJSON(textBlocks);
                }

                sbJSON.WriteLine("export function globalContextOscript(): any {\nconst data = {");
                sbJSON.WriteLine(textBlocks.TextGlobalContext);
                sbJSON.WriteLine("    };\n    return data;\n}\n\nexport function classesOscript(): any {\nconst data = {");


                sbJSON.WriteLine(textBlocks.TextContextDescription);

                sbJSON.WriteLine("    };\n    return data;\n}\n\nexport function systemEnum(): any {\nconst data = {");

                sbJSON.WriteLine(textBlocks.TextEnumsDescription);
                sbJSON.WriteLine("    };\n    return data;\n}");

                Console.WriteLine("Done");
                return "";
            }
        }
    }
}
