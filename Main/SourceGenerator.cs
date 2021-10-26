using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
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
					.Select(c => t.Item2.GetDeclaredSymbol(c))
					.Where(c => c is not null)
					.OfType<INamedTypeSymbol>())
				.Distinct(SymbolEqualityComparer.Default)
				.OfType<INamedTypeSymbol>()
				.ToList());

			var content = @$"namespace MrMeeseeks.PlantUMLGenerator 
{{ 
	public class {className} 
	{{
		public string PublicOrInternal => @""
{PlantUmlContent(readOnlyCollection, a => a is Accessibility.Public or Accessibility.Internal)}
"";
		public string Public => @""
{PlantUmlContent(readOnlyCollection, a => a is Accessibility.Public)}
"";
	}}
}}";
			
			context.AddSource(
					$"MrMeeseeks.PlantUMLGenerator.{className}.g.cs", 
					SourceText.From(
						content, 
						Encoding.UTF8));

			string PlantUmlContent(IReadOnlyList<INamedTypeSymbol> namedTypeSymbols, Func<Accessibility, bool> accessibilityPredicate)
			{
				var builder = new StringBuilder();
				
				foreach (var namedTypeSymbol in namedTypeSymbols
					.Where(nts => accessibilityPredicate(nts.DeclaredAccessibility)))
				{
					var isClass = namedTypeSymbol.TypeKind == TypeKind.Class;
					var isInterface = namedTypeSymbol.TypeKind == TypeKind.Interface;
					var isEnum = namedTypeSymbol.TypeKind == TypeKind.Enum;
					if (!isClass && !isInterface && !isEnum) continue;

					if (isClass || isInterface)
					{
						var classOrInterface = isClass ? "class" : "interface";
						builder = builder.AppendLine($"{(isClass && namedTypeSymbol.IsAbstract ? "abstract " : "")}{classOrInterface} {FullName(namedTypeSymbol)} {{");
						builder = namedTypeSymbol.GetMembers()
							.Where(s => accessibilityPredicate(s.DeclaredAccessibility))
							.Aggregate(builder, (current, member) => member switch
							{
								IFieldSymbol field => current.AppendLine($"{PlantUmlAccessModifier(field.DeclaredAccessibility)}{FullName(field.Type)} {field.Name}"),
								IMethodSymbol { MethodKind: not MethodKind.PropertyGet and not MethodKind.PropertySet } method => current.AppendLine($"{PlantUmlAccessModifier(method.DeclaredAccessibility)}{(method.ReturnsVoid ? "void" : FullName(method.ReturnType))} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{FullName(p.Type)} {FullName(p)}"))})"),
								IPropertySymbol property => current.AppendLine($"{PlantUmlAccessModifier(property.DeclaredAccessibility)}{FullName(property.Type)} {property.Name}{(property.SetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal } ? " <font color=darkred><==</font>" : "")}{(property.GetMethod is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal } ? " <font color=darkgreen>==></font>" : "")}"),
								_ => current
							});
						builder = builder.AppendLine("}");
						if (namedTypeSymbol.BaseType is {} baseClass && namedTypeSymbols.Contains(baseClass, SymbolEqualityComparer.Default))
							builder = builder.AppendLine($"class {FullName(namedTypeSymbol)} extends {FullName(baseClass)}");
						builder = namedTypeSymbol
							.AllInterfaces
							.Where(nts => namedTypeSymbols.Contains(nts, SymbolEqualityComparer.Default) 
							              && !namedTypeSymbol.AllInterfaces.Any(i => i.AllInterfaces.Contains(nts)))
							.Aggregate(builder, (current, typeSymbol) => 
								current.AppendLine($"{classOrInterface} {FullName(namedTypeSymbol)} implements {FullName(typeSymbol)}"));
					}
					else if (isEnum)
					{
						builder.AppendLine($"enum {FullName(namedTypeSymbol)} {{");
						builder = namedTypeSymbol.MemberNames.Aggregate(builder, (current, memberName) => current.AppendLine(memberName));
						builder = builder.AppendLine("}");
					}
				}

				return $@"@startuml
!theme cyborg-outline
{builder}
@enduml";
			}
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
