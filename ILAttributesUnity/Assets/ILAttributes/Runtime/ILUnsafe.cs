using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ILAttributes
{
    [ILProcess]
    public static unsafe class ILUnsafe
    {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILRetIdentity]
        public static extern ref T AsRef<T>(in T value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILRetIdentity]
        public static extern  T As<T>(object obj)where T:class;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILRetIdentity]
        public static extern  ref TRet AsRef<TSource,TRet>(ref TSource source);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0  ldarg.1 sizeof !!T mul add ret")]
        public static extern  ref T Add<T>(ref  T pointer,nint offset);
      
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0  ldarg.1 add ret")]
        public static extern  ref T AddByteOffset<T>(ref  T pointer,nint offset);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILRetIdentity]
        public static extern void* AsPointer<T>(ref T pointer);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 conv.u ret")]
        public static extern ref byte  AsRef<T>( T pointer)where T:class;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("sizeof !!T ret")]
        public static extern int SizeOf<T>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ret")]
        public static extern void SkipInit<T>(out T value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 ldarg.1 ldarg.2 cpblk ret")]
        public static extern void CopyBlock(ref byte destination, ref byte source, uint byteCount);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 ldarg.1 ceq ret")]
        public static extern bool AreSame<T>(ref T left, ref T right);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 ldobj !!T ret")]
        public static extern T Read<T>(void* source);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 unaligned. 1 ldobj !!T ret")]
        public static extern T ReadUnaligned<T>(void* source);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 unaligned. 1 ldobj !!T ret")]
        public static extern T ReadUnaligned<T>(ref byte  source);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 ldarg.1 unaligned. 1 stobj !!T ret")]
        public static extern void WriteUnaligned<T>(ref byte destination, T value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Experimental.ILInline("ldarg.0 ldarg.1 sub ret")]
        public static extern nint ByteOffset<T>(ref T origin, ref T target);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticMethod,"FastAllocateString",typeof(string))]
        public static extern string FastAllocateString(int length);
    }
   
}

