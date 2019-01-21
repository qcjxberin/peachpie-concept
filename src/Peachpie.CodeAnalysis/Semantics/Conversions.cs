﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.Symbols;
using Peachpie.CodeAnalysis.Utilities;

namespace Pchp.CodeAnalysis.Semantics
{
    sealed class Conversions
    {
        readonly PhpCompilation _compilation;

        public Conversions(PhpCompilation compilation)
        {
            _compilation = compilation ?? throw ExceptionUtilities.ArgumentNull();
        }

        static CommonConversion IdentityConversion => new CommonConversion(true, true, false, false, true, null);
        static CommonConversion ReferenceConversion => new CommonConversion(true, false, false, true, true, null);
        static CommonConversion ExplicitReferenceConversion => new CommonConversion(true, false, false, true, false, null);
        static CommonConversion NoConversion => new CommonConversion(false, false, false, false, false, null);
        static CommonConversion ImplicitNumeric => new CommonConversion(true, false, true, false, true, null);
        static CommonConversion ExplicitNumeric => new CommonConversion(true, false, true, false, false, null);

        static int ConvCost(CommonConversion conv, bool returning)
        {
            return conv.IsIdentity ? 0 : (conv.IsImplicit ^ returning) ? 1 : 3;
        }

        static (bool floating, bool signed, int size) ClassifyNumericType(TypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean: return (false, false, 1); // we classsify boolean as a number as well!
                case SpecialType.System_Char: return (false, false, 8);
                case SpecialType.System_SByte: return (false, true, 8);
                case SpecialType.System_Byte: return (false, false, 8);
                case SpecialType.System_Int16: return (false, true, 16);
                case SpecialType.System_UInt16: return (false, false, 16);
                case SpecialType.System_Int32: return (false, true, 32);
                case SpecialType.System_UInt32: return (false, false, 32);
                case SpecialType.System_Int64: return (false, true, 64);
                case SpecialType.System_UInt64: return (false, false, 64);
                //case SpecialType.System_IntPtr: return (false, true, 64);
                //case SpecialType.System_UIntPtr: return (false, false, 64);
                case SpecialType.System_Single: return (true, true, 32);
                case SpecialType.System_Double: return (true, true, 64);
                case SpecialType.System_Decimal: return (true, true, 128);
                default:

                    if (type.IsEnumType())
                    {
                        return ClassifyNumericType(type.GetEnumUnderlyingType());
                    }

                    return default;
            }
        }

        // numeric conversions
        public static CommonConversion ClassifyNumericConversion(TypeSymbol from, TypeSymbol to)
        {
            var fromnum = ClassifyNumericType(from);
            if (fromnum.size == 0) return NoConversion;

            var tonum = ClassifyNumericType(to);
            if (tonum.size == 0) return NoConversion;

            // both types are numbers,
            // naive conversion:

            if (fromnum.size < tonum.size || (fromnum.size == tonum.size && fromnum.signed == tonum.signed)) // blah
                return ImplicitNumeric;
            else
                return ExplicitNumeric;
        }

