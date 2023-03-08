#pragma warning disable

#if !NET7_0_OR_GREATER

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit
    { }

    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        /// <summary>
        /// The name of the compiler feature.
        /// </summary>
        public string FeatureName { get; }
    }

    internal sealed class RequiredMemberAttribute : Attribute
    { }
}


internal static class ShimsExtensions
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
#endif
