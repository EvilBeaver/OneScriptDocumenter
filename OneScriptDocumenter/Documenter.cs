using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;

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
                bool isOscriptStd = false;

                foreach (var assembly in assemblies)
                {
                    var name = Path.GetFileNameWithoutExtension(assembly);
                    var xmlName = Path.Combine(Path.GetDirectoryName(assembly), name + ".xml");
                    if (!File.Exists(xmlName))
                    {
                        Console.WriteLine("Missing xml-doc: {0}", xmlName);
                        continue;
                    }
                    if (name == "ScriptEngine.HostedScript") {
                        isOscriptStd = true;
                    }

                    Console.WriteLine("Processing: {0}", name);

                    var docMaker = new AssemblyDocumenter(assembly, xmlName);
                    docMaker.CreateDocumentationJSON(textBlocks);
                }
                Newtonsoft.Json.Linq.JObject jsonObj = new Newtonsoft.Json.Linq.JObject();
                var list = JsonConvert.DeserializeObject<dynamic>("{" + textBlocks.TextGlobalContext.ToString());
                jsonObj.Add("structureMenu", Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                Newtonsoft.Json.Linq.JObject structureMenu = jsonObj["structureMenu"] as Newtonsoft.Json.Linq.JObject;
                if (isOscriptStd)
                {
                    using (var layout = new StreamReader(ExtFiles.Get("structureMenu.json")))
                    {
                        var content = layout.ReadToEnd();
                        var menu = JsonConvert.DeserializeObject<dynamic>(content);

                        foreach (Newtonsoft.Json.Linq.JToken curType in menu)
                        {
                            if (((Newtonsoft.Json.Linq.JProperty)curType).Name == "global")
                            {
                                structureMenu.Add(((Newtonsoft.Json.Linq.JProperty)curType).Name, Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                            } else {
                                structureMenu.Add(((Newtonsoft.Json.Linq.JProperty)curType).Name, curType.First);
                            }
                        }
                        foreach (Newtonsoft.Json.Linq.JToken curType in list)
                        {
                            if (((Newtonsoft.Json.Linq.JObject)structureMenu["global"]).GetValue(((Newtonsoft.Json.Linq.JProperty)curType).Name) == null)
                            {
                                Newtonsoft.Json.Linq.JObject elemStructure = jsonObj["structureMenu"]["global"] as Newtonsoft.Json.Linq.JObject;
                                elemStructure.Add(((Newtonsoft.Json.Linq.JProperty)curType).Name, Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                            }
                            if (((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("properties") != null)
                            {
                                foreach (Newtonsoft.Json.Linq.JToken elem in ((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("properties"))
                                {
                                    Newtonsoft.Json.Linq.JObject elemStructure = jsonObj["structureMenu"]["global"][((Newtonsoft.Json.Linq.JProperty)curType).Name] as Newtonsoft.Json.Linq.JObject;
                                    elemStructure.Add(((Newtonsoft.Json.Linq.JProperty)elem).Name, "");
                                }
                            }
                            if (((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("methods") != null)
                            {
                                foreach (Newtonsoft.Json.Linq.JToken elem in ((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("methods"))
                                {
                                    Newtonsoft.Json.Linq.JObject elemStructure = jsonObj["structureMenu"]["global"][((Newtonsoft.Json.Linq.JProperty)curType).Name] as Newtonsoft.Json.Linq.JObject;
                                    elemStructure.Add(((Newtonsoft.Json.Linq.JProperty)elem).Name, "");
                                }
                            }
                        }

                    }
                } else {
                    structureMenu.Add("classes", JsonConvert.DeserializeObject<dynamic>("{\n\"Прочее\": \"\"\n }"));
                }
                foreach (Newtonsoft.Json.Linq.JToken curType in list)
                {
                    if (((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("properties") != null)
                    {
                        foreach (Newtonsoft.Json.Linq.JToken prop in ((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("properties"))
                        {
                            if (jsonObj["globalvariable"] == null)
                            {
                                jsonObj.Add("globalvariable", Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                            }
                            Newtonsoft.Json.Linq.JObject globalvariable = jsonObj["globalvariable"] as Newtonsoft.Json.Linq.JObject;
                            globalvariable.Add(((Newtonsoft.Json.Linq.JProperty)prop).Name, prop.First);
                        }
                    }
                    if (((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("methods") != null)
                    {
                        foreach (Newtonsoft.Json.Linq.JToken meth in ((Newtonsoft.Json.Linq.JProperty)curType).Value.SelectToken("methods"))
                        {
                            if (jsonObj["globalfunctions"] == null)
                            {
                                jsonObj.Add("globalfunctions", Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                            }
                            Newtonsoft.Json.Linq.JObject globalfunctions = jsonObj["globalfunctions"] as Newtonsoft.Json.Linq.JObject;
                            globalfunctions.Add(((Newtonsoft.Json.Linq.JProperty)meth).Name, meth.First);
                        }
                    }
                }
                jsonObj.Add("classes", Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                Newtonsoft.Json.Linq.JObject classes = jsonObj["classes"] as Newtonsoft.Json.Linq.JObject;
                var classesList = JsonConvert.DeserializeObject<dynamic>("{" + textBlocks.TextContextDescription.ToString() + "}");
                foreach (Newtonsoft.Json.Linq.JToken curType in classesList)
                {
                    classes.Add(((Newtonsoft.Json.Linq.JProperty)curType).Name, curType.First);
                }
                jsonObj.Add("systemEnum", Newtonsoft.Json.Linq.JObject.Parse(@"{ }"));
                Newtonsoft.Json.Linq.JObject systemEnum = jsonObj["systemEnum"] as Newtonsoft.Json.Linq.JObject;
                var systemEnumList = JsonConvert.DeserializeObject<dynamic>("{" + textBlocks.TextEnumsDescription.ToString() + "}");
                foreach (Newtonsoft.Json.Linq.JToken curType in systemEnumList)
                {
                    systemEnum.Add(((Newtonsoft.Json.Linq.JProperty)curType).Name, curType.First);
                }
                sbJSON.Write(JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                Console.WriteLine("Done");
                return "";
            }
        }
    }
}
