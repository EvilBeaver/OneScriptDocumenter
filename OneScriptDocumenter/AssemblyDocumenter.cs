using OneScriptDocumenter.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OneScriptDocumenter
{
    class AssemblyDocumenter
    {
        LoadedAssembly _library;
        XDocument _xmlDoc;
        Dictionary<string, XElement> _memberDocumentation = new Dictionary<string, XElement>();

        TypesDictinary _typesDict;

        public AssemblyDocumenter()
        { }

        public AssemblyDocumenter(string library, string xmldoc)
        {
            using(var reader = new StreamReader(xmldoc))
            {
                _xmlDoc = XDocument.Load(reader);
            }

            var dir = Path.GetDirectoryName(library);

            var loader = new AssemblyLoader(dir);

            _library = loader.Load(Path.GetFileName(library));

            // add to dictinary

            _typesDict = new TypesDictinary();


            // Пока топорное заполнение...
            var globalContexts = _library.GetMarkedTypes(ScriptMemberType.GlobalContext);
            foreach (var globalContext in globalContexts)
            {
                var attrib = _library.GetMarkup(globalContext, ScriptMemberType.GlobalContext);

                if (attrib == null)
                {
                    continue;
                }

                TypeInfo curTypeInfo = new TypeInfo();
                curTypeInfo.fullName = globalContext.FullName;
                curTypeInfo.ShortName = globalContext.Name;
                if (attrib.ConstructorArguments.Count > 0)
                {
                    curTypeInfo.nameEng = (string)attrib.ConstructorArguments[1].Value;
                    curTypeInfo.nameRus = (string)attrib.ConstructorArguments[0].Value;
                }
                _typesDict.add(curTypeInfo);
            }

            var contextTypes = _library.GetMarkedTypes(ScriptMemberType.Class);
            foreach (var classType in contextTypes)
            {
                var attrib = _library.GetMarkup(classType, ScriptMemberType.Class);

                if (attrib == null)
                {
                    continue;
                }

                TypeInfo curTypeInfo = new TypeInfo();
                curTypeInfo.fullName = classType.FullName;
                curTypeInfo.ShortName = classType.Name;
                if (attrib.ConstructorArguments.Count > 0)
                {
                    curTypeInfo.nameEng = (string)attrib.ConstructorArguments[1].Value;
                    curTypeInfo.nameRus = (string)attrib.ConstructorArguments[0].Value;
                }
                _typesDict.add(curTypeInfo);
            }

            _typesDict.save();

        }

        public string SetRusNames(string data, bool link = true)
        {

            string linkStr = (link) ? "link::" : "";

            string str = data;

            str = str.Replace("System.String", "Строка");
            str = str.Replace("System.DateTime", "Дата");
            str = str.Replace("System.Int32", "Число");
            str = str.Replace("System.Int64", "Число");
            str = str.Replace("System.Boolean", "Булево");
            str = str.Replace("ScriptEngine.Machine.IValue", "Произвольный");
            str = str.Replace("ScriptEngine.Machine.IRuntimeContextInstance", "ИнформацияОСценарии");

            foreach (TypeInfo curItm in _typesDict.types)
            {
                Regex regex = new Regex(@"(\b)(" + curItm.fullName + @")(\b)",RegexOptions.IgnoreCase);
                str = regex.Replace(str, linkStr + curItm.nameRus);
            }

            return str;
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

        public void CreateDocumentationJSON(StreamWriter sbJSON)
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
                AddGlobalContextDescriptionJSON(globalContext, sbJSON);
            }

            var contextTypes = _library.GetMarkedTypes(ScriptMemberType.Class);
            foreach (var classType in contextTypes)
            {
                AddContextDescriptionJSON(classType, sbJSON);
            }

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

        private void AddGlobalContextDescriptionJSON(Type globalContext, StreamWriter sbJSON)
        {
            //var childElement = new XElement("global-context");

            //childElement.Add(new XAttribute("clr-name", globalContext.FullName));

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
            var childElement = new XElement(System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(categoryName).Replace(" ", string.Empty));
            if (categoryName != null)
                childElement.Add(new XElement("category", categoryName));

            AppendXmlDocsJSON(childElement, "T:" + globalContext.FullName);

            AddPropertiesJSON(globalContext, childElement);
            AddMethodsJSON(globalContext, childElement);

            sbJSON.WriteLine(JSon.XmlToJSON(childElement.ToString()) + ",");
        }

        private void AddContextDescriptionJSON(Type classType, StreamWriter sbJSON)
        {
            //var childElement = new XElement("context");

            //childElement.Add(new XAttribute("clr-name", classType.FullName));
            var attrib = _library.GetMarkup(classType, ScriptMemberType.Class);
            var childElement = new XElement((string)attrib.ConstructorArguments[0].Value);


            childElement.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
            childElement.Add(new XElement("name_en", (string)attrib.ConstructorArguments[1].Value));

            AppendXmlDocsJSON(childElement, "T:" + classType.FullName);

            AddPropertiesJSON(classType, childElement);
            AddMethodsJSON(classType, childElement);
            AddConstructorsJSON(classType, childElement);

            sbJSON.WriteLine(JSon.XmlToJSON(childElement.ToString()) + ",");
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

        private void AddMethodsJSON(Type classType, XElement childElement)
        {
            var collection = new XElement("methods");
            var methodArray = classType.GetMethods();
            foreach (var meth in methodArray)
            {
                var attrib = _library.GetMarkup(meth, ScriptMemberType.Method);
                if (attrib != null)
                {
                    var fullName = classType.FullName + "." + MethodId(meth);
                    var element = new XElement((string)attrib.ConstructorArguments[0].Value);
                    //element.Add(new XAttribute("clr-name", fullName));
                    element.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
                    element.Add(new XElement("name_en", (string)attrib.ConstructorArguments[1].Value));
                    var returns = "";
                    if (meth.ReturnType.FullName != "System.Void")
                    {
                        returns = SetRusNames(meth.ReturnType.FullName, false);
                        if (returns != "") returns = ": " + returns;
                    }
                    element.Add(new XElement("signature", "(" + MethodIdJSON(meth, false) + ") " + returns));
                    AppendXmlDocsJSON(element, "M:" + fullName);

                    collection.Add(element);
                }
            }

            if (!collection.IsEmpty)
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
                var paramInfos = methParams.Select(x => x.ParameterType).ToArray();
                string[] paramTypeNames = new string[paramInfos.Length];

                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var info = paramInfos[i];
                    if (info.GenericTypeArguments.Length > 0)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(info.FullName, @"([\w.]+)`\d|(\[([\w0-9.=]+)(?:,\s(?:[\w0-9.= ]+))*\]),?");

                        var genericBuilder = new StringBuilder();
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

        private string MethodIdJSON(MethodInfo meth, bool addLink = true)
        {
            var sb = new StringBuilder();
            //sb.Append(meth.Name);
            var methParams = meth.GetParameters();
            if (methParams.Length > 0)
            {
                //sb.Append('(');
                var paramInfos = methParams.Select(x => x.ParameterType).ToArray();
                string[] paramTypeNames = new string[paramInfos.Length];

                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var info = paramInfos[i];
                    var MethcodArg = "";
                    if (info.GenericTypeArguments.Length > 0)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(info.FullName, @"([\w.]+)`\d|(\[([\w0-9.=]+)(?:,\s(?:[\w0-9.= ]+))*\]),?");

                        var genericBuilder = new StringBuilder();
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
                        MethcodArg = genericBuilder.ToString();
                    }
                    else
                    {
                        MethcodArg = info.FullName;
                    }

                    MethcodArg = SetRusNames(MethcodArg, addLink);
                    var Optional = (methParams[i].IsOptional) ? "?" : "";
                    paramTypeNames[i] = methParams[i].Name + Optional + ": " + MethcodArg;
                }
                sb.Append(string.Join(", ", paramTypeNames));
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

        private void AddPropertiesJSON(Type classType, XElement childElement)
        {
            var propElementCollection = new XElement("properties");

            var propArray = classType.GetProperties();
            foreach (var prop in propArray)
            {
                var attrib = _library.GetMarkup(prop, ScriptMemberType.Property);
                if (attrib != null)
                {
                    var propElement = new XElement((string)attrib.ConstructorArguments[0].Value);
                    propElement.Add(new XElement("name", (string)attrib.ConstructorArguments[0].Value));
                    propElement.Add(new XElement("name_en", (string)attrib.ConstructorArguments[1].Value));

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

                    //propElement.Add(new XElement("readable", canRead));
                    //propElement.Add(new XElement("writeable", canWrite));
                    AppendXmlDocsJSON(propElement, "P:" + classType.FullName + "." + prop.Name);
                    propElement.Add(new XElement("access", ((bool)canRead && (bool)canWrite) ? "Чтение/Запись" : ((bool)canRead && !(bool)canWrite) ? "Чтение" : "Запись"));
                    propElementCollection.Add(propElement);
                }
            }
            if (!propElementCollection.IsEmpty)
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

        private void AddConstructorsJSON(Type classType, XElement childElement)
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
                    //element.Add(new XAttribute("clr-name", fullName));
                    var ctorName = "По умолчанию";
                    var namedArgsName = attrib.NamedArguments.Where(x => x.MemberName == "Name").FirstOrDefault();
                    if (namedArgsName.MemberInfo != null)
                    {
                        ctorName = (string)namedArgsName.TypedValue.Value;
                        //element = new XElement(ctorName);
                        if (ctorName == "")
                            continue;
                    }
                    //TextInfo textInfo = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(ctorName);
                    var element = new XElement(System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(ctorName).Replace(" ", string.Empty));
                    element.Add(new XElement("name", ctorName));
                    element.Add(new XElement("signature", "(" + MethodIdJSON(meth, false) + ")"));
                    AppendXmlDocs(element, "M:" + fullName);
                    collection.Add(element);
                    itemsCount++;
                }
            }
            if (itemsCount > 0)
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
                    ProcessChildNodes(node, item);
                    element.Add(node);
                }
            }
        }

        private void AppendXmlDocsJSON(XElement element, string memberName)
        {
            XElement xDoc;
            if (_memberDocumentation.TryGetValue(memberName, out xDoc))
            {

                var remarks = xDoc.Element("remarks");
                string remarksStr = "";
                if (remarks != null)
                {
                    var descr = new XElement("remarks");
                    ProcessChildNodes(descr, remarks);
                    remarksStr = " " + descr.Value.Replace("\r\n", " ");
                }

                var summary = xDoc.Element("summary");
                if (summary != null)
                {
                    var descr = new XElement("description");
                    ProcessChildNodes(descr, summary);
                    descr.Value = descr.Value.Replace("\r\n", " ") + remarksStr;
                    element.Add(descr);
                }

                // returns
                var returnNode = xDoc.Element("returns");
                if (returnNode != null)
                {
                    var node = new XElement("returns");
                    ProcessChildNodes(node, returnNode);
                    if (!node.IsEmpty)
                        element.Add(node);
                }

                // other
                var elems = xDoc.Elements();
                foreach (var item in elems)
                {
                    if (item.Name == "summary" || item.Name == "param" || item.Name == "returns" || item.Name == "remarks")
                        continue;

                    var node = new XElement(item.Name);
                    ProcessChildNodes(node, item);
                    element.Add(node);
                }

                // parameters
                var paramsList = xDoc.Elements("param");
                if (paramsList.Count() > 0)
                {
                    var param = new XElement("params");
                    foreach (var paramItem in paramsList)
                    {
                        ProcessChildNodesJSON(param, paramItem);
                    }
                    if (!param.IsEmpty)
                        element.Add(param);

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

        private void ProcessChildNodesJSON(XElement dest, XElement source)
        {
            var nodes = source.Nodes();
            StringBuilder textContent = new StringBuilder();
            foreach (var node in nodes)
            {
                if (node.NodeType == System.Xml.XmlNodeType.Text)
                {
                    textContent.Append(node.ToString());
                }
                else if (node.NodeType == System.Xml.XmlNodeType.Element)
                {
                    var newElem = new XElement(((XElement)node).Name);
                    ProcessChildNodes(newElem, (XElement)node);
                    dest.Add(newElem);
                }
            }

            //foreach (var attr in source.Attributes())
            //{
            //    dest.Add(attr);
            //}

            if (textContent.Length > 0)
                dest.Add(new XElement(source.FirstAttribute.Value, textContent.ToString().Replace("\r\n", " ")));

            //dest.Add();
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
