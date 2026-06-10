namespace PI.Shared
{
    public class TitleBuilder
    {
        private string _value;

        public TitleBuilder(string value)
        {
            _value = value;
        }

        public TitleBuilder WithoutFileExtension()
        {
            var index = _value.LastIndexOf('.');
            if (index > 0) _value = _value.Substring(0, index);
            return this;
        }

        public TitleBuilder WithMaxLengthOf(int length, bool elipsisInTheMiddle = false)
        {
            if (_value.Length > length)
            {
                if (elipsisInTheMiddle)
                {
                    var tmp = $"{_value[0..((length - 3) / 2)]}...";
                    _value = $"{tmp}{_value[(_value.Length - length + tmp.Length)..]}";
                }
                else
                {
                    _value = $"{_value[0..(length - 3)]}...";
                }
            }

            return this;
        }

        public string Build() => _value;
    }
}