# ILAttributes
A collection of attributes for UnityEngine to write functions which can only be expressed by IL.

## How to install
### Package Manager
1. Open the Package Manager by going to Window > Package Manager.
2. Click on the "+" button and select "Add package from git URL".
3. Enter the following URL:

```
https://github.com/Akeit0/ILAttributes.git?path=/Assets/ILAttributes
```
### manifest.json
Open `Packages/manifest.json` and add the following in the `dependencies` block:

```json
"com.akeit0.com.akeit0.il-attributes": "https://github.com/Akeit0/ILAttributes.git?path=/Assets/ILAttributes"
```
# Contents
### To use ILAttributes you need to add [ILAttributes.ILProcess] attribute to the class.
## ILUnsafeAccessor
This is almost same as UnsafeAccessor in .NET8 but available in Unity.
```cs
using System;
using System.Runtime.CompilerServices;
using ILAttributes;
public class Class
{
    static void StaticPrivateMethod() { }
    static void StaticPrivateGenericMethod<T>() { }
    static int StaticPrivateField;
    Class(int i) { PrivateField = i; }
    void PrivateMethod() { }
    int PrivateField;
    void PrivateGenericMethod<T>() { }
}
public class Class<T>
{
    static void StaticPrivateMethod() { }
    // Not Supported now static void StaticPrivateGenericMethod<T>() { }
    static T StaticPrivateField;
    Class(T i) { PrivateField = i; }
    void PrivateMethod() { }
    T PrivateField;
   // Not Supported now void PrivateGenericMethod<T>() { }
}
[ILProcess]
public class AccessClass{
  public void CallStaticPrivateMethod()
  {
      StaticPrivateMethod(null);
  
      [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticMethod, Name = nameof(StaticPrivateMethod))]
      extern static void StaticPrivateMethod(Class c);
      [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticMethod, Name = nameof(StaticPrivateMethod),typeof(Class))]
      extern static void StaticPrivateMethod();
       [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticMethod, Name = nameof(StaticPrivateGenericMethod))]
      extern static void StaticPrivateGenericMethod<T>(Class c);
      [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticMethod, Name = nameof(StaticPrivateGenericMethod),typeof(Class))]
      extern static void StaticPrivateGenericMethod<T>();
  }
  public void GetSetStaticPrivateField()
  {
      ref int f = ref GetSetStaticPrivateField(null);
  
  
      [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticField, Name = "StaticPrivateField")]
      extern static ref int GetSetStaticPrivateField(Class c);
      [ILUnsafeAccessor(ILUnsafeAccessorKind.StaticField, Name = "StaticPrivateField",typeof(Class))]
      extern static ref int GetSetStaticPrivateField();
  }
  public void CallPrivateConstructor()
  {
      Class c1 = PrivateCtor(1);
  
      Class c2 = (Class)RuntimeHelpers.GetUninitializedObject(typeof(Class));
  
      PrivateCtorAsMethod(c2, 2);
  
  
      [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
      extern static Class PrivateCtor(int i);
  
      [UnsafeAccessor(UnsafeAccessorKind.Method, Name = ".ctor")]
      extern static void PrivateCtorAsMethod(Class c, int i);
  
  }
  public void CallPrivateMethod(Class c)
  {
      PrivateMethod(c);
  
      [ILUnsafeAccessor(ILUnsafeAccessorKind.Method, Name = nameof(PrivateMethod))]
      extern static void PrivateMethod(Class c);
      [ILUnsafeAccessor(ILUnsafeAccessorKind.Method, Name = nameof(PrivateGenericMethod))]
      extern static void PrivateGenericMethod<T>(Class c);
  }
  public void GetSetPrivateField(Class c)
  {
      ref int f = ref GetSetPrivateField(c);
  
  
      [ILUnsafeAccessor(ILUnsafeAccessorKind.Field, Name = "PrivateField")]
      extern static ref int GetSetPrivateField(Class c);
  }
}
```
## ILRetIdentity
This just returns first argument.
```cs
[ILProcess]
public class UnsafeClass{
  [ILRetIdentity]
  public static extern ref T AsRef<T>(in T value);
  [ILRetIdentity]
  public static extern  T As<T>(object obj)where T:class;
  [ILRetIdentity]
  public static extern  ref TRet AsRef<TSource,TRet>(ref TSource source);
  [ILRetIdentity]
  public static extern  ref Span<TRet> AsRef<TSource,TRet>(ref ref Span<TSource> source);
}
```
## Experimantal
### ILInline
InlineIL
Supports limited opcodes.
```cs
[ILProcess]
public class UnsafeClass{

  [Experimental.ILInline("ldarg.0  ldarg.1 sizeof !!T mul add ret")]
  public static extern  ref T Add<T>(ref  T pointer,nint offset);

  [Experimental.ILInline("ldarg.0  ldarg.1 add ret")]
  public static extern  ref T AddByteOffset<T>(ref  T pointer,nint offset);


  [Experimental.ILInline("ldarg.0 conv.u ret")]
  public static extern ref byte  AsRef<T>( T pointer)where T:class;


  [Experimental.ILInline("sizeof !!T ret")]
  public static extern int SizeOf<T>();
  
  [Experimental.ILInline("ret")]
  public static extern void SkipInit<T>(out T value);
  
  [Experimental.ILInline("ldarg.0 ldarg.1 ldarg.2 cpblk ret")]
  public static extern void CopyBlock(ref byte destination, ref byte source, uint byteCount);
        
}
```
# LICENCE
UNLICENCED
