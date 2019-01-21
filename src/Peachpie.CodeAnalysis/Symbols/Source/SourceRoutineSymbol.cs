﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Semantics.Graph;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Pchp.CodeAnalysis.CodeGen;
using Pchp.CodeAnalysis.FlowAnalysis;
using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Syntax;
using Devsense.PHP.Text;
using System.Globalization;
using System.Threading;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// Base symbol representing a method or a function from source.
    /// </summary>
    internal abstract partial class SourceRoutineSymbol : MethodSymbol
    {
        ControlFlowGraph _cfg;
        LocalsTable _locals;

        /// <summary>
        /// Lazily bound semantic block.
        /// Entry point of analysis and emitting.
        /// </summary>
        public override ControlFlowGraph ControlFlowGraph
        {
            get
            {
                if (_cfg == null && this.Statements != null) // ~ Statements => non abstract method
                {
                    // create initial flow state
                    var state = StateBinder.CreateInitialState(this);

                    // build control flow graph
                    _cfg = new ControlFlowGraph(
                        this.Statements,
                        SemanticsBinder.Create(DeclaringCompilation, LocalsTable, ContainingType as SourceTypeSymbol));
                    _cfg.Start.FlowState = state;
                }

                return _cfg;
            }
            internal set
            {
                _cfg = value;
            }
        }

        /// <summary>
        /// Gets table of local variables.
        /// Variables are lazily added to the table.
        /// </summary>
        internal LocalsTable LocalsTable
        {
            get
            {
                if (_locals == null)
                {
                    Interlocked.CompareExchange(ref _locals, new LocalsTable(this), null);
                }

                return _locals;
            }
        }

        internal abstract IList<Statement> Statements { get; }

        protected abstract TypeRefContext CreateTypeRefContext();

        internal abstract Signature SyntaxSignature { get; }

        /// <summary>
        /// Specified return type.
        /// </summary>
        internal abstract TypeRef SyntaxReturnType { get; }

        /// <summary>
        /// Gets routine declaration syntax.
        /// </summary>
        internal abstract AstNode Syntax { get; }

        /// <summary>
        /// Optionaly gets routines PHP doc block.
        /// </summary>
        internal abstract PHPDocBlock PHPDocBlock { get; }

        /// <summary>
        /// Reference to a containing file symbol.
        /// </summary>
        internal abstract SourceFileSymbol ContainingFile { get; }

        public override bool IsUnreachable => Flags.HasFlag(RoutineFlags.IsUnreachable);

        protected ImmutableArray<ParameterSymbol> _implicitParameters;
        private SourceParameterSymbol[] _srcParams;

        /// <summary>Implicitly declared [params] parameter if the routine allows access to its arguments. This allows more arguments to be passed than declared.</summary>
        private SynthesizedParameterSymbol _implicitVarArg; // behaves like a stack of optional parameters

        /// <summary>
        /// Builds implicit parameters before source parameters.
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<ParameterSymbol> BuildImplicitParams()
        {
            var index = 0;

            if (IsStatic)  // instance methods have <ctx> in <this>.<ctx> field, see SourceNamedTypeSymbol._lazyContextField
            {
                // Context <ctx>
                yield return new SpecialParameterSymbol(this, DeclaringCompilation.CoreTypes.Context, SpecialParameterSymbol.ContextName, index++);
            }
        }

        /// <summary>
        /// Gets value indicating this routine requires a special {PhpTypeInfo static} parameter to resolve `static` reserved type inside the routine body.
        /// </summary>
        internal virtual bool RequiresLateStaticBoundParam => false;

        /// <summary>
        /// Collects declaration diagnostics.
        /// </summary>
        public virtual void GetDiagnostics(DiagnosticBag diagnostic)
        {
            // check mandatory behind and optional parameter
            bool foundopt = false;
            foreach (var p in SyntaxSignature.FormalParams)
            {
                if (p.InitValue == null)
                {
                    if (foundopt && !p.IsVariadic)
                    {
                        diagnostic.Add(DiagnosticBagExtensions.ParserDiagnostic(this, p.Span, Devsense.PHP.Errors.Warnings.MandatoryBehindOptionalParam, "$" + p.Name.Name.Value));
                    }
                }
                else
                {
                    foundopt = true;
                }
            }
        }

        /// <summary>
        /// Constructs routine source parameters.
        /// </summary>
        protected IEnumerable<SourceParameterSymbol> BuildSrcParams(IEnumerable<FormalParam> formalparams, PHPDocBlock phpdocOpt = null)
        {
            var pindex = 0; // zero-based relative index

            foreach (var p in formalparams)
            {
                var ptag = (phpdocOpt != null) ? PHPDoc.GetParamTag(phpdocOpt, pindex, p.Name.Name.Value) : null;

                yield return new SourceParameterSymbol(this, p, relindex: pindex++, ptagOpt: ptag);
            }
        }

        protected virtual IEnumerable<SourceParameterSymbol> BuildSrcParams(Signature signature, PHPDocBlock phpdocOpt = null)
        {
            return BuildSrcParams(signature.FormalParams, phpdocOpt);
        }

        internal virtual ImmutableArray<ParameterSymbol> ImplicitParameters
        {
            get
            {
                if (_implicitParameters.IsDefault)
                {
                    ImmutableInterlocked.InterlockedInitialize(ref _implicitParameters, BuildImplicitParams().ToImmutableArray());
                }

                var currentImplicitParameters = _implicitParameters;
                if (RequiresLateStaticBoundParam && !currentImplicitParameters.Any(SpecialParameterSymbol.IsLateStaticParameter))
                {
                    // PhpTypeInfo <static>
                    var implicitParameters = currentImplicitParameters.Add(
                        new SpecialParameterSymbol(this, DeclaringCompilation.CoreTypes.PhpTypeInfo, SpecialParameterSymbol.StaticTypeName, currentImplicitParameters.Length));
                    ImmutableInterlocked.InterlockedCompareExchange(ref _implicitParameters, implicitParameters, currentImplicitParameters);
                }

                //
                return _implicitParameters;
            }
        }

        internal SourceParameterSymbol[] SourceParameters
        {
            get
            {
                if (_srcParams == null)
                {
                    var srcParams = BuildSrcParams(this.SyntaxSignature, this.PHPDocBlock).ToArray();
                    Interlocked.CompareExchange(ref _srcParams, srcParams, null);
                }

                return _srcParams;
            }
        }

        SourceParameterSymbol SourceVarargsParam
        {
            get
            {
                var srcparams = this.SourceParameters;
                if (srcparams.Length != 0)
                {
                    var last = srcparams.Last();
                    if (last.IsParams)
                    {
                        return last;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Implicitly added parameter corresponding to <c>params PhpValue[] {arguments}</c>. Replaces all the optional parameters.
        /// !!IMPORTANT!! Its <see cref="ParameterSymbol.Ordinal"/> specifies its position - all the source parameters with the same or higher ordinal are ignored.
        /// Can be <c>null</c> if not needed.
        /// </summary>
        protected ParameterSymbol VarargsParam
        {
            get
            {
                // declare implicit [... varargs] parameter if needed and not defined as source parameter
                if ((Flags & RoutineFlags.RequiresVarArg) != 0 && !IsGlobalScope)
                {
                    if (_implicitVarArg == null)
                    {
                        var srcparams = SourceVarargsParam;

                        // is there is params (...) already and no optional parameters, we can stick with it
                        if (srcparams != null && SourceParameters.All(p => p.Initializer == null))
                        {
                            return null;
                        }

                        // create implicit [... params]
                        var implicitVarArg = new SynthesizedParameterSymbol( // IsImplicitlyDeclared, IsParams
                            this,
                            ArrayTypeSymbol.CreateSZArray(this.ContainingAssembly, this.DeclaringCompilation.CoreTypes.PhpValue),
                            0,
                            RefKind.None,
                            SpecialParameterSymbol.ParamsName, isParams: true);
                        Interlocked.CompareExchange(ref _implicitVarArg, implicitVarArg, null);
                    }
                }

                if (_implicitVarArg != null)
                {
                    // implicit params replaces all the optional arguments!!
                    int mandatory = ImplicitParameters.Length + this.SourceParameters.TakeWhile(p => p.Initializer == null).Count();
                    _implicitVarArg.UpdateOrdinal(mandatory);
                }

                return _implicitVarArg;
            }
        }

        /// <summary>
        /// Gets params parameter or null.
        /// </summary>
        internal ParameterSymbol GetParamsParameter()
        {
            var p = VarargsParam ?? SourceVarargsParam;
            Debug.Assert(p == null || p.Type.IsSZArray());

            return p;
        }

        public override bool IsExtern => false;

        public override bool IsOverride => false;

        public override bool IsVirtual => !IsSealed && !IsStatic;

        public override bool CastToFalse => false;  // source routines never cast special values to FALSE

        public override MethodKind MethodKind
        {
            get
            {
                // TODO: ctor, dtor, props, magic, ...

                return MethodKind.Ordinary;
            }
        }

        public sealed override ImmutableArray<ParameterSymbol> Parameters
        {
            get
            {
                // [implicit parameters], [source parameters], [...varargs]

                var srcparams = SourceParameters;
                var implicitVarArgs = VarargsParam;

                var result = new List<ParameterSymbol>(ImplicitParameters.Length + srcparams.Length);

                result.AddRange(ImplicitParameters);

                if (implicitVarArgs == null)
                {
                    result.AddRange(srcparams);
                }
                else
                {
                    // implicitVarArgs replaces optional srcparams
                    for (int i = 0; i < srcparams.Length && srcparams[i].Ordinal < implicitVarArgs.Ordinal; i++)
                    {
                        result.Add(srcparams[i]);
                    }

                    result.Add(implicitVarArgs);
                }

                return result.AsImmutableOrEmpty();
            }
        }

        public sealed override int ParameterCount
        {
            get
            {
                // [implicit parameters], [source parameters], [...varargs]

                var implicitVarArgs = VarargsParam;
                if (implicitVarArgs != null)
                {
                    return implicitVarArgs.Ordinal + 1;
                }
                else
                {
                    return ImplicitParameters.Length + SourceParameters.Length;
                }
            }
        }

        public override bool ReturnsVoid => ReturnType.SpecialType == SpecialType.System_Void;

        public override RefKind RefKind => RefKind.None;

        public override TypeSymbol ReturnType => PhpRoutineSymbolExtensions.ConstructClrReturnType(this);

        public override ImmutableArray<AttributeData> GetAttributes()
        {
            // attributes from syntax node
            if (this.Syntax.TryGetCustomAttributes(out var attrs))
            {
                // initialize attribute data if necessary:
                attrs
                    .OfType<SourceCustomAttribute>()
                    .ForEach(x => x.Bind(this, this.ContainingFile));
            }
            else
            {
                attrs = ImmutableArray<AttributeData>.Empty;
            }

            // attributes from PHPDoc
            // ...

            //
            return base.GetAttributes().AddRange(attrs);
        }

        internal override ObsoleteAttributeData ObsoleteAttributeData => null;   // TODO: from PHPDoc

        /// <summary>
        /// virtual = IsVirtual AND NewSlot 
        /// override = IsVirtual AND !NewSlot
        /// </summary>
        internal override bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false) => !IsOverride && IsMetadataVirtual(ignoreInterfaceImplementationChanges);

        internal override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => IsVirtual && (!ContainingType.IsSealed || IsOverride || IsAbstract); // do not make method virtual if not necessary

        public override string GetDocumentationCommentXml(CultureInfo preferredCulture = null, bool expandIncludes = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO: XmlDocumentationCommentCompiler
            return this.PHPDocBlock?.Summary ?? string.Empty;
        }
    }
}
