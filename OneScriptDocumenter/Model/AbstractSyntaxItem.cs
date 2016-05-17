using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneScriptDocumenter.Model
{
    abstract class AbstractSyntaxItem
    {
        public MultilangString Caption { get; set; }
        public MultilangString Description { get; set; }

        public IList<AbstractSyntaxItem> Children { get; protected set; }
    }
}
