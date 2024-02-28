using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace Agent.Services
{
    public class DynamicCompiler
    {
        public Assembly CompileAndLoadAssembly(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var loadedAssemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location));

            var references = loadedAssemblies
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName: Path.GetRandomFileName(),
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                // Handle compilation failures
                throw new InvalidOperationException("Injected code has compile errors");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }
    }

    public class VisualStudioService : Service
    {
        public Assembly InjectCode(string code)
        {
            // TODO gsemple: this doesn't seem like the right place for .net framework injection code, its valid for anyone with a .net runtime
            // perhaps have a runtime framework service??
            // Dynamically compile and load the assembly
            var compiler = new DynamicCompiler();
            Assembly assembly = compiler.CompileAndLoadAssembly(code);
            return assembly;
        }
    }
}