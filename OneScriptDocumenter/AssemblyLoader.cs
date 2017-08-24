using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OneScriptDocumenter
{
    class AssemblyLoader
    {
        readonly Type _classAttributeType;
        readonly Type _methodAttributeType;
        readonly Type _propAttributeType;
        readonly Type _constructorAttributeType;
        readonly Type _globalContextAttributeType;

        readonly string _baseDirectory;

        public AssemblyLoader(string baseDirectory)
        {
            _baseDirectory = baseDirectory;

            var engineFile = Path.Combine(_baseDirectory, "ScriptEngine.dll");

            if (!File.Exists(engineFile))
                throw new ArgumentException("Base directory doesn't contain library ScriptEngine.dll");

            var scriptEngineLib = Assembly.ReflectionOnlyLoadFrom(engineFile);

            _classAttributeType = scriptEngineLib.GetType("ScriptEngine.Machine.Contexts.ContextClassAttribute", true);
            _globalContextAttributeType = scriptEngineLib.GetType("ScriptEngine.Machine.Contexts.GlobalContextAttribute", true);
            _methodAttributeType = scriptEngineLib.GetType("ScriptEngine.Machine.Contexts.ContextMethodAttribute", true);
            _propAttributeType = scriptEngineLib.GetType("ScriptEngine.Machine.Contexts.ContextPropertyAttribute", true);
            _constructorAttributeType = scriptEngineLib.GetType("ScriptEngine.Machine.Contexts.ScriptConstructorAttribute", true);

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.ReflectionOnlyLoad(args.Name);
        }

        public LoadedAssembly Load(string assemblyName)
        {
            var library = Assembly.ReflectionOnlyLoadFrom(Path.Combine(_baseDirectory, assemblyName));

            var scriptEngineLibs = library.GetReferencedAssemblies()
                .Where(x => x.Name != "ScriptEngine");

            foreach (var lib in scriptEngineLibs)
            {
                try
                {
                    Assembly.ReflectionOnlyLoad(lib.FullName);
                }
                catch(FileNotFoundException)
                {
                    Assembly.ReflectionOnlyLoadFrom(Path.Combine(_baseDirectory, lib.Name + ".dll"));
                }
            }

            return new LoadedAssembly(library, this);
        }

        public Type MemberTypeToAttributeType(ScriptMemberType memberType)
        {
            switch (memberType)
            {
                case ScriptMemberType.Class:
                    return _classAttributeType;
                case ScriptMemberType.Constructor:
                    return _constructorAttributeType;
                case ScriptMemberType.GlobalContext:
                    return _globalContextAttributeType;
                case ScriptMemberType.Method:
                    return _methodAttributeType;
                case ScriptMemberType.Property:
                    return _propAttributeType;
                default:
                    throw new ArgumentException("Unsupported member type");
            }
        }

    }
}
