﻿using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pchp.CodeAnalysis.Emit;
using System.Reflection.Metadata;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;

namespace Pchp.CodeAnalysis.Symbols
{
    partial class SourceRoutineSymbol
    {
        /// <summary>
        /// Gets place referring to <c>Pchp.Core.Context</c> object.
        /// </summary>
        internal virtual IPlace GetContextPlace(PEModuleBuilder module)
        {
            var ps = ImplicitParameters;
            if (ps.Length != 0 && SpecialParameterSymbol.IsContextParameter(ps[0]))
            {
                return new ParamPlace(ps[0]);  // <ctx>
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets place of <c>this</c> parameter in CLR corresponding to <c>current class instance</c>.
        /// </summary>
        internal virtual IPlace GetThisPlace()
        {
            return this.HasThis
                ? new ArgPlace(ContainingType, 0)
                : null;
        }

        /// <summary>
        /// Gets place of PHP <c>$this</c> variable.
        /// </summary>
        public virtual IPlace GetPhpThisVariablePlace(PEModuleBuilder module = null)
        {
            var thisPlace = GetThisPlace();
            if (thisPlace != null)
            {
                if (this.IsGeneratorMethod())
                {
                    // $this ~ arg1
                    thisPlace = new ArgPlace(thisPlace.Type, 1);
                }
                else if (this.ContainingType.IsTraitType())
                {
                    // $this ~ this.<>this
                    thisPlace = new FieldPlace(thisPlace, ((SourceTraitTypeSymbol)this.ContainingType).RealThisField, module);
                }

                //
                return thisPlace;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Creates ghost stubs,
        /// i.e. methods with a different signature calling this routine to comply with CLR standards.
        /// </summary>
        /// <returns>List of additional overloads.</returns>
        internal virtual IList<MethodSymbol> SynthesizeStubs(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            // TODO: resolve this already in SourceTypeSymbol.GetMembers(), now it does not get overloaded properly
            return SynthesizeOverloadsWithOptionalParameters(module, diagnostic);
        }

        /// <summary>
        /// Synthesizes method overloads in case there are optional parameters which explicit default value cannot be resolved as a <see cref="ConstantValue"/>.
        /// </summary>
        /// <remarks>
        /// foo($a = [], $b = [1, 2, 3])
        /// + foo() => foo([], [1, 2, 3)
        /// + foo($a) => foo($a, [1, 2, 3])
        /// </remarks>
        protected IList<MethodSymbol> SynthesizeOverloadsWithOptionalParameters(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            List<MethodSymbol> list = null;

            var ps = this.Parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i] is IPhpValue p && p.Initializer != null && ps[i].ExplicitDefaultConstantValue == null)   // => ConstantValue couldn't be resolved for optional parameter
                {
                    if (list == null)
                    {
                        list = new List<MethodSymbol>();
                    }

                    if (this.ContainingType.IsInterface)
                    {
                        // TODO: we can't build instance method in an interface
                        // - generate static extension method ?
                        // - annotate parameter with attribute and the initializer value?
                        //   ? [Optional(EmptyArray)]
                        //   ? [Optional(array(1,2,3))]
                        Debug.WriteLine($"we've lost parameter explicit default value {this.ContainingType.Name}::{this.RoutineName}, parameter ${ps[i].Name}");
                    }
                    else
                    {
                        // create ghost stub foo(p0, .. pi-1) => foo(p0, .. , pN)
                        list.Add(GhostMethodBuilder.CreateGhostOverload(this, module, diagnostic, i));
                    }
                }
            }

            return list ?? (IList<MethodSymbol>)Array.Empty<MethodSymbol>();
        }

        public virtual void Generate(CodeGenerator cg)
        {
            if (!this.IsGeneratorMethod())
            {
                //Proceed with normal method generation
                cg.GenerateScope(this.ControlFlowGraph.Start, int.MaxValue);
            }
            else
            {
                var genSymbol = new SourceGeneratorSymbol(this);
                var il = cg.Builder;

                /* Template:
                 * return BuildGenerator( <ctx>, this, new PhpArray(){ p1, p2, ... }, new GeneratorStateMachineDelegate((IntPtr)<genSymbol>), (RuntimeMethodHandle)this )
                 */

                cg.EmitLoadContext(); // ctx for generator
                cg.EmitThisOrNull();  // @this for generator

                // new PhpArray for generator's locals
                cg.EmitCall(ILOpCode.Newobj, cg.CoreMethods.Ctors.PhpArray);

                var generatorsLocals = cg.GetTemporaryLocal(cg.CoreTypes.PhpArray);
                cg.Builder.EmitLocalStore(generatorsLocals);

                // initialize parameters (set their _isOptimized and copy them to locals array)
                InitializeParametersForGeneratorMethod(cg, il, generatorsLocals);
                cg.Builder.EmitLoad(generatorsLocals);
                cg.ReturnTemporaryLocal(generatorsLocals);

                // new PhpArray for generator's synthesizedLocals
                cg.EmitCall(ILOpCode.Newobj, cg.CoreMethods.Ctors.PhpArray);

                // new GeneratorStateMachineDelegate(<genSymbol>) delegate for generator
                cg.Builder.EmitNullConstant(); // null
                cg.EmitOpCode(ILOpCode.Ldftn); // method
                cg.EmitSymbolToken(genSymbol, null);
                cg.EmitCall(ILOpCode.Newobj, cg.CoreTypes.GeneratorStateMachineDelegate.Ctor(cg.CoreTypes.Object, cg.CoreTypes.IntPtr)); // GeneratorStateMachineDelegate(object @object, IntPtr method)

                // handleof(this)
                cg.EmitLoadToken(this, null);

                // create generator object via Operators factory method
                cg.EmitCall(ILOpCode.Call, cg.CoreMethods.Operators.BuildGenerator_Context_Object_PhpArray_PhpArray_GeneratorStateMachineDelegate_RuntimeMethodHandle);

                // Convert to return type (Generator or PhpValue, depends on analysis)
                cg.EmitConvert(cg.CoreTypes.Generator, 0, this.ReturnType);
                il.EmitRet(false);

                // Generate SM method. Must be generated after EmitInit of parameters (it sets their _isUnoptimized field).
                CreateStateMachineNextMethod(cg, genSymbol);
            }
        }

        private void InitializeParametersForGeneratorMethod(CodeGenerator cg, Microsoft.CodeAnalysis.CodeGen.ILBuilder il, Microsoft.CodeAnalysis.CodeGen.LocalDefinition generatorsLocals)
        {
            // Emit init of unoptimized BoundParameters using separate CodeGenerator that has locals place pointing to our generator's locals array
            using (var localsArrayCg = new CodeGenerator(
                il, cg.Module, cg.Diagnostics,
                cg.DeclaringCompilation.Options.OptimizationLevel,
                cg.EmitPdbSequencePoints,
                this.ContainingType,
                contextPlace: null,
                thisPlace: null,
                routine: this, // needed to support static variables (they need enclosing routine while binding)
                locals: new LocalPlace(generatorsLocals),
                localsInitialized: false
                    ))
            {
                // EmitInit (for UnoptimizedLocals) copies arguments to locals array, does nothing for normal variables, handles local statics, global variables ...
                LocalsTable.Variables.ForEach(v => v.EmitInit(localsArrayCg));
            }
        }

        private void CreateStateMachineNextMethod(CodeGenerator cg, SourceGeneratorSymbol genSymbol)
        {
            cg.Module.SynthesizedManager.AddMethod(ContainingType, genSymbol); // save method symbol to module

            // generate generator's next method body
            var genMethodBody = MethodGenerator.GenerateMethodBody(cg.Module, genSymbol, (_il) =>
            {
                GenerateStateMachinesNextMethod(cg, _il, genSymbol);
            }
            , null, cg.Diagnostics, cg.EmitPdbSequencePoints);

            cg.Module.SetMethodBody(genSymbol, genMethodBody);
        }

        //Initialized a new CodeGenerator for generation of SourceGeneratorSymbol (state machine's next method)
        private void GenerateStateMachinesNextMethod(CodeGenerator cg, Microsoft.CodeAnalysis.CodeGen.ILBuilder _il, SourceGeneratorSymbol genSymbol)
        {
            // TODO: get correct ThisPlace, ReturnType etc. resolution & binding out of the box without GN_SGS hacks
            // using SourceGeneratorSymbol

            //Refactor parameters references to proper fields
            using (var stateMachineNextCg = new CodeGenerator(
                _il, cg.Module, cg.Diagnostics,
                cg.DeclaringCompilation.Options.OptimizationLevel,
                cg.EmitPdbSequencePoints,
                this.ContainingType,
                contextPlace: new ParamPlace(genSymbol.ContextParameter),
                thisPlace: new ParamPlace(genSymbol.ThisParameter),
                routine: this,
                locals: new ParamPlace(genSymbol.LocalsParameter),
                localsInitialized: true,
                tempLocals: new ParamPlace(genSymbol.TmpLocalsParameter)
                    )
            {
                GeneratorStateMachineMethod = genSymbol,    // Pass SourceGeneratorSymbol to CG for additional yield and StartBlock emit 
            })
            {
                stateMachineNextCg.GenerateScope(this.ControlFlowGraph.Start, int.MaxValue);
            }
        }
    }

