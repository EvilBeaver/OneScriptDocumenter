using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarkdownDeep;

namespace OneScriptDocumenter
{
    class MarkdownGen : MarkdownDeep.Markdown
    {
        public override void OnPrepareLink(HtmlTag tag)
        {
            tag.attributes["href"] = tag.attributes["href"] + ".htm";
            base.OnPrepareLink(tag);
        }
    }
}
