using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneScriptDocumenter.XmlDoc
{
    class TypesDictionaryNew
    {
        public List<TypeInformation> Types { get; } = new List<TypeInformation>();

        public TypeInformation FindByFullName(string fullName)
        {
            foreach (var curType in Types)
            {
                if (curType.FullName == fullName)
                {
                    return curType;
                }
            }

            return null;
        }

        public void Add(TypeInformation value)
        {
            if (FindByFullName(value.FullName) == null)
            {
                Types.Add(value);
            }
        }
    }

    class TypeInformation
    {
        public string FullName = "";
        public string ShortName = "";
        public string NameEng = "";
        public string NameRus = "";
    }
}
