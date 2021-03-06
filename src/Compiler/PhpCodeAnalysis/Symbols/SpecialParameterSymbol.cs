﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// Synthetized routine parameter.
    /// </summary>
    class SpecialParameterSymbol : ParameterSymbol
    {
        /// <summary>
        /// Name of special context parameter.
        /// </summary>
        public const string ContextName = "<ctx>";

        readonly MethodSymbol _symbol;
        readonly int _index;
        readonly string _name;
        readonly object _type;

        internal static SpecialParameterSymbol CreateThis(MethodSymbol symbol)
            => new SpecialParameterSymbol(symbol, (TypeSymbol)symbol.ContainingType, Syntax.VariableName.ThisVariableName.Value, -1);

        public SpecialParameterSymbol(MethodSymbol symbol, object type, string name, int index)
        {
            Debug.Assert(type is TypeSymbol || type is CoreType);
            Contract.ThrowIfNull(symbol);
            Contract.ThrowIfNull(type);

            _symbol = symbol;
            _type = type;
            _name = name;
            _index = index;
        }

        public override Symbol ContainingSymbol => _symbol;

        internal override IModuleSymbol ContainingModule => _symbol.ContainingModule;

        public override INamedTypeSymbol ContainingType => _symbol.ContainingType;
        
        public override string Name => _name;

        public override bool IsThis => _index == -1;

        internal override TypeSymbol Type
        {
            get
            {
                if (_type is TypeSymbol)
                    return (TypeSymbol)_type;

                if (_type is CoreType)
                    return ((CoreType)_type).Symbol;

                throw new ArgumentException();
            }
        }

        public override RefKind RefKind => RefKind.None;

        public override int Ordinal => _index;

        public override ImmutableArray<Location> Locations
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        internal override ConstantValue ExplicitDefaultConstantValue => null;   // TODO
    }
}
