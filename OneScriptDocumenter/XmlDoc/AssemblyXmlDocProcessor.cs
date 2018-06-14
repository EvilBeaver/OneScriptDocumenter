using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OneScriptDocumenter.XmlDoc
{
    class AssemblyXmlDocProcessor
    {
        readonly LoadedAssembly _library;
        readonly XDocument _xmlDoc;
        readonly Dictionary<string, XElement> _memberDocumentation = new Dictionary<string, XElement>();

        public AssemblyXmlDocProcessor(string library, string xmldoc)
        {
            using (var reader = new StreamReader(xmldoc))
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
                throw new ArgumentException("Wrong XML doc format");

            var libName = _library.Name;
            var fileLibName = asmElement.Element("name").Value;
            if (String.Compare(libName, fileLibName, true, System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new ArgumentNullException(String.Format("Mismatch assembly names. Expected {0}, found in XML {1}", libName, fileLibName));

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

            var systemEnums = _library.GetMarkedTypes(ScriptMemberType.SystemEnum);
            foreach (var systemEnum in systemEnums)
            {
                AddEnumsDescription(systemEnum, ScriptMemberType.SystemEnum);
            }
            var enums = _library.GetMarkedTypes(ScriptMemberType.EnumerationType);
            foreach (var sysEnum in enums)
            {
                AddEnumsDescription(sysEnum, ScriptMemberType.EnumerationType);
            }

            return output;
        }

        private void AddEnumsDescription(Type sysEnum, ScriptMemberType enumerationType)
        {
            var attrib = _library.GetMarkup(sysEnum, enumerationType);

            string name, alias;
            GetNameAndAlias(attrib, sysEnum.Name, out name, out alias);

            var childElement = new XElement(name);
            childElement.Add(new XElement("name", name));
            childElement.Add(new XElement("name_en", alias));

            AppendXmlDocs(childElement, "T:" + sysEnum.FullName);

            AddValues(sysEnum, childElement);
        }

        private void AddGlobalContextDescription(Type globalContext, XContainer xElement)
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
                    categoryName = (string)categoryMember.TypedValue.Value;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (categoryName != null)
                childElement.Add(new XElement("category", categoryName));

            AppendXmlDocs(childElement, "T:" + globalContext.FullName);

            AddProperties(globalContext, childElement);
            AddMethods(globalContext, childElement);

            xElement.Add(childElement);
        }

        private void AddContextDescription(Type classType, XContainer xElement)
        {
            var childElement = new XElement("context");

            childElement.Add(new XAttribute("clr-name", classType.FullName));
            var attrib = _library.GetMarkup(classType, ScriptMemberType.Class);

            string name, alias;
            GetNameAndAlias(attrib, classType.Name, out name, out alias);

            childElement.Add(new XElement("name", name));
            childElement.Add(new XElement("alias", alias));

            AppendXmlDocs(childElement, "T:" + classType.FullName);

            AddProperties(classType, childElement);
            AddMethods(classType, childElement);
            AddConstructors(classType, childElement);

            xElement.Add(childElement);
        }

        private void AddMethods(Type classType, XContainer childElement)
        {
            var collection = new XElement("methods");
            var methodArray = classType.GetMethods();
            foreach (var meth in methodArray)
            {
                var attrib = _library.GetMarkup(meth, ScriptMemberType.Method);
                if (attrib != null)
                {
                    var element = WriteMethod(meth, attrib);
                    collection.Add(element);
                }
            }

            childElement.Add(collection);
        }

        private XElement WriteMethod(MethodInfo meth, CustomAttributeData attrib)
        {
            string name, alias;
            GetNameAndAlias(attrib, meth.Name, out name, out alias);
            var element = new XElement("method");

            var sb = new StringBuilder();
            sb.Append(meth.Name);
            var paramsNode = new XElement("parameters");
            var methParams = meth.GetParameters();
            if (methParams.Length > 0)
            {
                sb.Append('(');
                string[] paramTypeNames = new string[methParams.Length];
                for (int i = 0; i < methParams.Length; i++)
                {
                    var info = methParams[i];
                    string paramType = "";
                    if (info.ParameterType.GenericTypeArguments.Length > 0)
                    {
                        var genericBuilder = BuildStringGenericTypes(info.ParameterType);

                        paramType = genericBuilder.ToString();
                    }
                    else
                    {
                        paramType = info.ParameterType.FullName;
                    }

                    paramTypeNames[i] = paramType;
                    var paramNode = new XElement("param",
                        new XAttribute("name", info.Name),
                        new XAttribute("clr-type", paramType),
                        new XAttribute("optional", info.IsOptional));

                    paramsNode.Add(paramNode);

                }
                sb.Append(string.Join(",", paramTypeNames));
                sb.Append(')');
            }

            var fullName = meth.DeclaringType.FullName + "." + sb.ToString();
            element.Add(new XAttribute("clr-name", fullName));
            element.Add(new XElement("name", name));
            element.Add(new XElement("alias", alias));
            element.Add(paramsNode);
            AppendXmlDocs(element, "M:" + fullName);

            return element;
        }

        private void AddValues(Type classType, XContainer childElement)
        {
            var propElementCollection = new XElement("values");

            var propArray = classType.GetProperties();
            foreach (var prop in propArray)
            {
                var attrib = _library.GetMarkup(prop, ScriptMemberType.EnumerationValue);
                if (attrib != null)
                {
                    string name, alias;
                    GetNameAndAlias(attrib, prop.Name, out name, out alias);

                    var propElement = new XElement(name);
                    propElement.Add(new XElement("name", name));
                    propElement.Add(new XElement("name_en", alias));

                    AppendXmlDocs(propElement, "P:" + classType.FullName + "." + prop.Name);
                    propElementCollection.Add(propElement);
                }
            }
            var fieldsArray = classType.GetFields();
            foreach (var field in fieldsArray)
            {
                var attrib = _library.GetMarkup(field, ScriptMemberType.EnumItem);
                if (attrib != null)
                {
                    string name, alias;
                    GetNameAndAlias(attrib, field.Name, out name, out alias);

                    var propElement = new XElement(name);
                    propElement.Add(new XElement("name", name));
                    propElement.Add(new XElement("name_en", alias));

                    AppendXmlDocs(propElement, "P:" + classType.FullName + "." + field.Name);
                    propElementCollection.Add(propElement);
                }
            }

            if (!propElementCollection.IsEmpty)
                childElement.Add(propElementCollection);
        }

        private void AddProperties(Type classType, XContainer childElement)
        {
            var propElementCollection = new XElement("properties");

            var propArray = classType.GetProperties();
            foreach (var prop in propArray)
            {
                var attrib = _library.GetMarkup(prop, ScriptMemberType.Property);
                if (attrib != null)
                {
                    XElement propElement;
                    propElement = fillPropElement(prop, attrib, classType);
                    propElementCollection.Add(propElement);
                }
            }
            if (!propElementCollection.IsEmpty)
                childElement.Add(propElementCollection);
        }

        private static void GetNameAndAlias(CustomAttributeData attrib, string clrName, out string name, out string alias)
        {
            name = (string)attrib.ConstructorArguments[0].Value;
            alias = (string)attrib.ConstructorArguments[1].Value;
            if (string.IsNullOrEmpty(alias))
            {
                alias = clrName;
            }
        }

        private void AppendXmlDocs(XContainer element, string memberName)
        {
            XElement xDoc;
            if (_memberDocumentation.TryGetValue(memberName, out xDoc))
            {
                var summary = xDoc.Element("summary");
                if (summary != null)
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
                if (returnNode != null)
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
                    ProcessChildNodes(node, item);
                    element.Add(node);
                }
            }
        }

        private void AddConstructors(Type classType, XContainer childElement)
        {

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

                    var namedArgsName = attrib.NamedArguments.FirstOrDefault(x => x.MemberName == "Name");
                    if (namedArgsName.MemberInfo == null)
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
            if (itemsCount > 0)
                childElement.Add(collection);
        }

        private string MethodId(MethodBase meth)
        {
            var sb = new StringBuilder();
            sb.Append(meth.Name);
            var methParams = meth.GetParameters();
            if (methParams.Length > 0)
            {
                sb.Append('(');
                var paramInfos = methParams.Select(x => x.ParameterType).ToArray();
                string[] paramTypeNames = new string[paramInfos.Length];

                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var info = paramInfos[i];
                    if (info.GenericTypeArguments.Length > 0)
                    {
                        var genericBuilder = BuildStringGenericTypes(info);

                        paramTypeNames[i] = genericBuilder.ToString();
                    }
                    else
                    {
                        paramTypeNames[i] = info.FullName;
                    }
                }
                sb.Append(string.Join(",", paramTypeNames));
                sb.Append(')');
            }
            return sb.ToString();
        }


        private StringBuilder BuildStringGenericTypes(Type info)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(info.FullName, @"([\w.]+)`\d|(\[([\w0-9.=]+)(?:,\s(?:[\w0-9.= ]+))*\]),?");

            var genericBuilder = new StringBuilder();

            if (matches.Count == 1)
            {
                return genericBuilder;
            }

            genericBuilder.Append(matches[0].Groups[1].ToString());
            genericBuilder.Append('{');
            bool fst = true;
            foreach (var capture in matches[1].Groups[3].Captures)
            {
                if (!fst)
                    genericBuilder.Append(", ");

                genericBuilder.Append(capture.ToString());
                fst = false;
            }
            genericBuilder.Append('}');

            return genericBuilder;

        }


        private void ProcessChildNodes(XContainer dest, XElement source)
        {
            var nodes = source.Nodes();
            StringBuilder textContent = new StringBuilder();
            foreach (var node in nodes)
            {
                if (node.NodeType == System.Xml.XmlNodeType.Text)
                {
                    dest.Add(new XText((XText)node));
                }
                else if (node.NodeType == System.Xml.XmlNodeType.Element)
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

            if (textContent.Length > 0)
                dest.Add(textContent.ToString());
        }

        private XElement fillPropElement(System.Reflection.PropertyInfo prop, System.Reflection.CustomAttributeData attrib, Type classType)
        {
            var propElement = new XElement("property");
            string name, alias;
            GetNameAndAlias(attrib, prop.Name, out name, out alias);
            propElement.Add(new XAttribute("clr-name", classType.FullName + "." + prop.Name));
            propElement.Add(new XElement("name", name));
            propElement.Add(new XElement("alias", alias));

            var access = findAccess(attrib, prop);

            propElement.Add(new XElement("readable", access["canRead"]));
            propElement.Add(new XElement("writeable", access["canWrite"]));

            AppendXmlDocs(propElement, "P:" + classType.FullName + "." + prop.Name);
            return propElement;

        }

        private Dictionary<string, bool?> findAccess(System.Reflection.CustomAttributeData attrib, System.Reflection.PropertyInfo prop)
        {
            bool? canRead = null;
            bool? canWrite = null;

            if (attrib.NamedArguments != null)
            {
                foreach (var attributeNamedArgument in attrib.NamedArguments)
                {
                    if (attributeNamedArgument.MemberName == "CanRead")
                    {
                        canRead = (bool)attributeNamedArgument.TypedValue.Value;
                    }

                    if (attributeNamedArgument.MemberName == "CanWrite")
                    {
                        canWrite = (bool)attributeNamedArgument.TypedValue.Value;
                    }
                }
            }
            if (canRead == null)
                canRead = prop.GetMethod != null;

            if (canWrite == null)
                canWrite = prop.SetMethod != null;

            var result = new Dictionary<string, bool?>();
            result.Add("canRead", canRead);
            result.Add("canWrite", canWrite);

            return result;

        }

        private XDocument BeginOutputDoc()
        {
            XDocument result = new XDocument();
            result.Add(new XElement("contexts"));

            return result;
        }
    }
}
