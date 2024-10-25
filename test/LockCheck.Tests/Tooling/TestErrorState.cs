using System;

namespace LockCheck.Tests.Tooling
{
    internal class TestErrorState : IHasErrorState
    {
        private bool _hasError;
        private Exception? _exception;
        private int? _errorCode;

        public bool HasError => _hasError;
        public Exception? Exception => _exception;
        public int? ErrorCode => _errorCode;

        public void SetError(Exception? ex = null, int errorCode = 0)
        {
            if (!_hasError)
            {
                _hasError = true;
                _exception = ex;
                _errorCode = errorCode;
            }
        }

        public override string ToString()
        {
            if (_hasError)
            {
                return $"HasError=true (code 0x{_errorCode:X8}, exception \"{_exception?.Message}\")";
            }

            return "HasError=false";
        }
    }
}
