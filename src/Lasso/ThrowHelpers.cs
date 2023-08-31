// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Lasso
{

    internal static partial class ArgumentNullThrowHelper
    {
        /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNull(
#if INTERNAL_NULLABLE_ATTRIBUTES || NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        [NotNull]
#endif
            object argument, string paramName = null)
        {
#if !NET7_0_OR_GREATER || NETSTANDARD || NETFRAMEWORK
            if (argument is null)
            {
                Throw(paramName);
            }
#else
        ArgumentNullException.ThrowIfNull(argument, paramName);
#endif
        }

#if !NET7_0_OR_GREATER || NETSTANDARD || NETFRAMEWORK
#if INTERNAL_NULLABLE_ATTRIBUTES || NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    [DoesNotReturn]
#endif
        internal static void Throw(string paramName) =>
            throw new ArgumentNullException(paramName);
#endif
    }

    internal static partial class ObjectDisposedThrowHelper
    {
        /// <summary>Throws an <see cref="ObjectDisposedException"/> if the specified <paramref name="condition"/> is <see langword="true"/>.</summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="instance">The object whose type's full name should be included in any resulting <see cref="ObjectDisposedException"/>.</param>
        /// <exception cref="ObjectDisposedException">The <paramref name="condition"/> is <see langword="true"/>.</exception>
        public static void ThrowIf(bool condition, object instance)
        {
#if !NET7_0_OR_GREATER
            if (condition)
            {
                ThrowObjectDisposedException(instance);
            }
#else
        ObjectDisposedException.ThrowIf(condition, instance);
#endif
        }

        /// <summary>Throws an <see cref="ObjectDisposedException"/> if the specified <paramref name="condition"/> is <see langword="true"/>.</summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="type">The type whose full name should be included in any resulting <see cref="ObjectDisposedException"/>.</param>
        /// <exception cref="ObjectDisposedException">The <paramref name="condition"/> is <see langword="true"/>.</exception>
        public static void ThrowIf(bool condition, Type type)
        {
#if !NET7_0_OR_GREATER
            if (condition)
            {
                ThrowObjectDisposedException(type);
            }
#else
        ObjectDisposedException.ThrowIf(condition, type);
#endif
        }

#if !NET7_0_OR_GREATER
        private static void ThrowObjectDisposedException(object instance)
        {
            throw new ObjectDisposedException(instance?.GetType().FullName);
        }

        private static void ThrowObjectDisposedException(Type type)
        {
            throw new ObjectDisposedException(type?.FullName);
        }
#endif
    }
}
