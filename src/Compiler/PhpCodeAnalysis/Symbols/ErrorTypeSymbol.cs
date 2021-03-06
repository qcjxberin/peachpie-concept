﻿using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// An ErrorSymbol is used when the compiler cannot determine a symbol object to return because
    /// of an error. For example, if a field is declared "Foo x;", and the type "Foo" cannot be
    /// found, an ErrorSymbol is returned when asking the field "x" what it's type is.
    /// </summary>
    internal abstract partial class ErrorTypeSymbol : NamedTypeSymbol, IErrorTypeSymbol
    {
        internal static readonly ErrorTypeSymbol UnknownResultType = new UnsupportedMetadataTypeSymbol();

        public abstract CandidateReason CandidateReason { get; }

        public override string Name => string.Empty;

        public override int Arity => 0;

        public override TypeKind TypeKind => TypeKind.Error;

        public override SymbolKind Kind => SymbolKind.ErrorType;

        public override Accessibility DeclaredAccessibility => Accessibility.NotApplicable;

        public virtual ImmutableArray<ISymbol> CandidateSymbols => ImmutableArray<ISymbol>.Empty;

        internal override bool MangleName => false;

        internal override TypeLayout Layout => default(TypeLayout);

        public override Symbol ContainingSymbol => null;

        public override ImmutableArray<Location> Locations => ImmutableArray<Location>.Empty;

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;

        public override bool IsStatic => false;

        public override bool IsAbstract => false;

        public override bool IsSealed => false;

        internal override ObsoleteAttributeData ObsoleteAttributeData => null;

        internal override IEnumerable<IFieldSymbol> GetFieldsToEmit()
        {
            yield break;
        }

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit() => ImmutableArray<NamedTypeSymbol>.Empty;

        public override ImmutableArray<Symbol> GetMembers() => ImmutableArray<Symbol>.Empty;

        public override ImmutableArray<Symbol> GetMembers(string name) => ImmutableArray<Symbol>.Empty;

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => ImmutableArray<NamedTypeSymbol>.Empty;

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name) => ImmutableArray<NamedTypeSymbol>.Empty;
    }

    internal sealed class UnsupportedMetadataTypeSymbol : ErrorTypeSymbol
    {
        readonly BadImageFormatException _mrEx;

        internal UnsupportedMetadataTypeSymbol(BadImageFormatException mrEx = null)
        {
            _mrEx = mrEx;
        }

        public override CandidateReason CandidateReason => CandidateReason.None;

        internal override bool MangleName
        {
            get
            {
                return false;
            }
        }
    }

    internal class MissingMetadataTypeSymbol : ErrorTypeSymbol
    {
        protected readonly string _name;
        protected readonly int _arity;
        protected readonly bool _mangleName;

        public MissingMetadataTypeSymbol(string name, int arity, bool mangleName)
        {
            Debug.Assert(name != null);

            this._name = name;
            this._arity = arity;
            this._mangleName = (mangleName && arity > 0);

        }
        public override CandidateReason CandidateReason => CandidateReason.None;

        public override string Name => _name;
        internal override bool MangleName => _mangleName;
        public override int Arity => _arity;
    }
}
