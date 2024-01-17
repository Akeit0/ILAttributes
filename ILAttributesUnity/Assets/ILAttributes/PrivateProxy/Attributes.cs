using System;
using System.Collections.Generic;

namespace ILAttributes.PrivateProxy
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GeneratePrivateProxyAttribute : Attribute
    {
        public Type Target { get; }
        public PrivateProxyGenerateKinds GenerateKinds { get; }

        public GeneratePrivateProxyAttribute(Type target)
        {
            this.Target = target;
            this.GenerateKinds = PrivateProxyGenerateKinds.All;
        }

        public GeneratePrivateProxyAttribute(Type target, PrivateProxyGenerateKinds generateKinds)
        {
            this.Target = target;
            this.GenerateKinds = generateKinds;
        }
    }

    [Flags]
    public enum PrivateProxyGenerateKinds
    {
        All = 0, // Field | Method | Property | Instance | Static
        Field = 1,
        Method = 2,
        Property = 4,
        Instance = 8,
        Static = 16,
    }
}