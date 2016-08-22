using System;

namespace System
{
    [AttributeUsage(AttributeTargets.Method)]
    class ILSub : Attribute
    {
        public ILSub(string il) { }
    }
}