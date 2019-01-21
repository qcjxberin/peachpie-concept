﻿using Devsense.PHP.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Symbols
{
    /// <summary>
    /// A symbol representing PHP type in CLR.
    /// </summary>
    public interface IPhpTypeSymbol : INamedTypeSymbol
    {
        /// <summary>
        /// Gets fully qualified name of the class.
        /// </summary>
        QualifiedName FullName { get; }

        /// <summary>
        /// Gets value indicating the class is declared as a trait.
        /// </summary>
        bool IsTrait { get; }

        #region Model

        /// <summary>
        /// Optional.
        /// A field holding a reference to current runtime context.
        /// Is of type <c>Context</c>.
        /// </summary>
        IFieldSymbol ContextStore { get; }

        /// <summary>
        /// Optional.
        /// A field holding array of the class runtime fields.
        /// Is of type <c>PhpArray</c>.
        /// </summary>
        IFieldSymbol RuntimeFieldsStore { get; }

        /// <summary>
        /// Optional. A <c>.ctor</c> that does not make call to PHP constructor.
        /// This method is expected to be declared with <b>protected</b> visibility and
        /// used in context of a derived class constructor, since in PHP user calls PHP constructor explicitly.
        /// </summary>
        IMethodSymbol InstanceConstructorFieldsOnly { get; }

        ///// <summary>
        ///// Gets enumeration of used traits.
        ///// </summary>
        //IEnumerable<IPhpTypeSymbol> Traits { get; }

        #endregion
    }
}
