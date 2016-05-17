using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneScriptDocumenter
{
    class CommandLineArgs
    {
        string[] _args;
        int _index = 0;

        public CommandLineArgs(string[] argsArray)
        {
            _args = argsArray;
        }

        public string Next()
        {
            if (_index >= _args.Length)
                return null;

            return _args[_index++];
        }
    }
}
