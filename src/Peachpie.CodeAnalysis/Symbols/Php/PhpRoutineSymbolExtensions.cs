﻿using Devsense.PHP.Syntax;
using Devsense.PHP.Syntax.Ast;
using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Roslyn.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Symbols
{
    public static class PhpRoutineSymbolExtensions
    {
        /// <summary>
        /// Gets value indicating the routine does not override any other routine.
        /// (static methods, private methods or sealed virtual methods not overriding anything)
        /// </summary>
        static bool IsNotOverriding(SourceRoutineSymbol routine)
        {
            return
                routine.IsStatic ||
                routine.DeclaredAccessibility == Accessibility.Private ||
                (routine.OverriddenMethod == null && (routine.ContainingType.IsSealed || routine.IsSealed));
        }

        /// <summary>
        /// Constructs most appropriate CLR return type of given routine.
        /// The method handles returning by alias, PHP7 return type, PHPDoc @return tag and result of flow analysis.
        /// In case the routine is an override or can be overriden, the CLR type is a value.
        /// </summary>
        internal static TypeSymbol ConstructClrReturnType(SourceRoutineSymbol routine)
        {
            var compilation = routine.DeclaringCompilation;

            // if the method is generator and can't be overriden then the return type must be generator 
            // TODO: would not be necessary if GN_SGS got fixed (the routine could report the return type correctly itself)
            if (routine.IsGeneratorMethod())
            {
                // if non-virtual -> return Generator directly
                if (IsNotOverriding(routine))
                {
                    return compilation.CoreTypes.Generator;
                }
                else //can't be sure -> play safe with PhpValue
                {
                    return compilation.CoreTypes.PhpValue;
                }
            }

            // &
            if (routine.SyntaxSignature.AliasReturn)
            {
                return compilation.CoreTypes.PhpAlias;
            }

            // : return type
            if (routine.SyntaxReturnType != null)
            {
                return compilation.GetTypeFromTypeRef(routine.SyntaxReturnType, routine.ContainingType as SourceTypeSymbol);
            }

            // for non virtual methods:
            if (IsNotOverriding(routine))
            {
                // /** @return */
                var typeCtx = routine.TypeRefContext;
                if (routine.PHPDocBlock != null && (compilation.Options.PhpDocTypes & PhpDocTypes.ReturnTypes) != 0)
                {
                    var returnTag = routine.PHPDocBlock.Returns;
                    if (returnTag != null && returnTag.TypeNames.Length != 0)
                    {
                        var tmask = PHPDoc.GetTypeMask(typeCtx, returnTag.TypeNamesArray, routine.GetNamingContext());
                        if (!tmask.IsVoid && !tmask.IsAnyType)
                        {
                            return compilation.GetTypeFromTypeRef(typeCtx, tmask);
                        }
                    }
                }

                // determine from code flow
                return compilation.GetTypeFromTypeRef(typeCtx, routine.ResultTypeMask);
            }
            else
            {
                // TODO: an override that respects the base? check routine.ResultTypeMask (flow analysis) and base implementation is it matches
            }

            // any value by default
            return compilation.CoreTypes.PhpValue;
        }

        /// <summary>
        /// Gets expected return type mask of given symbol (field, function, method or property).
        /// </summary>
        /// <remarks>Returned type mask corresponds to types that can be returned by invoking given symbol.</remarks>
        public static TypeRefMask GetResultType(this IPhpValue symbol, TypeRefContext ctx)
        {
            Contract.ThrowIfNull(symbol);
            Contract.ThrowIfNull(ctx);

            TypeSymbol t;

            if (symbol is FieldSymbol)
            {
                t = ((FieldSymbol)symbol).Type;
            }
            else if (symbol is MethodSymbol)
            {
                var m = (MethodSymbol)symbol;
                var r = symbol as SourceRoutineSymbol;

                // if the method is generator use ConstructClrReturnType analysis for return type
                // TODO: would not be necessary if GN_SGS got fixed (the routine could report the return type correctly itself)
                if (r != null && r.IsGeneratorMethod())
                {
                    t = m.ReturnType;
                }
                else if (r != null && r.IsStatic && r.SyntaxReturnType == null)
                {
                    // In case of a static function, we can return expected return type mask exactly.
                    // Such function cannot be overriden and we know exactly what the return type will be even the CLR type covers more possibilities.
                    return ctx.AddToContext(r.TypeRefContext, r.ResultTypeMask);
                }
                else
                {
                    t = m.ReturnType;
                }
            }
            else if (symbol is PropertySymbol)
            {
                t = ((PropertySymbol)symbol).Type;
            }
            else if (symbol is ParameterSymbol)
            {
                var ps = (ParameterSymbol)symbol;
                t = ps.Type;

                if (ps.IsParams)
                {
                    Debug.Assert(t.IsSZArray());
                    return ctx.GetArrayTypeMask(TypeRefFactory.CreateMask(ctx, ((ArrayTypeSymbol)t).ElementType));
                }
            }
            else
            {
                throw ExceptionUtilities.UnexpectedValue(symbol);
            }

            // create the type mask from the CLR type symbol
            var mask = TypeRefFactory.CreateMask(ctx, t);

            // [CastToFalse]
            if (symbol is IPhpRoutineSymbol phpr && phpr.CastToFalse)
            {
                mask |= ctx.GetBooleanTypeMask();    // the function may return FALSE

                // remove NULL (NULL is changed to FALSE), note it also can't return -1
                mask = ctx.WithoutNull(mask);
            }

            //
            return mask;
        }

        /// <summary>
        /// Resolves list of input arguments.
        /// Implicit parameters passed by compiler are ignored.
        /// </summary>
        /// <param name="routine">Routine.</param>
        /// <param name="ctx">TYpe context to transmer type masks into.</param>
        /// <returns>List of input PHP arguments.</returns>
        public static PhpParam[] GetExpectedArguments(this IPhpRoutineSymbol routine, TypeRefContext ctx)
        {
            Contract.ThrowIfNull(routine);

            var ps = routine.Parameters;
            //var table = (routine as SourceRoutineSymbol)?.LocalsTable;
            var result = new List<PhpParam>(ps.Length);

            int index = 0;

            foreach (ParameterSymbol p in ps)
            {
                if (result.Count == 0 && p.IsImplicitlyDeclared && !p.IsParams)
                {
                    continue;
                }

                // default value (bound expression)
                ConstantValue cvalue;
                var psrc = p as SourceParameterSymbol;
                var defaultexpr = psrc != null
                    ? psrc.Initializer
                    : ((cvalue = p.ExplicitDefaultConstantValue) != null ? new BoundLiteral(cvalue.Value) : null);

                //
                var phpparam = new PhpParam(
                    index++,
                    TypeRefFactory.CreateMask(ctx, p.Type),
                    p.RefKind != RefKind.None,
                    p.IsParams,
                    isPhpRw: p.GetPhpRwAttribute() != null,
                    defaultValue: defaultexpr);

                result.Add(phpparam);
            }

            //
            return result.ToArray();
        }

        /// <summary>
        /// Gets mask with 1-bits corresponding to an argument that must be passed by reference.
        /// </summary>
        internal static PhpSignatureMask GetByRefArguments(this IPhpRoutineSymbol routine)
        {
            Contract.ThrowIfNull(routine);

            var mask = new PhpSignatureMask();
            var ps = routine.Parameters;

            int index = 0;

            foreach (ParameterSymbol p in ps)
            {
                if (index == 0 && p.IsImplicitlyDeclared && !p.IsParams)
                {
                    continue;
                }

                if (p.IsParams)
                {
                    if (((ArrayTypeSymbol)p.Type).ElementType.Is_PhpAlias())    // PHP: ... &$p
                    {
                        mask.SetFrom(index++);
                    }
                }
                else
                {
                    mask[index++] =
                        p.RefKind != RefKind.None ||  // C#: ref|out p
                        p.Type.Is_PhpAlias();         // PHP: &$p
                }
            }

            return mask;
        }

        /// <summary>
        /// Gets additional flags of the caller routine.
        /// </summary>
        public static RoutineFlags InvocationFlags(this IPhpRoutineSymbol routine)
        {
            RoutineFlags f = RoutineFlags.None;

            var ps = routine.Parameters;
            foreach (var p in ps)
            {
                if (p.IsImplicitlyDeclared)
                {
                    if (SpecialParameterSymbol.IsQueryValueParameter(p, out var ctor, out var container))
                    {
                        switch (container)
                        {
                            case SpecialParameterSymbol.QueryValueTypes.CallerArgs:
                                f |= RoutineFlags.UsesArgs;
                                break;
                            case SpecialParameterSymbol.QueryValueTypes.LocalVariables:
                                f |= RoutineFlags.UsesLocals;
                                break;
                        }
                    }
                    else if (SpecialParameterSymbol.IsCallerStaticClassParameter(p))
                    {
                        f |= RoutineFlags.UsesLateStatic;
                    }
                }
                else
                {
                    // implicit parameters are at begining only
                    break;
                }
            }

            return f;
        }

        /// <summary>
        /// Gets value indicating the routine was found containing <c>yield</c>
        /// hence it is considered as a generator state machine method.
        /// </summary>
        /// <param name="routine">The analysed routine.</param>
        /// <returns>Value indicating the routine gets a generator.</returns>
        internal static bool IsGeneratorMethod(this SourceRoutineSymbol routine) => (routine.Flags & RoutineFlags.IsGenerator) != 0;

        /// <summary>
        /// Gets enumeration of all routines (global code, functions and methods) within the file.
        /// </summary>
        internal static IEnumerable<SourceRoutineSymbol> GetAllRoutines(this SourceFileSymbol file)
        {
            // all functions + global code + methods + lambdas
            var funcs = file.Functions.Cast<SourceRoutineSymbol>();
            var main = (SourceRoutineSymbol)file.MainMethod;

            var types = file.ContainedTypes.SelectMany(t => t.AllReachableVersions());
            var methods = types.SelectMany(f => f.GetMembers().OfType<SourceRoutineSymbol>());
            var lambdas = ((ILambdaContainerSymbol)file).Lambdas;

            return funcs.Concat(main).Concat(methods).Concat(lambdas);
        }
    }
}
