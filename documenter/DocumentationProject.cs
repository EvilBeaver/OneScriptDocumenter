using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace documenter
{
    class DocumentationProject
    {
        public string[] Assemblies;
        public Section[] Sections;
    }

    class Section
    {
        public string SectionName { get; set; }
        public object[] Articles { get; set; }
    }
}
