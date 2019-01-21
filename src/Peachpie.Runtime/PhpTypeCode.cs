﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.Core
{
    /// <summary>
    /// Pchp type hierarchy type codes.
    /// </summary>
    public enum PhpTypeCode : int
    {
        /// <summary>
        /// An invalid value, <c>void</c>.
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// The value is of type boolean.
        /// </summary>
        Boolean,

        /// <summary>
        /// 32-bit integer value. For compatibility with CLR only.
        /// </summary>
        Int32,

        /// <summary>
        /// 64-bit integer value.
        /// </summary>
        Long,

        /// <summary>
        /// 64-bit floating point number.
        /// </summary>
        Double,

        /// <summary>
        /// A PHP array.
        /// </summary>
        PhpArray,

        /// <summary>
        /// Unicode string value. Two-byte (UTF16) readonly string.
        /// </summary>
        String,

        /// <summary>
        /// Both Unicode and Binary writable string value. Encapsulates two-byte (UTF16), single-byte (binary) string and string builder.
        /// </summary>
        MutableString,

        /// <summary>
        /// A class type, <c>resource</c>, <c>Closure</c> or generic <c>Object</c>. Does not represent <c>NULL</c>.
        /// </summary>
        Object,

        /// <summary>
        /// <c>callable</c> object.
        /// </summary>
        Callable,

        /// <summary>
        /// <see cref="PhpAlias"/> type.
        /// </summary>
        Alias,

        /// <summary>
        /// A resource object.
        /// </summary>
        Resource,

        /// <summary>
        /// <c>NULL</c> value.
        /// </summary>
        Null,
    }

    /// <summary>
    /// Helper class providing methods for <see cref="PhpTypeCode"/>.
    /// </summary>
    public static class PhpTypeCodes
    {
        ///// <summary>
        ///// Gets value indicating whether given type is a nullable type.
        ///// </summary>
        //public static bool IsNullable(this PhpTypeCode code)
        //{
        //    return code == PhpTypeCode.Object || code == PhpTypeCode.PhpArray;
        //}
    }
}
