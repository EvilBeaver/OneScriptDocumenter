using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using documenter.Generators;
using documenter.XmlDoc;
using Newtonsoft.Json;

namespace documenter
{
    class Program
    {
        static int Main(string[] args)
        {
            int retCode = 0;

            try
            {
                retCode = ProcessAssemblies(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                retCode = 128;
            }

            if (System.Diagnostics.Debugger.IsAttached)
                Console.ReadKey();

            return retCode;
        }

        private static int ProcessAssemblies(string[] args)
        {
            var cmdLineArgs = new CommandLineArgs(args);
            var projectFile = cmdLineArgs.Next();
            if (projectFile == null)
            {
                ShowUsage();
                return 1;
            }

            var outputFile = cmdLineArgs.Next();
            if (outputFile == null)
            {
                ShowUsage();
                return 1;
            }

            DocumentationFormat docFormat;
            var format = cmdLineArgs.Next();
            string baseUrl = null;
            if (format == "-base")
            {
                baseUrl = cmdLineArgs.Next();
                if (baseUrl == null)
                {
                    ShowUsage();
                    return 1;
                }
                format = cmdLineArgs.Next();
            }

            if (format != null)
            {
                switch (format)
                {
                    case "xml":
                        docFormat = DocumentationFormat.Markdown;
                        break;
                    case "json":
                        docFormat = DocumentationFormat.JSON;
                        break;
                    case "html":
                        docFormat = DocumentationFormat.HTML;
                        break;
                    default:
                    {
                        ShowUsage();
                        return 1;
                    }
                }               
            }
            else
            {
                docFormat = DocumentationFormat.Markdown;
            }

            return CreateDocumentation(projectFile, outputFile, docFormat, baseUrl);
        }

        private static int CreateDocumentation(string projectFile, string outputFile, DocumentationFormat docFormat, string baseUrl)
        {
            DocumentationProject project;
            using (var reader = new StreamReader(projectFile))
            {
                project = JsonConvert.DeserializeObject<DocumentationProject>(reader.ReadToEnd());
            }

            var metadata = ReadAssemblyXmlDocs(project);
            IDocsGenerator generator;
            switch (docFormat)
            {
                case DocumentationFormat.Markdown:
                    generator = new MarkdownGenerator();
                    generator.BaseUrl = baseUrl;
                    break;
                default:
                    throw new NotImplementedException();
            }

            generator.Project = project;
            generator.DumpTo(outputFile, metadata);

            return 0;
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("OneScriptDocumenter.exe <input-file> <output-file> [xml|json|html] [-base url]");
        }

        private static XDocument ReadAssemblyXmlDocs(DocumentationProject project)
        {
            var xmlDocProcessor = new XmlGenerator();
            return xmlDocProcessor.CreateDocumentation(project.Assemblies);
        }
    }
}
