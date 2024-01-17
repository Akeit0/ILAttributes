using System;
namespace ILAttributes
{
   /// <summary>
   /// This is needed to process ILAttributes to its type members.
   /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class ILProcessAttribute : Attribute
    {
       
    }
    /// <summary>
    /// Return the same thing of the first argument 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ILRetIdentityAttribute : Attribute
    {
    }
    
    /// <summary>
    /// Generate UnsafeAccess. Allow 'unsafe' is needed to access non public members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ILUnsafeAccessorAttribute : Attribute
    {
        public ILUnsafeAccessorAttribute(byte kind,string name="")
        {
          
        }
        public ILUnsafeAccessorAttribute(byte kind,string name,Type declareType)
        {
        }
        
    }
    /// <summary>
    /// View IL Code on ILPP.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method|AttributeTargets.Constructor)]
    public class ILViewAttribute : Attribute
    {
        
    }
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    class ILRefGenAttribute : Attribute
    {
       
    }
    

    public static class ILUnsafeAccessorKind
    {
        public const byte Constructor = 0;
        public const byte Method = 1;
        public const byte Field = 2;
        public const byte StaticField = 3;
        public const byte StaticMethod = 4;
    }
}