    partial class SourceGlobalMethodSymbol
    {
        /// <summary>
        /// Real main method with <c>MainDelegate</c> signature.
        /// The method is generated lazily in order to provide method compatible with MainDelegate.
        /// <see cref="SourceGlobalMethodSymbol"/> may have (usually have) a different return type.
        /// </summary>
        internal SynthesizedMethodSymbol _mainMethod0;

        internal override IPlace GetThisPlace() => new ParamPlace(ThisParameter);

        internal override IList<MethodSymbol> SynthesizeStubs(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            //base.SynthesizeStubs(module, diagnostic);          // ghosts (always empty)

            // <Main>'0
            this.SynthesizeMainMethodWrapper(module, diagnostic);

            // no overloads synthesized for global code
            return Array.Empty<MethodSymbol>();
        }

        /// <summary>
        /// Main method wrapper in case it does not return PhpValue.
        /// </summary>
        void SynthesizeMainMethodWrapper(PEModuleBuilder module, DiagnosticBag diagnostics)
        {
            if (this.ReturnType != DeclaringCompilation.CoreTypes.PhpValue)
            {
                // PhpValue <Main>`0(parameters)
                var wrapper = new SynthesizedMethodSymbol(
                    this.ContainingFile, WellKnownPchpNames.GlobalRoutineName + "`0", true, false,
                    DeclaringCompilation.CoreTypes.PhpValue, Accessibility.Public);

                wrapper.SetParameters(this.Parameters.Select(p =>
                    new SynthesizedParameterSymbol(wrapper, p.Type, p.Ordinal, p.RefKind, p.Name)).ToArray());

                // save method symbol to module
                module.SynthesizedManager.AddMethod(this.ContainingFile, wrapper);

                // generate method body
                module.CreateMainMethodWrapper(wrapper, this, diagnostics);

                //
                _mainMethod0 = wrapper;
            }
        }
    }

