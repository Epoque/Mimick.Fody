﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mimick.Aspect;

namespace Mimick
{
    /// <summary>
    /// Indicates that the associated method should be invoked concurrently, requiring a write lock when the method is entered.
    /// </summary>
    [CompilationImplements(Interface = typeof(IRequireSynchronization))]
    [CompilationOptions(Scope = AttributeScope.Instanced)]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WriterAttribute : Attribute, IMethodInterceptor, IRequireInitialization, IRequireSynchronization
    {
        #region Properties

        /// <summary>
        /// Gets or sets the synchronization context used for concurrent read and write locks.
        /// </summary>
        ReaderWriterLockSlim IRequireSynchronization.SynchronizationContext
        {
            get; set;
        }

        #endregion

        /// <summary>
        /// Initialize the attribute.
        /// </summary>
        public void Initialize() => ((IRequireSynchronization)this).SynchronizationContext = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Called when a method has been invoked, and executes before the method body.
        /// </summary>
        /// <param name="e">The interception event arguments.</param>
        public void OnEnter(MethodInterceptionArgs e) => ((IRequireSynchronization)this).SynchronizationContext.EnterWriteLock();

        /// <summary>
        /// Called when a method has been invoked and has produced an unhandled exception.
        /// </summary>
        /// <param name="e">The interception event arguments.</param>
        /// <param name="ex">The intercepted exception.</param>
        public void OnException(MethodInterceptionArgs e, Exception ex) => throw ex;

        /// <summary>
        /// Called when a method has been invoked, and executes after the method body.
        /// </summary>
        /// <param name="e">The interception event arguments.</param>
        public void OnExit(MethodInterceptionArgs e) => ((IRequireSynchronization)this).SynchronizationContext.ExitWriteLock();
    }
}
