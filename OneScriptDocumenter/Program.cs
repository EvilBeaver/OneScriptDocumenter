using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Linq;

namespace OneScriptDocumenter
{
    class Program
    {
        static int Main(string[] args)
        {
            int retCode = 0;

            try
            {
                if (args.Length > 0 && args[0] == "markdown")
                {
                    retCode = GenerateMarkdown(args);
                }
                if (args.Length > 0 && args[0] == "html")
                {
                    retCode = GenerateHtml(args);
                }
                else
                {
                    retCode = GenerateXml(args);
                }

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

        private static int GenerateHtml(string[] args)
        {
            var cmdLineArgs = new CommandLineArgs(args);
            cmdLineArgs.Next(); // пропуск ключевого слова

            var inputDir = cmdLineArgs.Next();
            if (inputDir == null)
            {
                ShowUsage();
                return 1;
            }

            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine("Input dir doesn't exist");
                return 2;
            }

            var outputDir = cmdLineArgs.Next();
            if (outputDir == null)
                outputDir = Directory.GetCurrentDirectory();

            inputDir = Path.GetFullPath(inputDir);

            var files = Directory.EnumerateFiles(inputDir, "*.md", SearchOption.AllDirectories)
                .Select(x=>new {FullPath = x, RelativePath = x.Substring(inputDir.Length+1)});

            Directory.CreateDirectory(outputDir);
            var mdGen = new MarkdownDeep.Markdown();
            mdGen.AutoHeadingIDs = true;
            mdGen.ExtraMode = true;

            foreach (var file in files)
            {
                Console.WriteLine("Processing {0}", file.RelativePath);
                using (var inputFile = new StreamReader(file.FullPath))
                {
                    var content = inputFile.ReadToEnd();
                    var html = mdGen.Transform(content);
                    var outputFile = Path.Combine(outputDir, file.RelativePath.Substring(0, file.RelativePath.Length-2)+"htm");
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));

                    using (var outStream = new StreamWriter(outputFile, false, Encoding.UTF8))
                    {
                        outStream.Write(html);
                    }
                }
            }
            Console.WriteLine("Done");
            return 0;
        }

        private static int GenerateXml(string[] args)
        {
            var cmdLineArgs = new CommandLineArgs(args);

            var outputFile = cmdLineArgs.Next();
            if (outputFile == null)
            {
                ShowUsage();
                return 1;
            }

            List<string> assemblies = new List<string>();

            while (true)
            {
                var arg = cmdLineArgs.Next();
                if (arg == null)
                    break;

                assemblies.Add(arg);
            }

            if (assemblies.Count == 0)
            {
                ShowUsage();
                return 1;
            }

            return CreateDocumentation(outputFile, assemblies);

        }

        private static int GenerateMarkdown(string[] args)
        {
            var cmdLineArgs = new CommandLineArgs(args);
            cmdLineArgs.Next(); // пропуск ключевого слова

            var xmlDoc = cmdLineArgs.Next();
            if(xmlDoc == null)
            {
                ShowUsage();
                return 1;
            }

            var outputDir = cmdLineArgs.Next();
            if (outputDir == null)
                outputDir = Directory.GetCurrentDirectory();

            return CreateDocumentation(xmlDoc, outputDir);

        }

        private static int CreateDocumentation(string outputFile, List<string> assemblies)
        {
            var documenter = new Documenter();
            var doc = documenter.CreateDocumentation(assemblies);

            doc.Save(outputFile);

            return 0;
        }

        private static int CreateDocumentation(string xmlDocPath, string pathOutput)
        {
            XDocument doc;
            using (var fs = new FileStream(xmlDocPath, FileMode.Open, FileAccess.Read))
            {
                doc = XDocument.Load(fs);
            }
            
            string docContent = doc.ToString();

            XslTransform xslt = new XslTransform();
            xslt.Load("markdown.xslt");
            XPathDocument xpathdocument = new XPathDocument(new StringReader(docContent));

            var stream = new MemoryStream();
            XmlTextWriter writer = new XmlTextWriter(stream, Encoding.UTF8);
                
            xslt.Transform(xpathdocument, null, writer, null);

            stream.Position = 0;
            XDocument xdoc = XDocument.Load(stream);
            writer.Close();

            if (!Directory.Exists(pathOutput))
                Directory.CreateDirectory(pathOutput);

            var contentStdlibPath = Path.Combine(pathOutput, "stdlib");
            Directory.CreateDirectory(contentStdlibPath);
            
            var tocBuilder = new StringBuilder();
            var knownNodes = new HashSet<string>();
            string baseUrl;
            using (var layout = new StreamReader("toc_layout.md"))
            {
                baseUrl = layout.ReadLine();
                var content = layout.ReadToEnd();
                tocBuilder.Append(content);
                tocBuilder.Replace("$base_url$", baseUrl);
                var matches = Regex.Matches(content, @"(?=\S)\[(.*)\]\S");
                foreach (Match match in matches)
                {
                    var uri = match.Groups[1].Value;
                    knownNodes.Add(uri);
                }
            }

            using (var tocWriter = new StreamWriter(Path.Combine(pathOutput, "stdlib.md")))
            {
                tocWriter.Write(tocBuilder.ToString());
                tocBuilder.Clear();

                foreach (var fileNode in xdoc.Root.Elements("document"))
                {
                    string name = fileNode.Attribute("href").Value.Replace(".md", "");
                    string link = name.Replace(" ", "%20");
                    
                    string path = Path.Combine(contentStdlibPath, fileNode.Attribute("href").Value);
                    using (var file = new FileStream(path, FileMode.Create))
                    using (var fileWriter = new StreamWriter(file))
                    {
                        fileWriter.Write(fileNode.Value);
                    }

                    if (!knownNodes.Contains(name))
                        tocWriter.WriteLine("* [{0}]({1}/{2})", baseUrl, name, link);

                }
            }

            return 0;
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("documenter.exe <output-file> <path-to-dll> [<path-to-dll>...]");
            Console.WriteLine("documenter.exe markdown <path-to-xml> <output-dir>");
            Console.WriteLine("documenter.exe html <markdown-dir> <output-dir>");
        }

    }
}
