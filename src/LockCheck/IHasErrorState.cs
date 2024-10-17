using System;

namespace LockCheck
{
    /// <summary>
    /// An entity that can have error state.
    /// </summary>
    public interface IHasErrorState
    {
        /// <summary>
        /// Get a value that indicates if the entity is erroneous.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// Set the error state of the entity.
        /// </summary>
        /// <remarks>
        /// Implementations shall only consider the first call to this method, setting <see cref="HasError"/>
        /// to <c>true</c>. Further calls to this function shall be ignored.
        /// </remarks>
        /// <param name="ex">An optional exception that caused the error.</param>
        /// <param name="errorCode">An option error code that describes the error.</param>
        void SetError(Exception? ex = null, int errorCode = 0);
    }
}
