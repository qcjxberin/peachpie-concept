﻿using System;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using Pchp.Core.Reflection;

namespace Pchp.Core.Dynamic
{
    #region ISpecialParamHolder

    /// <summary>
    /// Denotates a parameter that is special, provided by compiler.
    /// </summary>
    internal interface ISpecialParamHolder
    {
        /// <summary>
        /// Gets value indicating the parameter is loaded by compiler providing context.
        /// </summary>
        bool IsImplicit { get; }

        /// <summary>
        /// Sets information to the callsite context.
        /// </summary>
        void Process(CallSiteContext info, Expression valueExpr);
    }

    #endregion

    /// <summary>
    /// Wraps an argument passed to callsite denotating a special meaning of the value.
    /// </summary>
    public struct ContextParam : ISpecialParamHolder
    {
        /// <summary>
        /// Runtime context.
        /// </summary>
        public Context Value;

        bool ISpecialParamHolder.IsImplicit => true;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            info.Context = valueExpr;
        }

        /// <summary>Initializes the structure.</summary>
        public ContextParam(Context value) => Value = value;
    }

    /// <summary>
    /// Wraps an argument passed to callsite denotating a function or property name.
    /// </summary>
    public struct NameParam<T> : ISpecialParamHolder
    {
        /// <summary>
        /// The invoked member, <c>callable</c>.
        /// </summary>
        public T Value;

        bool ISpecialParamHolder.IsImplicit => true;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            if (valueExpr.Type != typeof(string))
            {
                throw new InvalidOperationException();
            }

            var name = (string)(object)Value;

            info.IndirectName = valueExpr;
            info.AddRestriction(Expression.Equal(valueExpr, Expression.Constant(name)));

            if (info.Name == null)
            {
                info.Name = name;
            }
        }

        /// <summary>Initializes the structure.</summary>
        public NameParam(T value) => Value = value;
    }

    /// <summary>
    /// Wraps an argument passed to callsite denotating target type of a static invocation operation (call, static field, class const).
    /// </summary>
    public struct TargetTypeParam : ISpecialParamHolder
    {
        /// <summary>
        /// Target type.
        /// </summary>
        public PhpTypeInfo Value;

        bool ISpecialParamHolder.IsImplicit => true;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            Debug.Assert(Value != null);

            info.AddRestriction(BindingRestrictions.GetInstanceRestriction(valueExpr, Value));  // {arg} != null && {arg} == Value
            info.TargetType = Value;
        }

        /// <summary>Initializes the structure.</summary>
        public TargetTypeParam(PhpTypeInfo value) => Value = value;
    }

    /// <summary>
    /// Wraps an argument passed to callsite denotating a generic argument.
    /// </summary>
    public struct GenericParam : ISpecialParamHolder
    {
        /// <summary>
        /// A generic argument.
        /// </summary>
        public PhpTypeInfo Value;

        bool ISpecialParamHolder.IsImplicit => true;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            throw new NotImplementedException();
        }

        /// <summary>Initializes the structure.</summary>
        public GenericParam(PhpTypeInfo value) => Value = value;
    }

    /// <summary>
    /// Wraps an argument passed to callsite denotating a caller type.
    /// </summary>
    public struct CallerTypeParam : ISpecialParamHolder
    {
        /// <summary>
        /// Caller type context.
        /// </summary>
        public RuntimeTypeHandle Value;

        bool ISpecialParamHolder.IsImplicit => true;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            info.AddRestriction(Expression.Call(valueExpr, Cache.Operators.RuntimeTypeHandle_Equals_RuntimeTypeHandle, Expression.Constant(Value)));
            info.ClassContext = Type.GetTypeFromHandle(Value);
        }

        /// <summary>Initializes the structure.</summary>
        public CallerTypeParam(RuntimeTypeHandle value) => Value = value;
    }

    /// <summary>
    /// Wraps the argument unpacking.
    /// </summary>
    public struct UnpackingParam<T> : ISpecialParamHolder
    {
        public T Value;

        bool ISpecialParamHolder.IsImplicit => false;

        void ISpecialParamHolder.Process(CallSiteContext info, Expression valueExpr)
        {
            throw new InvalidOperationException();
        }

        /// <summary>Initializes the structure.</summary>
        public UnpackingParam(T value) => Value = value;
    }
}
