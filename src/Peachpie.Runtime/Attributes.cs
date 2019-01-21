﻿using Pchp.Core.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Pchp.Core
{
    /// <summary>
    /// Annotates a script class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ScriptAttribute : Attribute
    {
        /// <summary>
        /// Script path relative to the root.
        /// </summary>
        public string Path { get; private set; }

        public ScriptAttribute(string path)
        {
            this.Path = path;
        }
    }

    /// <summary>
    /// Annotates a script class from a phar archive.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PharAttribute : Attribute
    {
        /// <summary>
        /// PHAR file name.
        /// </summary>
        public string PharFile { get; private set; }

        public PharAttribute(string pharFile)
        {
            this.PharFile = pharFile;
        }
    }

    /// <summary>
    /// Assembly attribute indicating the assembly represents an extension.
    /// When this attribute is used on an assembly, declared types and methods are not visible to compiler as they are,
    /// instead, only public static members are visible as global declarations.
    /// 
    /// When used on the class, the attribute also annotates extension name and its set of functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    public class PhpExtensionAttribute : Attribute
    {
        /// <summary>
        /// Extensions name.
        /// </summary>
        public string[] Extensions { get; private set; }

        /// <summary>
        /// Optional.
        /// Type of class that will be instantiated in order to subscribe to <see cref="Context"/> events and/or perform one-time initialization.
        /// </summary>
        /// <remarks>
        /// The object is used to handle one-time initialization and context life-cycle.
        /// Implement initialization and subscription logic in .ctor.
        /// </remarks>
        public Type Registrator { get; set; }

        public PhpExtensionAttribute(params string[] extensions)
        {
            this.Extensions = extensions;
        }

        public override string ToString()
        {
            return $"Extension: {string.Join(", ", this.Extensions)}";
        }
    }

    /// <summary>
    /// Assembly attribute specifying language option used to compile the assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class TargetPhpLanguageAttribute : Attribute
    {
        /// <summary>
        /// Whether short open tags were enabled to compile the sources.
        /// </summary>
        public bool ShortOpenTag { get; set; }

        /// <summary>
        /// The language version of compiled sources.
        /// </summary>
        public string LanguageVersion { get; set; }

        /// <summary>
        /// Construct the attribute.
        /// </summary>
        public TargetPhpLanguageAttribute(string langVersion, bool shortOpenTag)
        {
            this.ShortOpenTag = shortOpenTag;
            this.LanguageVersion = langVersion;
        }
    }

    /// <summary>
    /// Marks public declarations that won't be visible in the PHP context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Field)]
    public sealed class PhpHiddenAttribute : Attribute
    {
    }

    /// <summary>
    /// Indicates to compiler that a symbol should be ignored unless a specified conditional compilation scope is valid.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = true)]
    public sealed class PhpConditionalAttribute : Attribute
    {
        public string ConditionString => _scope;
        readonly string _scope;

        public PhpConditionalAttribute(string scope)
        {
            _scope = scope;
        }
    }

    /// <summary>
    /// Marks public class or interface declaration as a PHP type visible to the scripts from extension libraries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class PhpTypeAttribute : Attribute
    {
        /// <summary>
        /// Optional. Explicitly set type name.
        /// </summary>
        public string ExplicitTypeName { get; }

        /// <summary>
        /// Indicates how to treat the type name.
        /// </summary>
        public PhpTypeName TypeNameAs { get; }

        /// <summary>
        /// Optional. Relative path to the file where the type is defined.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Value stating that the type name is inherited from the CLR name excluding its namespace part, see <see cref="PhpTypeName.NameOnly"/>.
        /// It causes CLR type <c>A.B.C.X</c> to appear in PHP as <c>X</c>.
        /// </summary>
        public const PhpTypeName InheritName = PhpTypeName.NameOnly;

        /// <summary>
        /// Value indicating how to treat the type name in PHP.
        /// </summary>
        public enum PhpTypeName
        {
            /// <summary>
            /// Full type name including its namespace name is used.
            /// </summary>
            Default = 0,

            /// <summary>
            /// Namespace of the CLR type is ignored.
            /// </summary>
            NameOnly = 1,

            /// <summary>
            /// The name is set explicitly overriding the CLR's type name.
            /// </summary>
            CustomName = 2,
        }

        /// <summary>
        /// Annotates the PHP type.
        /// </summary>
        public PhpTypeAttribute(PhpTypeName typeNameAs = PhpTypeName.Default)
        {
            Debug.Assert(typeNameAs != PhpTypeName.CustomName);
            TypeNameAs = typeNameAs;
        }

        /// <summary>
        /// Annotates the PHP type.
        /// </summary>
        /// <param name="phpTypeName">The type name that will be used in PHP context instead of CLR type name.</param>
        /// <param name="fileName">Optional relative path to the file where the type is defined.</param>
        public PhpTypeAttribute(string phpTypeName, string fileName = null)
        {
            ExplicitTypeName = phpTypeName ?? throw new ArgumentNullException();
            FileName = fileName;
            TypeNameAs = PhpTypeName.CustomName;
        }
    }

    /// <summary>
    /// Specifies real member accessibility as it will appear in declaring class.
    /// </summary>
    /// <remarks>
    /// Some members have to be emitted as public to be accessible from outside but appear non-public in PHP context.
    /// This attribute specifies real visibility of the member - method, property or class constant.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PhpMemberVisibilityAttribute : Attribute
    {
        /// <summary>
        /// Declared member accessibility flag.
        /// </summary>
        public int Accessibility { get; }

        /// <summary>
        /// Initializes the attribute.
        /// </summary>
        public PhpMemberVisibilityAttribute(int accessibility) { this.Accessibility = accessibility; }
    }

    /// <summary>
    /// Denotates a function parameter that will be loaded with current class.
    /// The parameter must be of type <see cref="RuntimeTypeHandle"/>, <see cref="PhpTypeInfo"/> or <see cref="string"/>.
    /// </summary>
    /// <remarks>
    /// The parameter is used to access callers class context.
    /// The parameter must be before regular parameters.</remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ImportCallerClassAttribute : Attribute
    {

    }

    /// <summary>
    /// Denotates a function parameter that will be loaded with current late static bound class.
    /// The parameter must be of type <see cref="PhpTypeInfo"/>.
    /// </summary>
    /// <remarks>
    /// The parameter is used to access calers' late static class (<c>static</c>).
    /// The parameter must be before regular parameters.</remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ImportCallerStaticClassAttribute : Attribute
    {

    }

    /// <summary>
	/// Marks return values of methods implementing PHP functions which returns <B>false</B> on error
	/// but has other return type than <see cref="bool"/> or <see cref="object"/>.
	/// </summary>
	/// <remarks>
	/// Compiler takes care of converting a return value of a method into <B>false</B> if necessary.
	/// An attribute can be applied only on return values of type <see cref="int"/> or <see cref="double"/> (less than 0 is converted to <B>false</B>)
	/// or of a reference type (<B>null</B> is converted to <B>false</B>).
	/// </remarks>
    [AttributeUsage(AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = false)]
    public sealed class CastToFalse : Attribute
    {

    }

    /// <summary>
    /// Marks classes that are declared as trait.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class PhpTraitAttribute : Attribute
    {

    }

    /// <summary>
    /// Compiler generated attribute denoting constructor that initializes only fields and calls minimal base .ctor.
    /// Such constructor is used for emitting derived class constructor that calls PHP constructor function by itself.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class PhpFieldsOnlyCtorAttribute : Attribute
    {

    }

    /// <summary>
    /// Compiler generated attribute denoting that associated value cannot be <c>null</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class NotNullAttribute : Attribute
    {

    }

    /// <summary>
	/// Marks arguments having by-value argument pass semantics and data of the value can be changed by a callee.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class PhpRwAttribute : Attribute
    {
    }
}
