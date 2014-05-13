﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Xml;

namespace XSerializer
{
    public static class XmlSerializer
    {
        private static readonly ConcurrentDictionary<Type, Func<Action<XmlSerializationOptions>, Type[], IXmlSerializer>> _createXmlSerializerFuncs = new ConcurrentDictionary<Type, Func<Action<XmlSerializationOptions>, Type[], IXmlSerializer>>(); 

        public static IXmlSerializer Create(Type type, params Type[] extraTypes)
        {
            return Create(type, options => {}, extraTypes);
        }

        public static IXmlSerializer Create(Type type, Action<XmlSerializationOptions> setOptions, params Type[] extraTypes)
        {
            var createXmlSerializer = _createXmlSerializerFuncs.GetOrAdd(
                type,
                t =>
                    {
                        var xmlSerializerType = typeof(XmlSerializer<>).MakeGenericType(t);
                        var ctor = xmlSerializerType.GetConstructor(new[] { typeof(Action<XmlSerializationOptions>), typeof(Type[]) });

                        Debug.Assert(ctor != null);

                        var setOptionsParameter = Expression.Parameter(typeof(Action<XmlSerializationOptions>), "setOptions");
                        var extraTypesParameter = Expression.Parameter(typeof(Type[]), "extraTypes");

                        var lambda =
                            Expression.Lambda<Func<Action<XmlSerializationOptions>, Type[], IXmlSerializer>>(
                                Expression.New(ctor, setOptionsParameter, extraTypesParameter),
                                setOptionsParameter,
                                extraTypesParameter);

                        return lambda.Compile();
                    });

            return createXmlSerializer(setOptions, extraTypes);
        }
    }

    public class XmlSerializer<T> : IXmlSerializer
    {
        private readonly IXmlSerializerInternal<T> _serializer;
        private readonly Encoding _encoding;
        private readonly Formatting _formatting;
        private readonly ISerializeOptions _serializeOptions;

        public XmlSerializer(params Type[] extraTypes)
            : this(options => {}, extraTypes)
        {
        }

        public XmlSerializer(Action<XmlSerializationOptions> setOptions, params Type[] extraTypes)
        {
            var options = new XmlSerializationOptions();
            
            if (setOptions != null)
            {
                setOptions(options);
            }

            if (options.RootElementName == null)
            {
                options.SetRootElementName(typeof(T).GetElementName());
            }

            options.ExtraTypes = extraTypes;

            _serializer = XmlSerializerFactory.Instance.GetSerializer<T>(options);
            _encoding = options.Encoding ?? Encoding.UTF8;
            _formatting = options.ShouldIndent ? Formatting.Indented : Formatting.None;
            _serializeOptions = options;
        }

        internal IXmlSerializerInternal<T> Serializer
        {
            get { return _serializer; }
        }

        public string Serialize(T instance)
        {
            return _serializer.Serialize(instance, _encoding, _formatting, _serializeOptions);
        }

        string IXmlSerializer.Serialize(object instance)
        {
            return Serialize((T)instance);
        }

        public void Serialize(Stream stream, T instance)
        {
            _serializer.Serialize(stream, instance, _encoding, _formatting, _serializeOptions);
        }

        void IXmlSerializer.Serialize(Stream stream, object instance)
        {
            Serialize(stream, (T)instance);
        }

        public void Serialize(TextWriter writer, T instance)
        {
            _serializer.Serialize(writer, instance, _formatting, _serializeOptions);
        }

        void IXmlSerializer.Serialize(TextWriter writer, object instance)
        {
            Serialize(writer, (T)instance);
        }

        public T Deserialize(string xml)
        {
            return _serializer.Deserialize(xml);
        }

        object IXmlSerializer.Deserialize(string xml)
        {
            return Deserialize(xml);
        }

        public T Deserialize(Stream stream)
        {
            return _serializer.Deserialize(stream);
        }

        object IXmlSerializer.Deserialize(Stream stream)
        {
            return Deserialize(stream);
        }

        public T Deserialize(TextReader reader)
        {
            return _serializer.Deserialize(reader);
        }

        object IXmlSerializer.Deserialize(TextReader reader)
        {
            return Deserialize(reader);
        }
    }
}
