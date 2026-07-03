using System;
using System.Collections.Generic;

namespace TK.Core.Utilities
{
    public class SharedBool<TCallerKey>
    {
        private readonly HashSet<TCallerKey> _callers;
        private bool _value;
        private readonly bool _stateToProtect;
        private readonly Action _getAction;
        private readonly Action<bool, TCallerKey> _setAction;

        public SharedBool(bool defaultValue, bool stateToProtect, Action getAction = null, Action<bool, TCallerKey> setAction = null)
        {
            _value = defaultValue;
            _stateToProtect = stateToProtect;
            _getAction = getAction;
            _setAction = setAction;
            _callers = new HashSet<TCallerKey>();
        }

        public bool GetState()
        {
            _getAction?.Invoke();

            return _value;
        }

        public void SetState(bool state, TCallerKey key)
        {
            if (state == _stateToProtect)
            {
                if (!_callers.Add(key))
                    return;
            }
            else
            {
                _callers.Remove(key);

                if (_callers.Count != 0)
                    return;
            }

            _value = state;
            _setAction?.Invoke(_value, key);
        }

        public static implicit operator bool(SharedBool<TCallerKey> sharedBool)
        {
            return sharedBool.GetState();
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }
}