using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MrMeeseeks.PlantUMLGenerator
{
	[Generator]
	public class SourceGenerator : ISourceGenerator
	{
		public void Execute(GeneratorExecutionContext context)
		{
			const string className = "Analysis";

			var readOnlyCollection = new ReadOnlyCollection<INamedTypeSymbol>(context.Compilation.SyntaxTrees
				.Select(st => (st, context.Compilation.GetSemanticModel(st)))
				.SelectMany(t => t.st
					.GetRoot()
					.DescendantNodesAndSelf()
					.OfType<TypeDeclarationSyntax>()
					.Select(c => t.Item2.GetDeclaredSymbol(c))
					.Where(c => c is not null)
					.OfType<INamedTypeSymbol>())
				.Distinct()
				.ToList());
			var builder = new StringBuilder();
			
			foreach (var namedTypeSymbol in readOnlyCollection)
			{
				var isClass = namedTypeSymbol.TypeKind == TypeKind.Class;
				var isInterface = namedTypeSymbol.TypeKind == TypeKind.Interface;
				if (!isClass && !isInterface) continue;
				builder = builder.AppendLine($"{(isClass ? "class" : "interface")} {FullName(namedTypeSymbol)} {{");
				foreach (var member in namedTypeSymbol.GetMembers())
				{
					switch (member)
					{
						case IFieldSymbol
						{
							DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
						} field:
							builder = builder.AppendLine($"{PlantUmlAccessModifier(field.DeclaredAccessibility)}{FullName(field.Type)} {field.Name}");
							break;
						case IMethodSymbol
						{
							DeclaredAccessibility: Accessibility.Public or Accessibility.Internal, 
							MethodKind: not MethodKind.PropertyGet and not MethodKind.PropertySet
						} method:
							builder = builder.AppendLine(
								$"{PlantUmlAccessModifier(method.DeclaredAccessibility)}{(method.ReturnsVoid ? "void" : FullName(method.ReturnType))} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{FullName(p.Type)} {FullName(p)}"))})");
							break;
						case IPropertySymbol
						{
							DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
						} property:
							builder = builder.AppendLine($"{PlantUmlAccessModifier(property.DeclaredAccessibility)}{FullName(property.Type)} {property.Name}{(property.SetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal } ? " <font color=darkred><==</font>" : "")}{(property.GetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal } ? " <font color=darkgreen>==></font>" : "")}");
							break;
					}
				}
				builder = builder.AppendLine($"}}");
			}

			string plantUmlContent = $@"@startuml
!theme cyborg-outline
{builder}
@enduml";
			
			var content = @$"namespace MrMeeseeks.PlantUMLGenerator 
{{ 
	public class {className} 
	{{
		public string VisibilityDiagram => @""
{plantUmlContent}
"";
	}}
}}";
			
			context.AddSource(
					$"MrMeeseeks.PlantUMLGenerator.{className}.g.cs", 
					SourceText.From(
						content, 
						Encoding.UTF8));
		}

		public void Initialize(GeneratorInitializationContext context)
        {
		}
		
		private static string PlantUmlAccessModifier(Accessibility accessibility) =>
			accessibility switch
			{
				Accessibility.Private => "-",
				Accessibility.ProtectedAndInternal => "#",
				Accessibility.Protected => "#",
				Accessibility.Internal => "~",
				Accessibility.ProtectedOrInternal => "~",
				Accessibility.Public => "+",
				_ => ""
			};
		
		private static string FullName(ISymbol type) =>
			type.ToDisplayString(new SymbolDisplayFormat(
				typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
				genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
				parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
				memberOptions: SymbolDisplayMemberOptions.IncludeRef));
	}
}
