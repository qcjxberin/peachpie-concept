﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Pchp.Syntax;
using Pchp.Syntax.AST;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// PHP class as a CLR type.
    /// </summary>
    internal sealed class SourceNamedTypeSymbol : NamedTypeSymbol
    {
        readonly TypeDecl _syntax;
        readonly SourceFileSymbol _file;

        readonly ImmutableArray<Symbol> _members;
        
        NamedTypeSymbol _lazyBaseType;

        public SourceFileSymbol ContainingFile => _file;

        public SourceNamedTypeSymbol(SourceFileSymbol file, TypeDecl syntax)
        {
            Contract.ThrowIfNull(file);

            _syntax = syntax;
            _file = file;

            _members = LoadMethods()
                .Concat<Symbol>(LoadFields())
                .ToImmutableArray();
        }

        IEnumerable<SourceMethodSymbol> LoadMethods()
        {
            foreach (var m in _syntax.Members.OfType<MethodDecl>())
            {
                yield return new SourceMethodSymbol(this, m);
            }
        }

        IEnumerable<SourceFieldSymbol> LoadFields()
        {
            foreach (var flist in _syntax.Members.OfType<FieldDeclList>())
            {
                foreach (var f in flist.Fields)
                {
                    yield return new SourceFieldSymbol(this, f, flist.Modifiers);
                }
            }
        }

        public override INamedTypeSymbol BaseType
        {
            get
            {
                if (_lazyBaseType == null && _syntax.BaseClassName.HasValue)
                {
                    if (_syntax.BaseClassName.Value.IsGeneric)
                        throw new NotImplementedException();

                    _lazyBaseType = (NamedTypeSymbol)DeclaringCompilation.GetTypeByMetadataName(_syntax.BaseClassName.Value.QualifiedName.ClrName())
                        ?? new MissingMetadataTypeSymbol(_syntax.BaseClassName.Value.QualifiedName.ClrName(), 0, false);
                }

                return _lazyBaseType;
            }
        }

        /// <summary>
        /// Gets type declaration syntax node.
        /// </summary>
        internal TypeDecl Syntax => _syntax;

        public override int Arity => 0;

        internal override IModuleSymbol ContainingModule => _file.SourceModule;

        public override Symbol ContainingSymbol => _file.SourceModule;

        internal override PhpCompilation DeclaringCompilation => _file.DeclaringCompilation;

        public override string Name => _syntax.Name.Value;

        public override string NamespaceName
            => (_syntax.Namespace != null) ? _syntax.Namespace.QualifiedName.ClrName() : string.Empty;

        public override TypeKind TypeKind => TypeKind.Class;

        public override Accessibility DeclaredAccessibility => _syntax.MemberAttributes.GetAccessibility();

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get
            {
                return ImmutableArray<SyntaxReference>.Empty;
            }
        }

        public override bool IsAbstract => _syntax.MemberAttributes.IsAbstract();

        public override bool IsSealed => _syntax.MemberAttributes.IsSealed();

        public override bool IsStatic => _syntax.MemberAttributes.IsStatic();

        public override ImmutableArray<Location> Locations
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        internal override TypeLayout Layout => default(TypeLayout);

        internal override ObsoleteAttributeData ObsoleteAttributeData
        {
            get
            {
                return null;
            }
        }

        internal override bool MangleName => false;

        public override ImmutableArray<Symbol> GetMembers() => _members;

        public override ImmutableArray<Symbol> GetMembers(string name)
            => _members.Where(s => s.Name.EqualsOrdinalIgnoreCase(name)).AsImmutable();

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers()
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name)
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }

        internal override IEnumerable<IFieldSymbol> GetFieldsToEmit()
        {
            return _members.OfType<IFieldSymbol>();
        }

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit()
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }
    }
}
