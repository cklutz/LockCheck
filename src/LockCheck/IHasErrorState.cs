using System;

namespace LockCheck
{
    public interface IHasErrorState
    {
        bool HasError { get; }
        void SetError(Exception ex = null);
    }
}
