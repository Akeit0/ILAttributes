using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ILAttributes
{
    
  /// <summary>
  /// Represents ref field.
  /// </summary>
  /// <typeparam name="T"></typeparam>
    [ILRefGen]
    public   ref struct ByReference<T>
    {
       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public extern ByReference(ref T value);
        /// <summary>
        /// Get reference.
        /// </summary>
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => throw new NotImplementedException();
        }
    
    }
}