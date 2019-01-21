﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.PooledObjects;
using Pchp.CodeAnalysis.CodeGen;
using Pchp.CodeAnalysis.Symbols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Pchp.CodeAnalysis.Semantics.Graph
{
    partial class Edge : IGenerator
    {
        /// <summary>
        /// Generates or enqueues next blocks to the worklist.
        /// </summary>
        internal abstract void Generate(CodeGenerator cg);

        void IGenerator.Generate(CodeGenerator cg) => this.Generate(cg);
    }

    partial class SimpleEdge
    {
        internal override void Generate(CodeGenerator cg)
        {
            if (cg.IsDebug && this.PhpSyntax != null)
            {
                cg.EmitSequencePoint(this.PhpSyntax);
            }
            cg.Scope.ContinueWith(NextBlock);
        }
    }

    partial class LeaveEdge
    {
        internal override void Generate(CodeGenerator cg)
        {
            // nop
        }
    }

    partial class ConditionalEdge
    {
        internal override void Generate(CodeGenerator cg)
        {
            Contract.ThrowIfNull(Condition);

            // if the cndition was resolved in compile-time,
            // generate only the positive branch
            if (!cg.IsDebug && this.Condition.ConstantValue.TryConvertToBool(out bool evaluated))
            {
                cg.Scope.ContinueWith(evaluated ? TrueTarget : FalseTarget);
                return;
            }

            //

            if (IsLoop) // perf
            {
                cg.Builder.DefineHiddenSequencePoint();
                cg.Builder.EmitBranch(ILOpCode.Br, this.Condition);

                // {
                cg.GenerateScope(TrueTarget, NextBlock.Ordinal);
                // }

                // if (Condition)
                cg.EmitSequencePoint(this.Condition.PhpSyntax);
                cg.Builder.MarkLabel(this.Condition);
                cg.EmitConvert(this.Condition, cg.CoreTypes.Boolean);
                cg.Builder.EmitBranch(ILOpCode.Brtrue, TrueTarget);
            }
            else
            {
                // if (Condition)
                cg.EmitSequencePoint(this.Condition.PhpSyntax);
                cg.EmitConvert(this.Condition, cg.CoreTypes.Boolean);
                cg.Builder.EmitBranch(ILOpCode.Brfalse, FalseTarget);

                // {
                cg.GenerateScope(TrueTarget, NextBlock.Ordinal);
                // }
            }

            cg.Scope.ContinueWith(FalseTarget);
        }
    }

    partial class TryCatchEdge
    {
        internal override void Generate(CodeGenerator cg)
        {
            EmitTryStatement(cg);

            //
            cg.Scope.ContinueWith(NextBlock);
        }

        void EmitTryStatement(CodeGenerator cg, bool emitCatchesOnly = false)
        {
            // Stack must be empty at beginning of try block.
            cg.Builder.AssertStackEmpty();

            // IL requires catches and finally block to be distinct try
            // blocks so if the source contained both a catch and
            // a finally, nested scopes are emitted.
            bool emitNestedScopes = (!emitCatchesOnly &&
                //(_catchBlocks.Length != 0) &&
                (_finallyBlock != null));

            cg.Builder.OpenLocalScope(ScopeType.TryCatchFinally);

            cg.Builder.OpenLocalScope(ScopeType.Try);
            // IL requires catches and finally block to be distinct try
            // blocks so if the source contained both a catch and
            // a finally, nested scopes are emitted.

            //_tryNestingLevel++;
            if (emitNestedScopes)
            {
                EmitTryStatement(cg, emitCatchesOnly: true);
            }
            else
            {
                cg.GenerateScope(_body, (_finallyBlock ?? NextBlock).Ordinal);

                if (NextBlock?.FlowState != null)
                {
                    cg.Builder.EmitBranch(ILOpCode.Br, NextBlock);
                }
            }

            //_tryNestingLevel--;
            // Close the Try scope
            cg.Builder.CloseLocalScope();

            if (!emitNestedScopes)
            {
                EmitScriptDiedBlock(cg);

                //
                foreach (var catchBlock in _catchBlocks)
                {
                    EmitCatchBlock(cg, catchBlock);
                }
            }

            if (!emitCatchesOnly && _finallyBlock != null)
            {
                cg.Builder.OpenLocalScope(ScopeType.Finally);
                cg.GenerateScope(_finallyBlock, NextBlock.Ordinal);

                // close Finally scope
                cg.Builder.CloseLocalScope();
            }

            // close the whole try statement scope
            cg.Builder.CloseLocalScope();
        }

        void EmitScriptDiedBlock(CodeGenerator cg)
        {
            // handle ScriptDiedException (caused by die or exit) separately and rethrow the exception

            var il = cg.Builder;

            // Template: catch (ScriptDiedException) { rethrow; }

            il.OpenLocalScope(ScopeType.Catch, cg.CoreTypes.ScriptDiedException.Symbol);
            il.EmitThrow(true);
            il.CloseLocalScope();
        }

        void EmitTypeCheck(CodeGenerator cg, BoundTypeRef tref)
        {
            var il = cg.Builder;

            // STACK : object

            if (tref.ResolvedType.IsErrorTypeOrNull())
            {
                // Template: filter(Operators.IsInstanceOf(<stack>, type))
                tref.EmitLoadTypeInfo(cg, false);
                cg.EmitCall(ILOpCode.Call, cg.CoreMethods.Operators.IsInstanceOf_Object_PhpTypeInfo)
                    .Expect(SpecialType.System_Boolean);
            }
            else
            {
                // Template: filter (<stack> is Interface)
                il.EmitOpCode(ILOpCode.Isinst);
                cg.EmitSymbolToken(tref.ResolvedType, null);
                il.EmitNullConstant();
                il.EmitOpCode(ILOpCode.Cgt_un); // value > null : bool
            }

            // STACK: i4 (boolean)
        }

        void EmitMultipleTypeCheck(CodeGenerator cg, ImmutableArray<BoundTypeRef> trefs)
        {
            var il = cg.Builder;

            // STACK : object

            var lblFound = new NamedLabel("filter_found");
            var lblEnd = new NamedLabel("filter_end");

            for (int i = 0; i < trefs.Length; i++)
            {
                il.EmitOpCode(ILOpCode.Dup);

                // (ex is T) : bool
                EmitTypeCheck(cg, trefs[i]);

                // if (STACK) goto lblFound;
                il.EmitBranch(ILOpCode.Brtrue, lblFound);
            }

            il.EmitOpCode(ILOpCode.Pop);    // POP object
            il.EmitBoolConstant(false);
            il.EmitBranch(ILOpCode.Br, lblEnd);

            il.MarkLabel(lblFound);
            il.EmitOpCode(ILOpCode.Pop);    // POP object
            il.EmitBoolConstant(true);

            il.MarkLabel(lblEnd);

            // STACK: i4 (boolean)
        }

        void EmitCatchBlock(CodeGenerator cg, CatchBlock catchBlock)
        {
            Debug.Assert(catchBlock.Variable.Variable != null);

            var il = cg.Builder;
            TypeSymbol extype;

            il.AdjustStack(1); // Account for exception on the stack.

            // set of types we catch in this catch block
            var trefs = catchBlock.TypeRef is TypeRef.BoundMultipleTypeRef mt
                ? mt.TypeRefs
                : ImmutableArray.Create((BoundTypeRef)catchBlock.TypeRef);

            // do we have to generate .filter or just .catch<type>:
            if (trefs.Length != 1 || trefs[0].ResolvedType.IsErrorTypeOrNull() || !trefs[0].ResolvedType.IsOfType(cg.CoreTypes.Exception))
            {
                // Template: catch when
                il.OpenLocalScope(ScopeType.Filter);

                // STACK : object

                if (trefs.Length == 1)
                {
                    EmitTypeCheck(cg, trefs[0]);

                    extype = trefs[0].ResolvedType.IsErrorTypeOrNull()
                        ? cg.CoreTypes.Object.Symbol
                        : trefs[0].ResolvedType;
                }
                else
                {
                    EmitMultipleTypeCheck(cg, trefs);

                    extype = cg.CoreTypes.Object.Symbol;
                }

                // STACK : i4 ? handle : continue

                il.MarkFilterConditionEnd();

                // STACK : object
                cg.EmitCastClass(cg.CoreTypes.Exception);   // has to be casted to System.Exception in order to generate valid IL
                cg.EmitCastClass(extype);
            }
            else
            {
                // Template: catch (TypeRef)
                extype = trefs[0].ResolvedType;
                il.OpenLocalScope(ScopeType.Catch, cg.Module.Translate(extype, null, cg.Diagnostics));
            }

            // STACK : extype

            // <tmp> = <ex>
            cg.EmitSequencePoint(catchBlock.Variable.PhpSyntax);
            var tmploc = cg.GetTemporaryLocal(extype);
            il.EmitLocalStore(tmploc);

            var varplace = catchBlock.Variable.BindPlace(cg);
            Debug.Assert(varplace != null);

            // $x = <tmp>
            varplace.EmitStore(cg, tmploc, BoundAccess.Write);

            //
            cg.ReturnTemporaryLocal(tmploc);
            tmploc = null;

            //
            cg.GenerateScope(catchBlock, NextBlock.Ordinal);

            //
            il.CloseLocalScope();
        }
    }

    partial class ForeachEnumereeEdge
    {
        CodeGenerator.TemporaryLocalDefinition _enumeratorLoc;
        MethodSymbol _moveNextMethod, _disposeMethod;
        PropertySymbol _currentValue, _currentKey, _current;

        static ILOpCode CallOpCode(MethodSymbol method, TypeSymbol declaringtype)
        {
            return method.IsMetadataVirtual() ? ILOpCode.Callvirt : ILOpCode.Call;
        }

        internal void EmitMoveNext(CodeGenerator cg)
        {
            Debug.Assert(_enumeratorLoc.IsValid);
            Debug.Assert(_moveNextMethod != null);
            Debug.Assert(_moveNextMethod.IsStatic == false);

            if (_enumeratorLoc.Type.IsValueType)
            {
                // <locaddr>.MoveNext()
                _enumeratorLoc.EmitLoadAddress(cg.Builder);
            }
            else
            {
                // <loc>.MoveNext()
                _enumeratorLoc.EmitLoad(cg.Builder);
            }

            cg.EmitCall(CallOpCode(_moveNextMethod, _enumeratorLoc.Type), _moveNextMethod)
                .Expect(SpecialType.System_Boolean);
        }

        internal void EmitGetCurrent(CodeGenerator cg, BoundReferenceExpression valueVar, BoundReferenceExpression keyVar)
        {
            Debug.Assert(_enumeratorLoc.IsValid);

            // NOTE: PHP writes first to {valueVar} then to {keyVar}

            if (_currentValue != null && _currentKey != null)
            {
                // special PhpArray enumerator

                cg.EmitSequencePoint(valueVar.PhpSyntax);
                valueVar.BindPlace(cg).EmitStore(cg, new PropertyPlace(_enumeratorLoc, _currentValue), valueVar.Access);

                if (keyVar != null)
                {
                    cg.EmitSequencePoint(keyVar.PhpSyntax);
                    keyVar.BindPlace(cg).EmitStore(cg, new PropertyPlace(_enumeratorLoc, _currentKey), keyVar.Access);
                }
            }
            else
            {
                Debug.Assert(_current != null);
                Debug.Assert(_current.GetMethod != null);

                var valuetype = _current.GetMethod.ReturnType;

                // ValueTuple (key, value)
                // TODO: KeyValuePair<key, value> // the same
                if (valuetype.Name == "ValueTuple" && valuetype.IsValueType && ((NamedTypeSymbol)valuetype).Arity == 2)
                {
                    // tmp = current;
                    var tmp = cg.GetTemporaryLocal(valuetype);
                    cg.EmitGetProperty(_enumeratorLoc, _current);
                    cg.Builder.EmitLocalStore(tmp);

                    // TODO: ValueTuple Helper
                    var item1 = valuetype.GetMembers("Item1").Single() as FieldSymbol;
                    var item2 = valuetype.GetMembers("Item2").Single() as FieldSymbol;

                    var item1place = new FieldPlace(new LocalPlace(tmp), item1, cg.Module);
                    var item2place = new FieldPlace(new LocalPlace(tmp), item2, cg.Module);

                    // value = tmp.Item2;
                    cg.EmitSequencePoint(valueVar.PhpSyntax);
                    valueVar.BindPlace(cg).EmitStore(cg, item2place, valueVar.Access);

                    // key = tmp.Item1;
                    if (keyVar != null)
                    {
                        cg.EmitSequencePoint(keyVar.PhpSyntax);
                        keyVar.BindPlace(cg).EmitStore(cg, item1place, keyVar.Access);
                    }

                    //
                    cg.ReturnTemporaryLocal(tmp);
                }
                // just a value
                else
                {
                    cg.EmitSequencePoint(valueVar.PhpSyntax);
                    valueVar.BindPlace(cg).EmitStore(cg, new PropertyPlace(_enumeratorLoc, _current), valueVar.Access);  // TOOD: PhpValue.FromClr

                    if (keyVar != null)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        void EmitDisposeAndClean(CodeGenerator cg)
        {
            // enumerator.Dispose()
            if (_disposeMethod != null)
            {
                // TODO: if (enumerator != null)

                if (_enumeratorLoc.Type.IsValueType)
                {
                    _enumeratorLoc.EmitLoadAddress(cg.Builder);
                }
                else
                {
                    _enumeratorLoc.EmitLoad(cg.Builder);
                }

                cg.EmitCall(CallOpCode(_disposeMethod, (TypeSymbol)_enumeratorLoc.Type), _disposeMethod)
                    .Expect(SpecialType.System_Void);
            }

            //// enumerator = null;
            //if (!_enumeratorLoc.Type.IsValueType)
            //{
            //    cg.Builder.EmitNullConstant();
            //    cg.Builder.EmitLocalStore(_enumeratorLoc);
            //}

            //
            cg.ReturnTemporaryLocal(_enumeratorLoc);
            _enumeratorLoc = null;

            // unbind
            _moveNextMethod = null;
            _disposeMethod = null;
            _currentValue = null;
            _currentKey = null;
            _current = null;
        }

        internal override void Generate(CodeGenerator cg)
        {
            Debug.Assert(this.Enumeree != null);

            // get the enumerator,
            // bind actual MoveNext() and CurrentValue and CurrentKey

            // Template: using(
            // a) enumerator = enumeree.GetEnumerator()
            // b) enumerator = Operators.GetEnumerator(enumeree)
            // ) ...

            cg.EmitSequencePoint(this.Enumeree.PhpSyntax);

            var enumereeType = cg.Emit(this.Enumeree);
            Debug.Assert(enumereeType.SpecialType != SpecialType.System_Void);

            var getEnumeratorMethod = enumereeType.LookupMember<MethodSymbol>(WellKnownMemberNames.GetEnumeratorMethodName);

            TypeSymbol enumeratorType;

            if (enumereeType.IsOfType(cg.CoreTypes.PhpArray))
            {
                cg.Builder.EmitBoolConstant(_aliasedValues);

                // PhpArray.GetForeachtEnumerator(bool)
                enumeratorType = cg.EmitCall(ILOpCode.Callvirt, cg.CoreMethods.PhpArray.GetForeachEnumerator_Boolean);  // TODO: IPhpArray
            }
            else if (enumereeType.IsOfType(cg.CoreTypes.IPhpEnumerable))
            {
                var GetForeachEnumerator_Bool_RuntimeTypeHandle = cg.CoreTypes.IPhpEnumerable.Method("GetForeachEnumerator", cg.CoreTypes.Boolean, cg.CoreTypes.RuntimeTypeHandle);

                // enumeree.GetForeachEnumerator(bool aliasedValues, RuntimeTypeHandle caller)
                cg.Builder.EmitBoolConstant(_aliasedValues);
                cg.EmitCallerTypeHandle();
                enumeratorType = cg.EmitCall(ILOpCode.Callvirt, GetForeachEnumerator_Bool_RuntimeTypeHandle);
            }
            else if (enumereeType.IsOfType(cg.CoreTypes.Iterator))
            {
                enumeratorType = cg.EmitCall(ILOpCode.Call, cg.CoreMethods.Operators.GetForeachEnumerator_Iterator);
            }
            // TODO: IPhpArray
            else if (getEnumeratorMethod != null && getEnumeratorMethod.ParameterCount == 0 && enumereeType.IsReferenceType)
            {
                // enumeree.GetEnumerator()
                enumeratorType = cg.EmitCall(CallOpCode(getEnumeratorMethod, enumereeType), getEnumeratorMethod);
            }
            else
            {
                cg.EmitConvertToPhpValue(enumereeType, 0);
                cg.Builder.EmitBoolConstant(_aliasedValues);
                cg.EmitCallerTypeHandle();

                // Operators.GetForeachEnumerator(PhpValue, bool, RuntimeTypeHandle)
                enumeratorType = cg.EmitCall(ILOpCode.Call, cg.CoreMethods.Operators.GetForeachEnumerator_PhpValue_Bool_RuntimeTypeHandle);
            }

            //
            _current = enumeratorType.LookupMember<PropertySymbol>(WellKnownMemberNames.CurrentPropertyName);   // TODO: Err if no Current
            _currentValue = enumeratorType.LookupMember<PropertySymbol>(_aliasedValues ? "CurrentValueAliased" : "CurrentValue");
            _currentKey = enumeratorType.LookupMember<PropertySymbol>("CurrentKey");
            _disposeMethod = enumeratorType.LookupMember<MethodSymbol>("Dispose", m => m.ParameterCount == 0 && !m.IsStatic);

            //
            _enumeratorLoc = cg.GetTemporaryLocal(enumeratorType, longlive: true, immediateReturn: false);
            _enumeratorLoc.EmitStore();

            // bind methods
            _moveNextMethod = enumeratorType.LookupMember<MethodSymbol>(WellKnownMemberNames.MoveNextMethodName);    // TODO: Err if there is no MoveNext()
            Debug.Assert(_moveNextMethod.ReturnType.SpecialType == SpecialType.System_Boolean);
            Debug.Assert(_moveNextMethod.IsStatic == false);

            if (_disposeMethod != null
                && cg.GeneratorStateMachineMethod == null)  // Temporary workaround allowing "yield" inside foreach. Yield cannot be inside TRY block, so we don't generate TRY for state machines. Remove this condition once we manage to bind try/catch/yield somehow
            {
                /* Template: try { body } finally { enumerator.Dispose }
                 */

                // try {
                cg.Builder.AssertStackEmpty();
                cg.Builder.OpenLocalScope(ScopeType.TryCatchFinally);
                cg.Builder.OpenLocalScope(ScopeType.Try);

                //
                EmitBody(cg);

                // }
                cg.Builder.CloseLocalScope();   // /Try

                // finally {
                cg.Builder.OpenLocalScope(ScopeType.Finally);

                // enumerator.Dispose() & cleanup
                EmitDisposeAndClean(cg);

                // }
                cg.Builder.CloseLocalScope();   // /Finally
                cg.Builder.CloseLocalScope();   // /TryCatchFinally
            }
            else
            {
                EmitBody(cg);
                EmitDisposeAndClean(cg);
            }
        }

        void EmitBody(CodeGenerator cg)
        {
            Debug.Assert(NextBlock.NextEdge is ForeachMoveNextEdge);
            cg.GenerateScope(NextBlock, NextBlock.NextEdge.NextBlock.Ordinal);
            cg.Builder.EmitBranch(ILOpCode.Br, NextBlock.NextEdge.NextBlock);
        }
    }

    partial class ForeachMoveNextEdge
    {
        internal override void Generate(CodeGenerator cg)
        {
            /* Template:
             *  for (;MoveNext(enumerator);)
             *      $value = CurrentValue(enumerator);
             *      $key = CurrentKey(enumerator);
             *      {body}
             *  }
             */

            var lblMoveNext = new NamedLabel("MoveNext");
            var lblBody = new object();

            //
            cg.Builder.DefineHiddenSequencePoint();
            cg.Builder.EmitBranch(ILOpCode.Br, lblMoveNext);
            cg.Builder.MarkLabel(lblBody);

            // $value, $key
            this.EnumereeEdge.EmitGetCurrent(cg, this.ValueVariable, this.KeyVariable);

            // {
            cg.GenerateScope(this.BodyBlock, NextBlock.Ordinal);
            // }

            // if (enumerator.MoveNext())
            cg.EmitSequencePoint(_moveSpan);
            cg.Builder.MarkLabel(lblMoveNext);
            this.EnumereeEdge.EmitMoveNext(cg); // bool
            cg.Builder.EmitBranch(ILOpCode.Brtrue, lblBody);

            //
            cg.Scope.ContinueWith(NextBlock);
        }
    }

    partial class SwitchEdge
    {
        static bool IsInt32(object value) => value is int || (value is long && (long)value <= int.MaxValue && (long)value >= int.MinValue);
        static bool IsString(object value) => value is string;

        internal override void Generate(CodeGenerator cg)
        {
            // four cases:
            // 1. just single or none case label that can be replaced with single IF
            // 2. switch over integers, using native CIL switch
            // 3. switch over strings, using C# static Dictionary and CIL switch
            // 4. PHP style switch which is just a bunch of IFs

            if (this.CaseBlocks.Length == 0 || (this.CaseBlocks[0].IsDefault && this.CaseBlocks.Length == 1))
            {
                Debug.Assert(this.CaseBlocks.Length <= 1);

                // no SWITCH or IF needed

                cg.EmitPop(this.SwitchValue.WithAccess(BoundAccess.None).Emit(cg)); // None Access, also using BoundExpression.Emit directly to avoid CodeGenerator type specialization which is not needed
                if (this.CaseBlocks.Length != 0)
                {
                    cg.GenerateScope(this.CaseBlocks[0], NextBlock.Ordinal);
                }
            }
            else
            {
                // CIL Switch:
                bool allConst = this.CaseBlocks.All(c => c.IsDefault || (c.CaseValue.IsOnlyBoundElement && c.CaseValue.BoundElement.ConstantValue.HasValue));
                bool allIntConst = allConst && this.CaseBlocks.All(c => c.IsDefault || IsInt32(c.CaseValue.BoundElement.ConstantValue.Value));
                //bool allconststrings = allconsts && this.CaseBlocks.All(c => c.IsDefault || IsString(c.CaseValue.ConstantValue.Value));

                var default_block = this.DefaultBlock;

                // <switch_loc> = <SwitchValue>;
                TypeSymbol switch_type;
                LocalDefinition switch_loc;

                // Switch Header
                if (allIntConst)
                {
                    switch_type = cg.CoreTypes.Int32;
                    cg.EmitSequencePoint(this.SwitchValue.PhpSyntax);
                    cg.EmitConvert(this.SwitchValue, switch_type);
                    switch_loc = cg.GetTemporaryLocal(switch_type);
                    cg.Builder.EmitLocalStore(switch_loc);

                    // switch (labels)
                    cg.Builder.EmitIntegerSwitchJumpTable(GetSwitchCaseLabels(CaseBlocks), default_block ?? NextBlock, switch_loc, switch_type.PrimitiveTypeCode);
                }
                //else if (allconststrings)
                //{

                //}
                else
                {
                    // legacy jump table
                    // IF (case_i) GOTO label_i;

                    cg.EmitSequencePoint(this.SwitchValue.PhpSyntax);
                    switch_type = cg.Emit(this.SwitchValue);
                    switch_loc = cg.GetTemporaryLocal(switch_type);
                    cg.Builder.EmitLocalStore(switch_loc);

                    //
                    foreach (var this_block in this.CaseBlocks)
                    {
                        var caseValueBag = this_block.CaseValue;
                        if (caseValueBag.IsEmpty) { continue; }

                        if (!caseValueBag.IsOnlyBoundElement)
                        {
                            cg.ReturnTemporaryLocal(switch_loc); // statements in pre-bound-blocks could return (e.g. yieldStatement) & destroy stack-local switch_loc variable -> be defensive
                            caseValueBag.PreBoundBlockFirst.Emit(cg); // emit all blocks that have to go before case value emit
                        }

                        // reininiaze switch_loc if destroyed previously
                        if (!caseValueBag.IsOnlyBoundElement)
                        {
                            cg.Emit(this.SwitchValue);
                            switch_loc = cg.GetTemporaryLocal(switch_type);
                            cg.Builder.EmitLocalStore(switch_loc);
                        }

                        // <CaseValue>:
                        var caseValue = caseValueBag.BoundElement;
                        cg.EmitSequencePoint(caseValue.PhpSyntax);

                        // if (<switch_loc> == c.CaseValue) goto this_block;
                        cg.Builder.EmitLocalLoad(switch_loc);
                        BoundBinaryEx.EmitEquality(cg, switch_type, caseValue);
                        cg.Builder.EmitBranch(ILOpCode.Brtrue, this_block);
                    }

                    // default:
                    cg.Builder.EmitBranch(ILOpCode.Br, default_block ?? NextBlock);
                }

                // FREE <switch_loc>
                cg.ReturnTemporaryLocal(switch_loc);

                // Switch Body
                for (int i = 0; i < this.CaseBlocks.Length; i++)
                {
                    var next_case = (i + 1 < this.CaseBlocks.Length) ? this.CaseBlocks[i + 1] : null;

                    // {
                    cg.GenerateScope(this.CaseBlocks[i], (next_case ?? NextBlock).Ordinal);
                    // }

                }
            }

            cg.Scope.ContinueWith(NextBlock);
        }

        /// <summary>
        /// Gets case labels.
        /// </summary>
        static KeyValuePair<ConstantValue, object>[] GetSwitchCaseLabels(IEnumerable<CaseBlock> sections)
        {
            var labelsBuilder = ArrayBuilder<KeyValuePair<ConstantValue, object>>.GetInstance();
            foreach (var section in sections)
            {
                if (section.IsDefault)
                {
                    // fallThroughLabel = section
                }
                else
                {
                    labelsBuilder.Add(new KeyValuePair<ConstantValue, object>(Int32Constant(section.CaseValue.BoundElement.ConstantValue.Value), section));
                }
            }

            return labelsBuilder.ToArrayAndFree();
        }

        // TODO: move to helpers
        static ConstantValue Int32Constant(object value)
        {
            if (value is int) return ConstantValue.Create((int)value);
            if (value is long) return ConstantValue.Create((int)(long)value);
            if (value is double) return ConstantValue.Create((int)(double)value);

            throw new ArgumentException();
        }
    }
}
