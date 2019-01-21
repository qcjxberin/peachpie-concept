﻿using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Peachpie.CodeAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// List of overloads for a function call.
    /// </summary>
    internal struct OverloadsList
    {
        /// <summary>
        /// Defines the scope of members visibility.
        /// Used to resolve visibility of called methods and accessed properties.
        /// </summary>
        public struct VisibilityScope
        {
            /// <summary>
            /// The type scope if resolved.
            /// Can be <c>null</c> when outside of class or when scope is unknown in compile-time.
            /// </summary>
            public NamedTypeSymbol Scope;

            /// <summary>
            /// Whether the scope can change.
            /// In result visibility of private and protected members may change in runtime. 
            /// </summary>
            public bool ScopeIsDynamic;

            /// <summary>
            /// Builds the visibility scope.
            /// </summary>
            public static VisibilityScope Create(NamedTypeSymbol self, SourceRoutineSymbol routine)
            {
                return new VisibilityScope()
                {
                    Scope = self,
                    ScopeIsDynamic = self.IsTraitType() || routine is SourceLambdaSymbol || (routine?.IsGlobalScope == true),
                };
            }
        }

        readonly MethodSymbol _single;
        readonly MethodSymbol[] _methods;

        public OverloadsList(MethodSymbol method)
        {
            _single = method ?? throw ExceptionUtilities.ArgumentNull();
            _methods = default;
        }

        public OverloadsList(MethodSymbol[] methods)
        {
            if (methods.Length == 1)
            {
                _single = methods[0];
                _methods = default;
            }
            else
            {
                _single = default;
                _methods = methods;
            }
        }

        /// <summary>
        /// Tries to resolve method in design time.
        /// </summary>
        /// <returns>
        /// Might return one of following:
        /// - resolved single <see cref="MethodSymbol"/>
        /// - <see cref="MissingMethodSymbol"/>
        /// - <see cref="AmbiguousMethodSymbol"/>
        /// - <see cref="InaccessibleMethodSymbol"/>
        /// </returns>
        public MethodSymbol/*!*/Resolve(TypeRefContext typeCtx, ImmutableArray<BoundArgument> args, VisibilityScope scope)
        {
            if (_single != null)
            {
                return IsAccessible(_single, scope)
                    ? scope.ScopeIsDynamic && IsNonPublic(_single)
                        ? new AmbiguousMethodSymbol(ImmutableArray.Create(_single), false) // TODO: find a way on how to disable this check in CLR
                        : _single
                    : new InaccessibleMethodSymbol(ImmutableArray.Create(_single));
            }

            if (_methods == null || _methods.Length == 0)
            {
                return new MissingMethodSymbol();
            }

            // see Pchp.Core.Dynamic.OverloadBinder

            // collect valid methods:
            var result = new List<MethodSymbol>(_methods.Where(MethodSymbolExtensions.IsValidMethod));

            // only visible methods:
            RemoveInaccessible(result, scope);

            if (result.Count == 0)
            {
                return new InaccessibleMethodSymbol(_methods.AsImmutable());
            }

            if (scope.ScopeIsDynamic && result.Any(IsNonPublic))
            {
                // we have to postpone the resolution to runtime:
                return new AmbiguousMethodSymbol(result.AsImmutable(), false);
            }

            if (result.Count == 1)
            {
                return result[0];
            }

            // TODO: cost of args convert operation

            // by params count

            var result2 = new List<MethodSymbol>();

            foreach (var m in result)
            {
                var nmandatory = 0;
                var hasoptional = false;
                var hasparams = false;
                var match = true;
                var hasunpacking = false;

                var expectedparams = m.GetExpectedArguments(typeCtx);

                foreach (var p in expectedparams)
                {
                    hasoptional |= p.DefaultValue != null;
                    hasparams |= p.IsVariadic;
                    if (!hasoptional && !hasparams) nmandatory++;

                    if (p.Index < args.Length)
                    {
                        hasunpacking |= args[p.Index].IsUnpacking;

                        // TODO: check args[i] is convertible to p.Type
                        var p_type = typeCtx.WithoutNull(p.Type);
                        var a_type = typeCtx.WithoutNull(args[p.Index].Value.TypeRefMask);

                        match &= a_type == p_type && !hasunpacking; // check types match (ignoring NULL flag)
                    }
                }

                //
                if ((args.Length >= nmandatory || hasunpacking) && (hasparams || args.Length <= expectedparams.Length))
                {
                    // TODO: this is naive implementation of overload resolution,
                    // make it properly using Conversion Cost
                    if (match && !hasparams)
                    {
                        return m;   // perfect match
                    }

                    //
                    result2.Add(m);
                }
            }

            //
            return (result2.Count == 1) ? result2[0] : new AmbiguousMethodSymbol(result.AsImmutable(), true);
        }

        static bool IsNonPublic(MethodSymbol m) => m.DeclaredAccessibility != Accessibility.Public;

        /// <summary>
        /// Removes methods that are inaccessible for sure.
        /// </summary>
        static void RemoveInaccessible(List<MethodSymbol> methods, VisibilityScope scope)
        {
            for (int i = methods.Count - 1; i >= 0; i--)
            {
                if (!IsAccessible(methods[i], scope))
                {
                    methods.RemoveAt(i);
                }
            }
        }

        static bool IsAccessible(MethodSymbol m, VisibilityScope scope)
        {
            return (
                m.DeclaredAccessibility != Accessibility.ProtectedAndInternal && // C# 7.2 "private protected"
                m.DeclaredAccessibility != Accessibility.Internal && // "internal"
                (scope.ScopeIsDynamic || m.IsAccessible(scope.Scope)) &&  // method is accessible (or might be in runtime)
                !m.IsFieldsOnlyConstructor()    // method is not a special .ctor which is not accessible from user's code
                );
        }
    }
}
