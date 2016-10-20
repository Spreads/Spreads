using System;

// ReSharper disable once CheckNamespace
namespace System
{
    [AttributeUsage(AttributeTargets.Method)]
    class ILSub : Attribute
    {
        public ILSub(string il) { }
    }
}