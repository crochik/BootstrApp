using System;
using System.ComponentModel;

namespace Reports.Controllers
{
    /// <summary>
    /// Dynamic property descriptor is used for iTypedList implementation in AppDataset.
    /// </summary>
    /// <typeparam name="TTarget">DatasetRow.</typeparam>
    /// <typeparam name="TProperty">Data type of field value.</typeparam>
    public class DynamicPropertyDescriptor<TTarget, TProperty> : PropertyDescriptor
    {
        private readonly Func<TTarget, string, TProperty> getter;
        private readonly Action<TTarget, string, TProperty> setter;
        private readonly string propertyName;

        public DynamicPropertyDescriptor(
           string propertyName,
           Func<TTarget, string, TProperty> getter,
           Action<TTarget, string, TProperty> setter,
           Attribute[] attributes)
              : base(propertyName, attributes ?? new Attribute[] { })
        {
            this.setter = setter;
            this.getter = getter;
            this.propertyName = propertyName;
        }

        public DynamicPropertyDescriptor(string propertyName, Func<TTarget, string, TProperty> getter) : base(propertyName, new Attribute[] { })
        {
            this.setter = null;
            this.getter = getter;
            this.propertyName = propertyName;
        }

        public override bool Equals(object obj)
        {
            return obj is DynamicPropertyDescriptor<TTarget, TProperty> o && o.propertyName.Equals(propertyName);
        }

        public override int GetHashCode()
        {
            return propertyName.GetHashCode();
        }

        public override Type ComponentType
        {
            get { return typeof(TTarget); }
        }

        public override Type PropertyType
        {
            get { return typeof(TProperty); }
        }

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override bool IsReadOnly
        {
            get { return setter == null; }
        }

        public override object GetValue(object component)
        {
            return getter((TTarget)component, propertyName);
        }

        public override void ResetValue(object component)
        {
            throw new NotImplementedException();
        }

        public override void SetValue(object component, object value)
        {
            if (setter == null)
            {
                throw new NotSupportedException("object does not support setting values thru dynamic property descriptor or property is read only");
            }
            setter((TTarget)component, propertyName, (TProperty)value);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return true;
        }
    }

}