    partial class SourceMethodSymbol
    {
        internal override IPlace GetContextPlace(PEModuleBuilder module)
        {
            var thisplace = GetThisPlace();
            if (thisplace != null)
            {
                // <this>.<ctx> in instance methods
                var t = (SourceTypeSymbol)this.ContainingType;

                var ctx_field = t.ContextStore;
                if (ctx_field != null)  // might be null in interfaces
                {
                    return new FieldPlace(thisplace, ctx_field, module);
                }
                else
                {
                    Debug.Assert(t.IsInterface);
                    return null;
                }
            }

            //
            return base.GetContextPlace(module);
        }

        internal override IList<MethodSymbol> SynthesizeStubs(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            // empty body for static abstract
            if (this.ControlFlowGraph == null && this.IsStatic)
            {
                SynthesizeEmptyBody(module, diagnostic);
            }

            //
            return base.SynthesizeStubs(module, diagnostic);
        }

        void SynthesizeEmptyBody(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            Debug.Assert(this.ControlFlowGraph == null);
            Debug.Assert(this.IsAbstract == false);

            module.SetMethodBody(this, MethodGenerator.GenerateMethodBody(module, this, (il) =>
            {
                var cg = new CodeGenerator(this, il, module, diagnostic, module.Compilation.Options.OptimizationLevel, false);

                // Template: return default(T)
                cg.EmitRetDefault();
            }, null, diagnostic, false));
        }
    }

