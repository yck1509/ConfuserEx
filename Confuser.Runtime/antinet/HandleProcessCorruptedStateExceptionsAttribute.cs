using System;

namespace System.Runtime.ExceptionServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class HandleProcessCorruptedStateExceptionsAttribute : Attribute
    {
    }
}
