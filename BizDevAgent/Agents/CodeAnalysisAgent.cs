using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text;

namespace BizDevAgent.Agents
{
    public class MethodBodyRewriter : CSharpSyntaxRewriter
    {
        private readonly Func<string, string, Task<string>> _transformMethodBody;

        public MethodBodyRewriter(Func<string, string, Task<string>> transformMethodBody)
        {
            _transformMethodBody = transformMethodBody;
        }

        // Placeholder for async transformation; actual transformation occurs in a separate method
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // This will be replaced with an async call in ApplyTransformAsync
            return node;
        }

        public async Task<SyntaxNode> ApplyTransformAsync(MethodDeclarationSyntax node)
        {
            var originalMethodText = node.ToFullString();
            var originalBodyText = node.Body?.ToFullString() ?? string.Empty;
            var newBodyText = await _transformMethodBody(originalMethodText, originalBodyText);

            // Attempt to parse the new body text within a minimal class/method structure
            var dummyClassText = $"class DummyClass {{ void DummyMethod() {{ {newBodyText} }} }}";
            var parsedSyntax = SyntaxFactory.ParseCompilationUnit(dummyClassText);

            // Use FirstOrDefault to safely attempt to retrieve the method declaration
            var dummyMethod = parsedSyntax.DescendantNodes()
                                          .OfType<MethodDeclarationSyntax>()
                                          .FirstOrDefault();

            if (dummyMethod == null || dummyMethod.Body == null)
            {
                throw new InvalidOperationException("Failed to parse the transformed method body.");
            }

            var newBody = dummyMethod.Body;

            var newNode = node.WithBody(newBody)
                              .WithExpressionBody(null)
                              .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None));

            return newNode;
        }
    }


    public class CodeAnalysisAgent : Agent
    {
        public async Task<string> TransformMethodBodies(string processingTag, string sourceCode, Func<string, string, Task<string>> transformMethodBody)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var rewriter = new MethodBodyRewriter(transformMethodBody);

            // Manually iterate over method declarations and apply the transformation
            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            foreach (var methodDeclaration in methodDeclarations)
            {
                var identifier = GetMethodIdentifier(methodDeclaration);
                var transformedNode = await rewriter.ApplyTransformAsync(methodDeclaration);

                // Find the method in the new 'root' that matches the identifier
                var methodToReplace = root.DescendantNodes()
                                           .OfType<MethodDeclarationSyntax>()
                                           .FirstOrDefault(m => GetMethodIdentifier(m) == identifier);

                if (methodToReplace != null)
                {
                    root = root.ReplaceNode(methodToReplace, transformedNode);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"// METHOD BODIES TRANSFORMED FOR THIS FILE: {processingTag}");
            sb.AppendLine(root.ToFullString());
            return sb.ToString();
        }

        private string GetMethodIdentifier(MethodDeclarationSyntax method)
        {
            // Start with the method name
            var identifierBuilder = new StringBuilder(method.Identifier.ValueText);

            // Add a distinguishing feature for methods without parameters to differentiate from those with parameters
            if (!method.ParameterList.Parameters.Any())
            {
                identifierBuilder.Append("()");
            }

            // Append parameter types to handle overloads
            foreach (var parameter in method.ParameterList.Parameters)
            {
                // Append the type of each parameter
                // Note: Consider including the parameter modifiers (ref, out, in) if necessary
                identifierBuilder.Append($"_{parameter.Type}");
            }

            return identifierBuilder.ToString();
        }
    }
}
