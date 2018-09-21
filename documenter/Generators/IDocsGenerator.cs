using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace documenter.Generators
{
    interface IDocsGenerator
    {
        string BaseUrl { get; set; }
        DocumentationProject Project { get; set; }
        void DumpTo(string destination, XDocument metadata);
    }
}
