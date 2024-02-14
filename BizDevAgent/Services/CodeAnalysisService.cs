using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text;

namespace BizDevAgent.Services
{
    /// <summary>
    /// A SyntaxRewriter to rename classes and their constructors.
    /// </summary>
    public class ClassAndConstructorRenamer : CSharpSyntaxRewriter
    {
        private readonly string _oldClassName;
        private readonly string _newClassName;

        public ClassAndConstructorRenamer(string oldClassName, string newClassName)
        {
            _oldClassName = oldClassName;
            _newClassName = newClassName;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // Rename the class if it matches the old class name
            if (node.Identifier.Text == _oldClassName)
            {
                return node.WithIdentifier(SyntaxFactory.Identifier(_newClassName));
            }

            return base.VisitClassDeclaration(node);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            // Rename the constructor if its identifier matches the old class name
            if (node.Identifier.Text == _oldClassName)
            {
                return node.WithIdentifier(SyntaxFactory.Identifier(_newClassName));
            }

            return base.VisitConstructorDeclaration(node);
        }
    }

    /// <summary>
    /// Replace method bodies by applying the given transformation.  Used by the code summarization routines.
    /// </summary>
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

    public class PublicApiRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            // Remove all constructors
            return null;
        }

        public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
        {
            // Remove all using directives
            return null;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            bool hasAgentAttribute = node.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attr => attr.Name.ToString().EndsWith("AgentApi"));

            if (!hasAgentAttribute)
            {
                return null;
            }

            // For the RepositoryQuerySession class, remove all members except public methods
            var newMembers = node.Members.Where(member =>
                member is MethodDeclarationSyntax method &&
                method.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                !method.AttributeLists.Any(attr => attr.Attributes.Any(a => a.Name.ToString().Contains("Obsolete")))); // Example to exclude [Obsolete] methods

            // Preserve documentation immediately preceding method bodies
            var newMethods = newMembers.Select(method =>
            {
                if (method is MethodDeclarationSyntax methodSyntax)
                {
                    var leadingTrivia = methodSyntax.GetLeadingTrivia()
                        .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
                    return methodSyntax
                        .WithBody(null)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                        .WithLeadingTrivia(leadingTrivia)
                        .NormalizeWhitespace();
                }
                return method;
            });

            // Create a new class declaration with only the filtered methods
            var newClass = node.WithMembers(SyntaxFactory.List(newMethods));

            return base.VisitClassDeclaration(newClass);
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Check if the method has the [HideFromSummary] attribute
            bool hasAgentAttribute = node.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attr => attr.Name.ToString().EndsWith("AgentApi"));

            if (!hasAgentAttribute)
            {
                return null;
            }

            // Replace method body with a semicolon for public methods
            if (node.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                var newNode = node
                    .WithBody(null)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed); // For readability

                return base.VisitMethodDeclaration(newNode);
            }

            return null; // Remove non-public methods
        }

        // Optionally, override other Visit* methods if needed.
    }

    public class CodeAnalysisService : Service
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

        /// <summary>
        /// Given csharp source code, rename the class from the old name to a new name.
        /// </summary>
        public string RenameClass(string sourceCode, string oldClassName, string newClassName)
        {
            // Parse the source code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            // Find all class declarations matching the old class name
            var classDeclarations = root.DescendantNodes()
                                         .OfType<ClassDeclarationSyntax>()
                                         .Where(c => c.Identifier.Text == oldClassName);

            var classDeclarationsList = classDeclarations.ToList();
            if (classDeclarationsList.Count != 1)
            {
                throw new InvalidOperationException($"Found {classDeclarationsList.Count} classes named '{oldClassName}' to rename");
            }

            // Create a rewriter to replace class and constructor identifiers
            var rewriter = new ClassAndConstructorRenamer(oldClassName, newClassName);
            var newRoot = rewriter.Visit(root);

            var rewriter2 = new ClassAndConstructorRenamer(oldClassName, newClassName);
            newRoot = rewriter2.Visit(newRoot);

            // Return the modified source code
            return newRoot.ToFullString();
        }

        /// <summary>
        /// Given some source code, strip out the method bodies and anything else that seems extraneous to its use as a public API.
        /// </summary>
        public string GeneratePublicApiSkeleton(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var rewriter = new PublicApiRewriter();
            var newRoot = rewriter.Visit(syntaxTree.GetRoot());

            var newCode = newRoot.NormalizeWhitespace().ToFullString();

            return newCode;
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
