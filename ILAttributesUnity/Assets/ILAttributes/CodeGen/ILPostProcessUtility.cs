using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace ILAttributes.CodeGen
{
    public static class ILPostProcessUtility
    {

        public static bool ContainsGenerics(this TypeReference typeReference)
        {
            var elementType = typeReference.GetElementType();
            return elementType.IsGenericInstance||elementType.IsGenericParameter;
            
        }
        private static string corPath=File.Exists(FileUtils.GetCorePathWrittenPath())?File.ReadAllText(FileUtils.GetCorePathWrittenPath()):"" ;

        public static ModuleDefinition Core;
        internal static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = resolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);
            //apparently, it will happen that when we ask to resolve a type that lives inside Unity.Entities, and we
            //are also postprocessing Unity.Entities, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //unity.entities itself as well.
            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);
            //
            if (Core == null &&!string.IsNullOrEmpty(corPath))
            {
                Core =  AssemblyDefinition.ReadAssembly(corPath).MainModule;
            
            }
            // else
            // {
            //     Core = AssemblyDefinition.ReadAssembly(CoreLib.Location).MainModule;
            // }
              return assemblyDefinition;
        }

        internal static ILPostProcessResult GetResult(AssemblyDefinition assemblyDefinition,
            List<DiagnosticMessage> diagnostics)
        {
            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }
    }

    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module)
        {
            return new PostProcessorReflectionImporter(module);
        }
    }

    internal class PostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";
        private AssemblyNameReference _correctCorlib;

        public PostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            _correctCorlib = module.AssemblyReferences.FirstOrDefault(a =>
                a.Name is "mscorlib" or "netstandard" or SystemPrivateCoreLib);
           
           
        }

        public override AssemblyNameReference ImportReference(AssemblyName reference)
        {
            if (_correctCorlib != null && reference.Name is SystemPrivateCoreLib)
                return _correctCorlib;

            return base.ImportReference(reference);
        }
    }
}