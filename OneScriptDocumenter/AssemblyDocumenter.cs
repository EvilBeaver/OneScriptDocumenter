using OneScriptDocumenter.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OneScriptDocumenter
{
    class AssemblyDocumenter
    {
        LoadedAssembly _library;
        XDocument _xmlDoc;
        Dictionary<string, XElement> _memberDocumentation = new Dictionary<string, XElement>();

        public AssemblyDocumenter(string library, string xmldoc)
        {
            using(var reader = new StreamReader(xmldoc))
            {
                _xmlDoc = XDocument.Load(reader);
            }

            var dir = Path.GetDirectoryName(library);

            var loader = new AssemblyLoader(dir);

            _library = loader.Load(Path.GetFileName(library));

        }

        public XDocument CreateDocumentation()
        {
            var asmElement = _xmlDoc.Root.Element("assembly");
            if (asmElement == null)
                throw new ApplicationException("Wrong XML doc format");

            var libName = _library.Name;
            var fileLibName = asmElement.Element("name").Value;
            if (String.Compare(libName, fileLibName, true) != 0)
                throw new ApplicationException(String.Format("Mismatch assembly names. Expected {0}, found in XML {1}", libName, fileLibName));

            var members = _xmlDoc.Element("doc").Element("members").Elements();
            _memberDocumentation.Clear();
            foreach (var item in members)
            {
                string key = item.Attribute("name").Value;
                _memberDocumentation[key] = item;
            }

            XDocument output = BeginOutputDoc();

            var globalContexts = _library.GetMarkedTypes(ScriptMemberType.GlobalContext);
            foreach (var globalContext in globalContexts)
            {
                AddGlobalContextDescription(globalContext, output.Root);
            }

            var contextTypes = _library.GetMarkedTypes(ScriptMemberType.Class);
            foreach (var classType in contextTypes)
            {
                AddContextDescription(classType, output.Root);
            }

            return output;
        }

        private void AddGlobalContextDescription(Type globalContext, XElement xElement)
        {
            var childElement = new XElement("global-context");

            childElement.Add(new XAttribute("clr-name", globalContext.FullName));

            var attrib = _library.GetMarkup(globalContext, ScriptMemberType.GlobalContext);
            if (attrib == null)
                return;

            string categoryName = null;

            try
            {
                if (attrib.NamedArguments != null)
                {
                    var categoryMember = attrib.NamedArguments.First(x => x.MemberName == "Category");
                    categoryName = (string) categoryMember.TypedValue.Value;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }
            
            if(categoryName != null)
                childElement.Add(new XElement("category", categoryName));
            
            AppendXmlDocs(childElement, "T:" + globalContext.FullName);

            AddProperties(globalContext, childElement);
            AddMethods(globalContext, childElement);

            xElement.Add(childElement);
        }

        private void AddContextDescription(Type classType, XElement xElement)
        {
            var childElement = new XElement("context");

            childElement.Add(new XAttribute("clr-name", classType.FullName));
            var attrib = _library.GetMarkup(classType, ScriptMemberType.Class);
            
            childElement.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
            childElement.Add(new XElement("alias", (string)attrib.ConstructorArguments[1].Value));

            AppendXmlDocs(childElement, "T:" + classType.FullName);

            AddProperties(classType, childElement);
            AddMethods(classType, childElement);
            AddConstructors(classType, childElement);

            xElement.Add(childElement);
        }

        private void AddMethods(Type classType, XElement childElement)
        {
            var collection = new XElement("methods");
            var methodArray = classType.GetMethods();
            foreach (var meth in methodArray)
            {
                var attrib = _library.GetMarkup(meth, ScriptMemberType.Method);
                if(attrib != null)
                {
                    var fullName = classType.FullName + "." + MethodId(meth);
                    var element = new XElement("method");
                    element.Add(new XAttribute("clr-name", fullName));
                    element.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
                    element.Add(new XElement("alias", (string)attrib.ConstructorArguments[1].Value));

                    AppendXmlDocs(element, "M:" + fullName);

                    collection.Add(element);
                }
            }

            childElement.Add(collection);
        }

        private string MethodId(MethodInfo meth)
        {
            var sb = new StringBuilder();
            sb.Append(meth.Name);
            var methParams = meth.GetParameters();
            if (methParams.Length > 0)
            {
                sb.Append('(');
                var paramInfos = methParams.Select(x => x.ParameterType.FullName);
                sb.Append(string.Join(",", paramInfos));
                sb.Append(')');
            }
            return sb.ToString();
        }

        private void AddProperties(Type classType, XElement childElement)
        {
            var propElementCollection = new XElement("properties");

            var propArray = classType.GetProperties();
            foreach (var prop in propArray)
            {
                var attrib = _library.GetMarkup(prop, ScriptMemberType.Property);
                if(attrib != null)
                {
                    var propElement = new XElement("property");
                    propElement.Add(new XAttribute("clr-name", classType.FullName + "." + prop.Name));
                    propElement.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
                    propElement.Add(new XElement("alias", (string)attrib.ConstructorArguments[1].Value));
                    
                    bool? canRead = null;
                    bool? canWrite = null;

                    if (attrib.NamedArguments != null)
                    {
                        foreach (var attributeNamedArgument in attrib.NamedArguments)
                        {
                            if (attributeNamedArgument.MemberName == "CanRead")
                            {
                                canRead = (bool) attributeNamedArgument.TypedValue.Value;
                            }

                            if (attributeNamedArgument.MemberName == "CanWrite")
                            {
                                canWrite = (bool) attributeNamedArgument.TypedValue.Value;
                            }
                        }
                    }

                    if (canRead == null)
                        canRead = prop.GetMethod != null;

                    if (canWrite == null)
                        canWrite = prop.SetMethod != null;

                    propElement.Add(new XElement("readable", canRead));
                    propElement.Add(new XElement("writeable", canWrite));

                    AppendXmlDocs(propElement, "P:" + classType.FullName + "." + prop.Name);

                    propElementCollection.Add(propElement);
                }
            }

            childElement.Add(propElementCollection);
        }

        private void AddConstructors(Type classType, XElement childElement)
        {
            var d = classType.FullName;
            
            int itemsCount = 0;
            var collection = new XElement("constructors");
            var methodArray = classType.GetMethods(BindingFlags.Static | BindingFlags.Public);
            
            foreach (var meth in methodArray)
            {
                var attrib = _library.GetMarkup(meth, ScriptMemberType.Constructor);
                if (attrib != null)
                {
                    var fullName = classType.FullName + "." + MethodId(meth);
                    var element = new XElement("ctor");
                    element.Add(new XAttribute("clr-name", fullName));

                    var namedArgsName = attrib.NamedArguments.Where(x => x.MemberName == "Name").FirstOrDefault();
                    if(namedArgsName.MemberInfo == null)
                    {
                        element.Add(new XElement("name", "По умолчанию"));
                    }
                    else
                    {
                        var ctorName = (string)namedArgsName.TypedValue.Value;
                        if (ctorName == "")
                            continue;
                        element.Add(new XElement("name", ctorName));
                    }

                    AppendXmlDocs(element, "M:" + fullName);
                    collection.Add(element);
                    itemsCount++;
                }
            }
            if(itemsCount > 0)
                childElement.Add(collection);
        }

        private void AppendXmlDocs(XElement element, string memberName)
        {
            XElement xDoc;
            if(_memberDocumentation.TryGetValue(memberName, out xDoc))
            {

                var summary = xDoc.Element("summary");
                if(summary != null)
                {
                    var descr = new XElement("description");
                    ProcessChildNodes(descr, summary);
                    element.Add(descr);
                }

                // parameters
                var paramsList = xDoc.Elements("param");
                foreach (var paramItem in paramsList)
                {
                    var param = new XElement("param");
                    ProcessChildNodes(param, paramItem);
                    element.Add(param);
                }

                // returns
                var returnNode = xDoc.Element("returns");
                if(returnNode != null)
                {
                    var node = new XElement("returns");
                    ProcessChildNodes(node, returnNode);
                    element.Add(node);
                }

                // other
                var elems = xDoc.Elements();
                foreach (var item in elems)
                {
                    if (item.Name == "summary" || item.Name == "param" || item.Name == "returns") 
                        continue;

                    var node = new XElement(item.Name);
                    ProcessChildNodes(node, summary);
                    element.Add(node);
                }
            }
        }

        private void ProcessChildNodes(XElement dest, XElement source)
        {
            var nodes = source.Nodes();
            StringBuilder textContent = new StringBuilder();
            foreach (var node in nodes)
            {
                if(node.NodeType == System.Xml.XmlNodeType.Text)
                {
                    textContent.Append(CollapseWhiteSpace(node.ToString()));
                }
                else if(node.NodeType == System.Xml.XmlNodeType.Element)
                {
                    var newElem = new XElement(((XElement)node).Name);
                    ProcessChildNodes(newElem, (XElement)node);
                    dest.Add(newElem);
                }
            }

            foreach (var attr in source.Attributes())
            {
                dest.Add(attr);
            }

            if(textContent.Length > 0)
                dest.Add(textContent.ToString());
        }

        private string CollapseWhiteSpace(string p)
        {
            if (p == String.Empty)
                return "";

            StringBuilder sb = new StringBuilder();
            using(var sr = new StringReader(p))
            {
                string line = null;
                do
                {
                    line = sr.ReadLine();
                    if(!String.IsNullOrWhiteSpace(line))
                        sb.AppendLine(line.Trim());
                    else if(line != null && line.Length > 0) 
                        sb.AppendLine();

                } while (line != null);
            }

            return sb.ToString();
        }

        private XDocument BeginOutputDoc()
        {
            XDocument result = new XDocument();
            result.Add(new XElement("contexts"));

            return result;
        }
    }
}
