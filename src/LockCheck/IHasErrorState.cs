using System;

namespace LockCheck
{
    /// <summary>
    /// An entity that can have error state.
    /// </summary>
    public interface IHasErrorState
    {
        /// <summary>
        /// Get a value that indicates if the entity is errnous.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// Set the error state of the entity..
        /// </summary>
        /// <param name="ex">An optional exception that caused the error.</param>
        void SetError(Exception ex = null);
    }
}
