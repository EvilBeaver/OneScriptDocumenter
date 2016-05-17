using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OneScriptDocumenter
{
    class LoadedAssembly
    {
        private Assembly _library;
        private AssemblyLoader _assemblyLoader;

        private Type[] _allTypes;

        public LoadedAssembly(Assembly library, AssemblyLoader assemblyLoader)
        {
            _library = library;
            _assemblyLoader = assemblyLoader;
        }

        public string Name
        {
            get
            {
                return _library.GetName().Name;
            }
        }

        public Type[] AllTypes
        {
            get
            {
                if(_allTypes == null)
                    _allTypes = _library.GetTypes();

                return _allTypes;
            }
        }

        public Type[] GetMarkedTypes(ScriptMemberType markupElement)
        {
            var attributeType = _assemblyLoader.MemberTypeToAttributeType(markupElement);
            var types = AllTypes.Where(x => x.GetCustomAttributesData()
                .FirstOrDefault(attr => attr.AttributeType == attributeType) != null);

            return types.ToArray();
        }

        public CustomAttributeData GetMarkup(MemberInfo member, ScriptMemberType markupElement)
        {
            var type = _assemblyLoader.MemberTypeToAttributeType(markupElement);
            return member.GetCustomAttributesData().FirstOrDefault(attr => attr.AttributeType == type);
        }

        public CustomAttributeData GetMarkup(Type type, ScriptMemberType markupElement)
        {
            var attributeType = _assemblyLoader.MemberTypeToAttributeType(markupElement);
            var result = type.GetCustomAttributesData().FirstOrDefault(attr => attr.AttributeType == attributeType);
            return result;
        }

    }
}