        // resolve operator method
        public MethodSymbol ResolveOperator(TypeSymbol receiver, bool hasref, string[] opnames, TypeSymbol[] extensions, TypeSymbol operand = null, TypeSymbol target = null)
        {
            Debug.Assert(receiver != null);
            Debug.Assert(opnames != null && opnames.Length != 0);

            MethodSymbol candidate = null;
            int candidatecost = int.MaxValue;   // candidate cost
            int candidatecost_minor = 0;        // second cost

            for (int ext = -1; ext < extensions.Length; ext++)
            {
                for (var container = ext < 0 ? receiver : extensions[ext]; container != null; container = container.IsStatic ? null : container.BaseType)
                {
                    if (container.SpecialType == SpecialType.System_ValueType) continue; //

                    for (int i = 0; i < opnames.Length; i++)
                    {
                        var members = container.GetMembers(opnames[i]);
                        for (int m = 0; m < members.Length; m++)
                        {
                            if (members[m] is MethodSymbol method)
                            {
                                if (ext >= 0 && !method.IsStatic) continue;    // only static methods allowed in extension containers
                                if (method.DeclaredAccessibility != Accessibility.Public) continue;
                                if (method.Arity != 0) continue; // CONSIDER

                                // TODO: replace with overload resolution

                                int cost = 0;
                                int cost_minor = 0;

                                if (target != null && method.ReturnType != target)
                                {
                                    var conv = ClassifyConversion(method.ReturnType, target, checkimplicit: false, checkexplicit: false);
                                    if (conv.Exists)    // TODO: chain the conversion, sum the cost
                                    {
                                        cost += ConvCost(conv, true);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                var ps = method.Parameters;
                                int pconsumed = 0;

                                // TSource receiver,
                                if (method.IsStatic)
                                {
                                    if (ps.Length <= pconsumed) continue;
                                    bool isbyref = ps[pconsumed].RefKind != RefKind.None;
                                    if (isbyref && hasref == false) continue;
                                    // if (container != receiver && ps[pconsumed].HasThisAttribute == false) continue; // [ThisAttribute] // proper extension method
                                    var pstype = ps[pconsumed].Type;
                                    if (pstype != receiver)
                                    {
                                        if (isbyref) continue; // cannot convert addr

                                        var conv = ClassifyConversion(receiver, pstype, checkexplicit: false, checkimplicit: false);
                                        if (conv.Exists)   // TODO: chain the conversion
                                        {
                                            cost += ConvCost(conv, false);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                    pconsumed++;
                                }

                                // Context ctx, 
                                if (pconsumed < ps.Length && SpecialParameterSymbol.IsContextParameter(ps[pconsumed]))
                                {
                                    pconsumed++;
                                    cost_minor--; // specialized operator - prefered
                                }

                                // TOperand,
                                if (operand != null)
                                {
                                    if (ps.Length <= pconsumed) continue;
                                    if (ps[pconsumed].Type != operand)
                                    {
                                        var conv = ClassifyConversion(operand, ps[pconsumed].Type, checkexplicit: false);
                                        if (conv.Exists)    // TODO: chain the conversion
                                        {
                                            cost += ConvCost(conv, false);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                    pconsumed++;
                                }

                                // Context ctx, 
                                if (pconsumed < ps.Length && SpecialParameterSymbol.IsContextParameter(ps[pconsumed]))
                                {
                                    pconsumed++;
                                    cost_minor--;   // specialized operator - prefered
                                }

                                if (ps.Length != pconsumed) continue;

                                if (container.SpecialType == SpecialType.System_Object ||
                                    container.IsValueType)
                                {
                                    //cost++; // should be enabled
                                    cost_minor++;   // implicit conversion
                                }

                                //
                                if (cost < candidatecost || (cost == candidatecost && cost_minor < candidatecost_minor))
                                {
                                    candidate = method;
                                    candidatecost = cost;
                                    candidatecost_minor = cost_minor;
                                }
                            }
                        }
                    }
                }
            }

            //

            return candidate;
        }

        // resolve implicit conversion
        string[] ImplicitConversionOpNames(TypeSymbol target)
        {
            switch (target.SpecialType)
            {
                case SpecialType.System_Char: return new[] { "AsChar", "ToChar" };
                case SpecialType.System_Boolean: return new[] { WellKnownMemberNames.ImplicitConversionName, "AsBoolean", "ToBoolean" };
                case SpecialType.System_Int32: return new[] { WellKnownMemberNames.ImplicitConversionName, "AsInt", "ToInt", "ToLong" };
                case SpecialType.System_Int64: return new[] { WellKnownMemberNames.ImplicitConversionName, "AsLong", "ToLong" };
                case SpecialType.System_Double: return new[] { WellKnownMemberNames.ImplicitConversionName, "AsDouble", "ToDouble" };
                case SpecialType.System_String: return new[] { WellKnownMemberNames.ImplicitConversionName, "AsString", WellKnownMemberNames.ObjectToString };
                case SpecialType.System_Object: return new[] { "AsObject" }; // implicit conversion to object is not possible
                default:

                    // AsPhpArray

                    // ToNumber
                    if (target == _compilation.CoreTypes.PhpNumber.Symbol) return new[] { WellKnownMemberNames.ImplicitConversionName, "ToNumber" };

                    // ToPhpString
                    if (target == _compilation.CoreTypes.PhpString.Symbol) return new[] { WellKnownMemberNames.ImplicitConversionName, "ToPhpString" };

                    // AsResource
                    // AsObject
                    // AsPhpValue
                    if (target == _compilation.CoreTypes.PhpValue.Symbol) return new[] { WellKnownMemberNames.ImplicitConversionName };

                    // AsPhpAlias
                    if (target == _compilation.CoreTypes.PhpAlias.Symbol) return new[] { WellKnownMemberNames.ImplicitConversionName, "AsPhpAlias" };

                    // enum
                    if (target.IsEnumType()) return ImplicitConversionOpNames(target.GetEnumUnderlyingType());

                    //
                    return new[] { WellKnownMemberNames.ImplicitConversionName };
            }
        }

        string[] ExplicitConversionOpNames(TypeSymbol target)
        {
            switch (target.SpecialType)
            {
                case SpecialType.System_Boolean: return new[] { WellKnownMemberNames.ExplicitConversionName, "ToBoolean" };
                case SpecialType.System_Int32: return new[] { WellKnownMemberNames.ExplicitConversionName, "ToInt", "ToLong" };
                case SpecialType.System_Int64: return new[] { WellKnownMemberNames.ExplicitConversionName, "ToLong" };
                case SpecialType.System_Double: return new[] { WellKnownMemberNames.ExplicitConversionName, "ToDouble" };
                case SpecialType.System_String: return new[] { WellKnownMemberNames.ExplicitConversionName, WellKnownMemberNames.ObjectToString };
                case SpecialType.System_Object: return new[] { "ToObject" };    // implicit conversion to object is not possible
                default:

                    // AsPhpArray
                    if (target == _compilation.CoreTypes.PhpArray.Symbol) return new[] { WellKnownMemberNames.ExplicitConversionName, "ToArray" };

                    // ToNumber
                    if (target == _compilation.CoreTypes.PhpNumber.Symbol) return new[] { WellKnownMemberNames.ExplicitConversionName, "ToNumber" };

                    // ToPhpString
                    if (target == _compilation.CoreTypes.PhpString.Symbol) return new[] { WellKnownMemberNames.ExplicitConversionName, "ToPhpString" };

                    // ToPhpValue
                    // ToPhpAlias

                    return new[] { WellKnownMemberNames.ExplicitConversionName };
            }
        }

        /// <summary>
        /// Checks the type is a reference type (derived from <c>System.Object</c>) but it has a special meaning in PHP's semantics.
        /// Such a type cannot be converted to Object by simple casting.
        /// Includes: string, resource, array, alias.
        /// </summary>
        public static bool IsSpecialReferenceType(TypeSymbol t)
        {
            if (t.IsReferenceType)
            {
                if (t.SpecialType == SpecialType.System_String)
                {
                    return true;
                }

                if (t.ContainingAssembly?.IsPeachpieCorLibrary == true)
                {
                    // TODO: constants
                    if (t.Name == "PhpAlias" ||
                        t.Name == "PhpResource" ||
                        t.Name == "PhpArray")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public CommonConversion ClassifyConversion(TypeSymbol from, TypeSymbol to, bool checkimplicit = true, bool checkexplicit = true)
        {
            if (from == to)
            {
                return IdentityConversion;
            }

            if (from.IsReferenceType && to.IsReferenceType && from.IsOfType(to))
            {
                // (PHP) string, resource, array, alias -> object: NoConversion

                if (to.SpecialType != SpecialType.System_Object || !IsSpecialReferenceType(from))
                {
                    return ReferenceConversion;
                }
            }

            if (to.SpecialType == SpecialType.System_Object && from.IsInterfaceType())
            {
                return ReferenceConversion;
            }

            // implicit conversions handled by 'EmitConversion':

            if (to.SpecialType == SpecialType.System_Void)
            {
                return IdentityConversion;
            }

            // resolve conversion operator method:

            var conv = ClassifyNumericConversion(from, to);
            if (!conv.Exists)
            {
                // TODO: cache result

                var op = checkimplicit ? ResolveOperator(from, false, ImplicitConversionOpNames(to), new[] { to, _compilation.CoreTypes.Convert.Symbol }, target: to) : null;

                if (op != null)
                {
                    conv = new CommonConversion(true, false, false, false, true, op);
                }
                else if (checkexplicit)
                {
                    op = ResolveOperator(from, false, ExplicitConversionOpNames(to), new[] { to, _compilation.CoreTypes.Convert.Symbol }, target: to);

                    if (op != null)
                    {
                        conv = new CommonConversion(true, false, false, false, false, op);
                    }
                    // explicit reference conversion (reference type -> reference type)
                    else if (
                        from.IsReferenceType && to.IsReferenceType &&
                        !IsSpecialReferenceType(from) && !IsSpecialReferenceType(to) &&
                        !from.IsArray() && !to.IsArray())
                    {
                        conv = ExplicitReferenceConversion;
                    }
                }
            }

            return conv;
        }
    }
}