    partial class SourceFunctionSymbol
    {
        internal override IList<MethodSymbol> SynthesizeStubs(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            var overloads = base.SynthesizeStubs(module, diagnostic);

            // synthesize RoutineInfo:
            var cctor = module.GetStaticCtorBuilder(_file);
            lock (cctor)
            {
                using (var cg = new CodeGenerator(
                        cctor, module, diagnostic, OptimizationLevel.Release, false, this.ContainingType,
                        contextPlace: null, thisPlace: null, routine: this))
                {
                    var field = new FieldPlace(null, this.EnsureRoutineInfoField(module), module);

                    // {RoutineInfoField} = RoutineInfo.CreateUserRoutine(name, handle, overloads[])
                    field.EmitStorePrepare(cctor);

                    cctor.EmitStringConstant(this.QualifiedName.ToString());
                    cctor.EmitLoadToken(module, DiagnosticBag.GetInstance(), this, null);
                    cg.Emit_NewArray(cg.CoreTypes.RuntimeMethodHandle, overloads.AsImmutable(), m => cg.EmitLoadToken(m, null));

                    cctor.EmitCall(module, DiagnosticBag.GetInstance(), ILOpCode.Call, cg.CoreMethods.Reflection.CreateUserRoutine_string_RuntimeMethodHandle_RuntimeMethodHandleArr);

                    field.EmitStore(cctor);
                }
            }

            //
            return overloads;
        }
    }

    partial class SourceLambdaSymbol
    {
        internal override IPlace GetContextPlace(PEModuleBuilder module)
        {
            // Template: Operators.Context(<closure>)
            return new OperatorPlace(DeclaringCompilation.CoreMethods.Operators.Context_Closure, new ParamPlace(ClosureParameter));
        }

        public override IPlace GetPhpThisVariablePlace(PEModuleBuilder module = null)
        {
            if (UseThis)
            {
                // Template: Operators.This(<closure>)
                return new OperatorPlace(DeclaringCompilation.CoreMethods.Operators.This_Closure, new ParamPlace(ClosureParameter));
            }
            else
            {
                return null;
            }
        }

        internal IPlace GetCallerTypePlace()
        {
            // Template: Operators.Scope(<closure>)
            return new OperatorPlace(DeclaringCompilation.CoreMethods.Operators.Scope_Closure, new ParamPlace(ClosureParameter));
        }

        internal override IList<MethodSymbol> SynthesizeStubs(PEModuleBuilder module, DiagnosticBag diagnostic)
        {
            var overloads = base.SynthesizeStubs(module, diagnostic);

            // synthesize RoutineInfo:
            var cctor = module.GetStaticCtorBuilder(_container);
            lock (cctor)
            {
                var field = new FieldPlace(null, this.EnsureRoutineInfoField(module), module);

                var ct = module.Compilation.CoreTypes;

                // {RoutineInfoField} = new PhpAnonymousRoutineInfo(name, handle)
                field.EmitStorePrepare(cctor);

                cctor.EmitStringConstant(this.MetadataName);
                cctor.EmitLoadToken(module, DiagnosticBag.GetInstance(), this, null);
                cctor.EmitCall(module, DiagnosticBag.GetInstance(), ILOpCode.Call, ct.Operators.Method("AnonymousRoutine", ct.String, ct.RuntimeMethodHandle));

                field.EmitStore(cctor);
            }

            //
            return overloads;
        }
    }

    partial class SourceGeneratorSymbol
    {
        internal void EmitInit(PEModuleBuilder module)
        {
            // Don't need any initial emit
        }
    }
};