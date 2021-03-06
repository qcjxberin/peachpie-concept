﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Pchp.CodeAnalysis.FlowAnalysis
{
    /// <summary>
    /// Represents type mask for <see cref="TypeRefContext"/>.
    /// </summary>
    public struct TypeRefMask : IEquatable<TypeRefMask>, IEquatable<ulong>
    {
        #region Constants

        public const string MixedTypeName = "mixed";
        public const string VoidTypeName = "void";

        private const int BitsCount = sizeof(ulong) * 8;

        /// <summary>
        /// Gets maximum number of types that can be handled by <see cref="TypeRefMask"/>.
        /// </summary>
        public const int IndicesCount = BitsCount - 2;  // BitsCount - bits_reserved_for_flags

        /// <summary>
        /// Bit mask of all flags supported by the <see cref="TypeRefMask"/>.
        /// </summary>
        public const ulong FlagsMask = (ulong)MaskFlags.Mask;

        /// <summary>
        /// Mask of any type.
        /// </summary>
        public const ulong AnyTypeMask = ~(ulong)0;

        #endregion

        #region enum MaskFlags

        /// <summary>
        /// Additional type mask flags.
        /// </summary>
        [Flags]
        private enum MaskFlags : ulong
        {
            IsHint = (ulong)1 << (BitsCount - 1),
            IncludesSubclasses = (ulong)1 << (BitsCount - 2),

            /// <summary>
            /// Mask of all flags.
            /// </summary>
            Mask = IsHint | IncludesSubclasses
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// Each bit corresponds to a type within its <see cref="TypeRefContext"/>.
        /// </summary>
        public ulong Mask { get { return _mask; } }
        private ulong _mask;

        /// <summary>
        /// Gets value indicating whether the type represents an any type.
        /// </summary>
        public bool IsAnyType { get { return _mask == AnyTypeMask; } }

        /// <summary>
        /// Gets value indicating whether the type information is not initialized.
        /// </summary>
        /// <remarks>Also represents <c>void</c> type.</remarks>
        public bool IsUninitialized { get { return _mask == (ulong)0; } }

        /// <summary>
        /// Gets value indicating whether the type represents <c>void</c>.
        /// </summary>
        public bool IsVoid { get { return (_mask & ~(ulong)(MaskFlags.Mask)) == 0; } }

        /// <summary>
        /// Gets or sets value indicating whether the type represents a hint.
        /// </summary>
        public bool IsHint
        {
            get { return (_mask & (ulong)MaskFlags.IsHint) != 0; }
            set
            {
                if (value) SetIsHint();
                else RemoveIsHint();
            }
        }

        /// <summary>
        /// Gets value indicating whether given type mask represents a type including its subclasses.
        /// </summary>
        public bool IncludesSubclasses
        {
            get { return (_mask & (ulong)MaskFlags.IncludesSubclasses) != 0; }
            set
            {
                if (value) SetIncludesSubclasses();
                else RemoveIncludesSubclasses();
            }
        }

        /// <summary>
        /// Gets value indicating whether the mask represents just a single type reference.
        /// </summary>
        public bool IsSingleType
        {
            get
            {
                var mask = _mask & ~FlagsMask;    // remove flags
                return mask != 0 && (mask & (mask - 1)) == 0;
            }
        }

        /// <summary>
        /// Gets type mask representing any type.
        /// </summary>
        public static TypeRefMask AnyType { get { return new TypeRefMask(AnyTypeMask); } }

        #endregion

        #region Construction

        public TypeRefMask(ulong mask)
        {
            _mask = mask;
        }

        /// <summary>
        /// Creates type mask corresponding to a single type of given index.
        /// </summary>
        public static TypeRefMask CreateFromTypeIndex(int index)
        {
            var typemask = default(TypeRefMask);
            typemask.AddType(index);

            //
            return typemask;
        }

        #endregion

        /// <summary>
        /// Gets value indicating whether this type mask represents the type with given index.
        /// </summary>
        public bool HasType(int index)
        {
            return ((_mask & ((ulong)1 << index)) != 0 && index < IndicesCount && index >= 0);
        }

        /// <summary>
        /// Adds type with given index to the mask.
        /// </summary>
        public void AddType(int index)
        {
            if (index >= 0 && index < IndicesCount)
            {
                _mask |= ((ulong)1 << index);
            }
            else
            {
                _mask = AnyTypeMask;  // AnyType
            }
        }

        /// <summary>
        /// Marks this type as a hint.
        /// </summary>
        public void SetIsHint()
        {
            _mask |= (ulong)MaskFlags.IsHint;
        }

        /// <summary>
        /// Marks this type as not a hint.
        /// </summary>
        public void RemoveIsHint()
        {
            if (!this.IsAnyType)
            {
                _mask &= ~(ulong)MaskFlags.IsHint;
            }
        }

        /// <summary>
        /// Marks this type as a hint.
        /// </summary>
        public void SetIncludesSubclasses()
        {
            _mask |= (ulong)MaskFlags.IncludesSubclasses;
        }

        /// <summary>
        /// Marks this type as not a hint.
        /// </summary>
        internal void RemoveIncludesSubclasses()
        {
            if (!this.IsAnyType)
            {
                _mask &= ~(ulong)MaskFlags.IncludesSubclasses;
            }
        }

        /// <summary>
        /// Gets or sets value indicating whether the mask represents a type at given index.
        /// </summary>
        /// <remarks>Index higher than or equal to <see cref="IndicesCount"/> are ignored.</remarks>
        public bool this[int index]
        {
            get
            {
                return HasType(index);
            }
            set
            {
                Debug.Assert(index >= 0);
                if (value) AddType(index);
                else if (index < IndicesCount && !this.IsAnyType) _mask &= ~((ulong)1 << index);
            }
        }

        #region Object Members

        public override int GetHashCode()
        {
            return _mask.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is TypeRefMask && base.Equals((TypeRefMask)obj);
        }

        public override string ToString()
        {
            if (IsAnyType) return MixedTypeName;

            string value = null;
            if (IsVoid)
            {
                value = VoidTypeName;
            }
            else
            {
                ulong mask = this.Mask & ~(ulong)FlagsMask;
                for (int i = 0; mask != 0; i++, mask = (mask & ~(ulong)1) >> 1)
                {
                    if ((mask & 1) != 0)
                    {
                        if (value != null) value += ",";
                        value += (i).ToString();
                    }
                }
            }

            //
            if (IsHint)
            {
                value += " (hint)";
            }

            //
            return value;
        }

        #endregion

        #region IEquatable<TypeRefMask> Members

        public bool Equals(TypeRefMask other)
        {
            return Equals(other.Mask);
        }

        #endregion

        #region Operators

        public static bool operator ==(TypeRefMask a, TypeRefMask b)
        {
            return a.Mask == b.Mask;
        }

        public static bool operator !=(TypeRefMask a, TypeRefMask b)
        {
            return a.Mask != b.Mask;
        }

        public static TypeRefMask operator +(TypeRefMask a, TypeRefMask b)
        {
            return new TypeRefMask(a.Mask | b.Mask);
        }

        public static TypeRefMask operator |(TypeRefMask a, TypeRefMask b)
        {
            return new TypeRefMask(a.Mask | b.Mask);
        }

        public static TypeRefMask operator |(TypeRefMask a, ulong b)
        {
            return new TypeRefMask(a.Mask | b);
        }

        public static TypeRefMask Or (TypeRefMask a, TypeRefMask b)
        {
            return a | b;
        }

        public static implicit operator ulong(TypeRefMask type)
        {
            return type.Mask;
        }

        public static implicit operator TypeRefMask(ulong mask)
        {
            return new TypeRefMask(mask);
        }

        #endregion

        #region IEquatable<ulong> Members

        public bool Equals(ulong other)
        {
            return _mask == other;
        }

        #endregion
    }
}
