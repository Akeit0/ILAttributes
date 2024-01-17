using System;

namespace ILAttributes.Experimental
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ILInlineAttribute : Attribute
    {
        public ILInlineAttribute(string opCodes,params Type[] types)
        {
        }
        public ILInlineAttribute(string opCodes)
        {
        }
    }
}